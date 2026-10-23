using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("SW-MATE AI Updater")]
[assembly: AssemblyProduct("SW-MATE AI")]
[assembly: AssemblyVersion("0.2.0.0")]
[assembly: AssemblyFileVersion("0.2.0.0")]

namespace SwMateAI.Updater
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                if (args.Length > 0 && args[0] == "--self-test")
                    return SelfTest();
                if (args.Length == 3 && args[0] == "--apply")
                    return ApplyUpdate(args[1], args[2]);

                bool notify = args.Length > 0 && args[0] == "--notify";
                if (notify && CheckedRecently()) return 0;

                ReleaseInfo release;
                try
                {
                    release = UpdateService.GetLatestAsync().GetAwaiter().GetResult();
                    MarkChecked();
                }
                catch (Exception ex)
                {
                    if (!notify) MessageBox.Show(ex.Message, "SW-MATE AI - Cập nhật",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 1;
                }

                if (release.Version <= UpdateService.InstalledVersion)
                {
                    if (!notify) MessageBox.Show(
                        "Đã là phiên bản mới nhất (" + UpdateService.InstalledVersion + ").",
                        "SW-MATE AI", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 0;
                }

                if (MessageBox.Show(
                    "Có bản SW-MATE AI " + release.Version.ToString(3) +
                    " mới.\nPhiên bản đang dùng: " + UpdateService.InstalledVersion +
                    "\n\nTải bản cập nhật ngay?",
                    "SW-MATE AI - Có cập nhật", MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes) return 0;

                Application.Run(new DownloadForm(release));
                return 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "SW-MATE AI - Lỗi cập nhật",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }

        private static bool CheckedRecently()
        {
            try
            {
                string path = Path.Combine(UpdateService.CacheDirectory, "last-check.txt");
                return File.Exists(path) &&
                    DateTime.UtcNow - File.GetLastWriteTimeUtc(path) < TimeSpan.FromHours(24);
            }
            catch { return false; }
        }

        private static void MarkChecked()
        {
            try
            {
                Directory.CreateDirectory(UpdateService.CacheDirectory);
                File.WriteAllText(Path.Combine(UpdateService.CacheDirectory, "last-check.txt"),
                    DateTime.UtcNow.ToString("o"));
            }
            catch { /* Lack of cache permission must not prevent update checking. */ }
        }

        internal static void StartApplyProcess(string installer, string expectedHash)
        {
            // Copy the updater outside Program Files so Inno Setup can replace its
            // installed copy without trying to overwrite a running executable.
            string runner = Path.Combine(UpdateService.CacheDirectory, "SW-MATE-AI-UpdateRunner.exe");
            File.Copy(Assembly.GetExecutingAssembly().Location, runner, true);
            Process.Start(new ProcessStartInfo
            {
                FileName = runner,
                Arguments = "--apply \"" + installer + "\" " + expectedHash,
                UseShellExecute = true,
                WorkingDirectory = UpdateService.CacheDirectory
            });
        }

        private static int ApplyUpdate(string installer, string expectedHash)
        {
            string full = Path.GetFullPath(installer);
            string allowed = Path.GetFullPath(UpdateService.CacheDirectory)
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(allowed, StringComparison.OrdinalIgnoreCase) ||
                !UpdateService.IsSha256(expectedHash) ||
                !File.Exists(full) ||
                !UpdateService.HashFile(full).Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("File cập nhật không nằm trong cache hợp lệ hoặc sai SHA-256.");

            // Never terminate SOLIDWORKS: users may have unsaved CAD documents.
            DateTime deadline = DateTime.UtcNow.AddMinutes(15);
            while (Process.GetProcessesByName("SLDWORKS").Length > 0)
            {
                if (DateTime.UtcNow > deadline)
                {
                    MessageBox.Show("Hết thời gian chờ đóng SOLIDWORKS. Hãy thử cập nhật lại.",
                        "SW-MATE AI", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return 2;
                }
                Thread.Sleep(2000);
            }

            // UAC elevation is requested by the Inno installer, not bypassed.
            using (var setup = Process.Start(new ProcessStartInfo
            {
                FileName = full,
                Arguments = "/SILENT /SUPPRESSMSGBOXES /NORESTART",
                UseShellExecute = true,
                Verb = "runas"
            }))
            {
                if (setup == null) throw new InvalidOperationException("Không mở được bộ cài.");
                setup.WaitForExit();
                if (setup.ExitCode != 0)
                {
                    MessageBox.Show("Cài đặt chưa thành công. Mã lỗi: " + setup.ExitCode,
                        "SW-MATE AI", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return setup.ExitCode;
                }
            }
            MessageBox.Show("Cập nhật hoàn tất. Hãy mở lại SOLIDWORKS.",
                "SW-MATE AI", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 0;
        }

        private static int SelfTest()
        {
            string sample = "{\"tag_name\":\"v0.3.0\",\"assets\":[{" +
                "\"name\":\"SW-MATE-AI-Setup-0.3.0.exe\"," +
                "\"browser_download_url\":\"https://github.com/DucHai092004/SolidWorksAIAgent/releases/download/v0.3.0/SW-MATE-AI-Setup-0.3.0.exe\"," +
                "\"digest\":\"sha256:" + new string('a', 64) + "\"}]}";
            var release = UpdateService.ParseRelease(sample);
            bool wrongAssetRejected = RejectsRelease(sample.Replace(
                "SW-MATE-AI-Setup-0.3.0.exe", "SW-MATE-AI-Setup-0.2.0.exe"));
            bool badHashRejected = RejectsRelease(sample.Replace(
                new string('a', 64), "not-a-hash"));
            bool untrustedHostRejected = RejectsRelease(sample.Replace(
                "https://github.com/", "https://example.com/"));
            bool pass = wrongAssetRejected && badHashRejected && untrustedHostRejected &&
                release.Version == new Version(0, 3, 0) &&
                release.Sha256 == new string('a', 64) &&
                UpdateService.TryParseVersion("v2.1.0-beta", out var v) &&
                v == new Version(2, 1, 0) &&
                !UpdateService.IsSha256("bad") &&
                UpdateService.HashFile(Assembly.GetExecutingAssembly().Location).Length == 64;
            string report = Path.Combine(Path.GetTempPath(), "SW-MATE-AI-Updater-SelfTest.txt");
            File.WriteAllText(report, pass ? "PASS" : "FAIL");
            return pass ? 0 : 3;
        }

        private static bool RejectsRelease(string json)
        {
            try { UpdateService.ParseRelease(json); return false; }
            catch (InvalidDataException) { return true; }
        }
    }
}
