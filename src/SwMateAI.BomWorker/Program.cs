using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading;
using SolidWorks.Interop.sldworks;
using SwMateAI.Core.BOM;
using Environment = System.Environment;

namespace SwMateAI.BomWorker
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            ISldWorks isolated = null;
            Process isolatedProcess = null;
            string tempRoot = null;

            try
            {
                string manifestPath = ParseArgument(args, "--manifest");
                string outputFolder = ParseArgument(args, "--output");
                if (string.IsNullOrWhiteSpace(manifestPath) || !File.Exists(manifestPath))
                    return Fail("BOM manifest was not found.");

                BomManifest manifest = ReadManifest(manifestPath);
                if (manifest.Result.Items.Count == 0)
                    return Fail("BOM manifest contains no items.");

                if (string.IsNullOrWhiteSpace(outputFolder))
                {
                    string root = Path.GetDirectoryName(manifest.AssemblyPath) ??
                                  Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    outputFolder = Path.Combine(root, "SW-MATE_AI_Output");
                }
                Directory.CreateDirectory(outputFolder);

                tempRoot = Path.Combine(
                    Path.GetTempPath(),
                    "SW-MATE_AI",
                    "BOM_Worker",
                    Guid.NewGuid().ToString("N"));
                string localSourceFolder = Path.Combine(tempRoot, "source");
                string imageFolder = Path.Combine(tempRoot, "images");
                Directory.CreateDirectory(localSourceFolder);
                Directory.CreateDirectory(imageFolder);

                Console.WriteLine("PROGRESS|INSTANCE|STARTING");
                Console.Out.Flush();

                isolatedProcess = StartIsolatedSolidWorks();
                if (isolatedProcess == null)
                    return Fail("Could not start isolated SOLIDWORKS preview process.");

                isolated = WaitForSolidWorksCom(isolatedProcess.Id, 45000);
                if (isolated == null)
                    return Fail("Isolated SOLIDWORKS preview process did not become available.");

                try { isolated.Visible = false; } catch { }
                Console.WriteLine("PROGRESS|INSTANCE|READY|PID=" + isolatedProcess.Id);
                Console.Out.Flush();

                var capture = new BomPreviewImageCapture(isolated);
                var localCopies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                int captured = 0;
                int skipped = 0;
                int index = 0;
                var clock = Stopwatch.StartNew();

                foreach (BomItem item in manifest.Result.Items)
                {
                    index++;
                    string originalSource = item.SourcePath;
                    try
                    {
                        string localSource = LocalizeSource(
                            originalSource,
                            localSourceFolder,
                            localCopies,
                            index);

                        if (string.IsNullOrWhiteSpace(localSource))
                        {
                            skipped++;
                        }
                        else
                        {
                            item.SourcePath = localSource;
                            string image = capture.Capture(item, imageFolder);
                            if (!string.IsNullOrWhiteSpace(image) && File.Exists(image)) captured++;
                            else skipped++;
                        }
                    }
                    catch
                    {
                        skipped++;
                    }
                    finally
                    {
                        item.SourcePath = originalSource;
                    }

                    Console.WriteLine(
                        "PROGRESS|IMAGE|" + index + "/" + manifest.Result.Items.Count +
                        "|OK=" + captured + "|SKIP=" + skipped);
                    Console.Out.Flush();

                    Thread.Sleep(150);
                }

                manifest.Result.CapturedImageCount = captured;
                string baseName = SafeBaseName(manifest.AssemblyPath);
                string outputPath = UniquePath(Path.Combine(outputFolder, baseName + "_BOM.xlsx"));
                new BomFastExcelExporter().Export(manifest.Result, outputPath);
                clock.Stop();

                Console.WriteLine(
                    "RESULT|OK|" + Escape(outputPath) +
                    "|ITEMS=" + manifest.Result.Items.Count +
                    "|IMAGES=" + captured +
                    "|SKIPPED=" + skipped +
                    "|MS=" + clock.ElapsedMilliseconds +
                    "|PID=" + isolatedProcess.Id);
                return 0;
            }
            catch (Exception ex)
            {
                return Fail(ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                if (isolated != null)
                {
                    try { isolated.ExitApp(); } catch { }
                }
                ReleaseCom(isolated);

                if (isolatedProcess != null)
                {
                    try
                    {
                        if (!isolatedProcess.WaitForExit(5000) && !isolatedProcess.HasExited)
                            isolatedProcess.Kill();
                    }
                    catch { }
                    try { isolatedProcess.Dispose(); } catch { }
                }

                if (!string.IsNullOrWhiteSpace(tempRoot) && Directory.Exists(tempRoot))
                {
                    try { Directory.Delete(tempRoot, true); } catch { }
                }
            }
        }

        private static string LocalizeSource(
            string sourcePath,
            string localFolder,
            Dictionary<string, string> cache,
            int index)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                return string.Empty;

            if (cache.TryGetValue(sourcePath, out string cached) && File.Exists(cached))
                return cached;

            string extension = Path.GetExtension(sourcePath);
            string destination = Path.Combine(
                localFolder,
                "source_" + index.ToString("D4") + extension);

            try
            {
                File.Copy(sourcePath, destination, true);
                cache[sourcePath] = destination;
                return destination;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static BomManifest ReadManifest(string path)
        {
            var manifest = new BomManifest();
            foreach (string raw in File.ReadLines(path, Encoding.UTF8))
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                string[] parts = raw.Split('|');
                if (parts.Length < 2) continue;

                if (parts[0] == "ASSEMBLY")
                {
                    manifest.AssemblyPath = Decode(parts[1]);
                    continue;
                }

                if (parts[0] != "ITEM" || parts.Length < 13) continue;
                manifest.Result.Items.Add(new BomItem
                {
                    ItemNumber = ParseInt(parts[1]),
                    Level = ParseInt(parts[2]),
                    Quantity = ParseInt(parts[3]),
                    PartNumber = Decode(parts[4]),
                    Description = Decode(parts[5]),
                    Material = Decode(parts[6]),
                    ComponentType = Decode(parts[7]),
                    Configuration = Decode(parts[8]),
                    SourcePath = Decode(parts[9]),
                    RepresentativeComponentName = Decode(parts[10]),
                    IsVirtual = parts[11] == "1",
                    IsLoaded = parts[12] == "1"
                });
            }
            return manifest;
        }

        private static int ParseInt(string value)
        {
            return int.TryParse(value, out int parsed) ? parsed : 0;
        }

        private static string Decode(string value)
        {
            try { return Encoding.UTF8.GetString(Convert.FromBase64String(value ?? string.Empty)); }
            catch { return string.Empty; }
        }

        private static Process StartIsolatedSolidWorks()
        {
            var before = new HashSet<int>(
                Process.GetProcessesByName("SLDWORKS").Select(process => process.Id));

            string executable = ResolveSolidWorksExecutable();
            if (string.IsNullOrWhiteSpace(executable) || !File.Exists(executable))
                return null;

            Process.Start(new ProcessStartInfo
            {
                FileName = executable,
                Arguments = "/b",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });

            var clock = Stopwatch.StartNew();
            while (clock.ElapsedMilliseconds < 45000)
            {
                Process candidate = Process.GetProcessesByName("SLDWORKS")
                    .Where(process => !before.Contains(process.Id))
                    .OrderByDescending(process => SafeStartTime(process))
                    .FirstOrDefault();
                if (candidate != null) return candidate;
                Thread.Sleep(500);
            }
            return null;
        }

        private static string ResolveSolidWorksExecutable()
        {
            try
            {
                Process existing = Process.GetProcessesByName("SLDWORKS").FirstOrDefault();
                string path = existing?.MainModule?.FileName;
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path)) return path;
            }
            catch { }

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "SOLIDWORKS Corp",
                "SOLIDWORKS",
                "SLDWORKS.exe");
        }

        private static DateTime SafeStartTime(Process process)
        {
            try { return process.StartTime; }
            catch { return DateTime.MinValue; }
        }

        private static ISldWorks WaitForSolidWorksCom(int processId, int timeoutMilliseconds)
        {
            string monikerName = "SolidWorks_PID_" + processId;
            var clock = Stopwatch.StartNew();
            while (clock.ElapsedMilliseconds < timeoutMilliseconds)
            {
                object value = TryGetRotObject(monikerName);
                if (value is ISldWorks app) return app;
                ReleaseCom(value);
                Thread.Sleep(500);
            }
            return null;
        }

        private static object TryGetRotObject(string targetName)
        {
            IRunningObjectTable rot = null;
            IEnumMoniker enumerator = null;
            IBindCtx bindContext = null;
            try
            {
                if (GetRunningObjectTable(0, out rot) != 0 || rot == null) return null;
                rot.EnumRunning(out enumerator);
                if (enumerator == null) return null;

                var monikers = new IMoniker[1];
                while (enumerator.Next(1, monikers, IntPtr.Zero) == 0)
                {
                    IMoniker moniker = monikers[0];
                    try
                    {
                        if (CreateBindCtx(0, out bindContext) != 0 || bindContext == null) continue;
                        string displayName = string.Empty;
                        try { moniker.GetDisplayName(bindContext, null, out displayName); }
                        catch { }

                        if (string.Equals(displayName, targetName, StringComparison.OrdinalIgnoreCase))
                        {
                            rot.GetObject(moniker, out object value);
                            return value;
                        }
                    }
                    finally
                    {
                        ReleaseCom(bindContext);
                        bindContext = null;
                        ReleaseCom(moniker);
                    }
                }
                return null;
            }
            finally
            {
                ReleaseCom(bindContext);
                ReleaseCom(enumerator);
                ReleaseCom(rot);
            }
        }

        private static string SafeBaseName(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path ?? string.Empty);
            return string.IsNullOrWhiteSpace(name) ? "Assembly" : name;
        }

        private static string UniquePath(string path)
        {
            if (!File.Exists(path)) return path;
            string dir = Path.GetDirectoryName(path) ?? string.Empty;
            string name = Path.GetFileNameWithoutExtension(path);
            string ext = Path.GetExtension(path);
            for (int i = 1; i < 1000; i++)
            {
                string candidate = Path.Combine(dir, name + "_" + i + ext);
                if (!File.Exists(candidate)) return candidate;
            }
            throw new IOException("Could not allocate a unique BOM output file name.");
        }

        private static string ParseArgument(string[] args, string key)
        {
            if (args == null) return string.Empty;
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1]?.Trim() ?? string.Empty;
            return string.Empty;
        }

        private static int Fail(string message)
        {
            Console.WriteLine("RESULT|ERROR|" + Escape(message));
            return 1;
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty)
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Replace("|", "/");
        }

        private static void ReleaseCom(object value)
        {
            if (value == null || !Marshal.IsComObject(value)) return;
            try { Marshal.FinalReleaseComObject(value); } catch { }
        }

        private sealed class BomManifest
        {
            public string AssemblyPath { get; set; } = string.Empty;
            public BomResult Result { get; } = new BomResult();
        }

        [DllImport("ole32.dll")]
        private static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable runningObjectTable);

        [DllImport("ole32.dll")]
        private static extern int CreateBindCtx(int reserved, out IBindCtx bindContext);
    }
}
