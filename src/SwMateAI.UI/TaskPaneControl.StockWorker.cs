using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Controls;
using SwMateAI.Core.Agent;

namespace SwMateAI.UI
{
    public partial class TaskPaneControl
    {
        private const int StockWorkerNoProgressTimeoutSeconds = 300;
        private bool _stockWorkerRunning;

        private void StartStockExportWorker(AgentCore agent, string outputText, TextBlock status)
        {
            if (_stockWorkerRunning)
            {
                SetBomStatus(status, "[RUN] Bảng phôi đang được xuất. Vui lòng chờ hoàn tất.", 251, 191, 36);
                return;
            }
            if (!TryBeginExtractionTask("Bảng phôi Excel", status)) return;

            bool handedOffToWorker = false;
            Timer watchdogTimer = null;
            try
            {
                AgentContext context = agent.ObserveContext();
                if (!context.HasActiveDocument ||
                    !string.Equals(context.DocumentType, "Assembly", StringComparison.OrdinalIgnoreCase))
                {
                    SetBomStatus(status, "[LỖI] Hãy mở một Assembly trước khi xuất bảng phôi.", 248, 113, 113);
                    return;
                }
                if (string.IsNullOrWhiteSpace(context.DocumentPath) || !File.Exists(context.DocumentPath))
                {
                    SetBomStatus(status, "[LỖI] Hãy lưu Assembly trước khi xuất bảng phôi.", 248, 113, 113);
                    return;
                }

                string folder;
                try
                {
                    folder = ResolveOutputFolder(agent, outputText);
                    Directory.CreateDirectory(folder);
                }
                catch (Exception ex)
                {
                    SetBomStatus(status, "[LỖI] Thư mục đầu ra: " + ex.Message, 248, 113, 113);
                    return;
                }

                string name = SafeUiFileName(Path.GetFileNameWithoutExtension(context.DocumentPath));
                string outputPath = UniqueUiPath(Path.Combine(folder, name + "_StockMaterial.xlsx"));
                string baseDirectory = Path.GetDirectoryName(typeof(TaskPaneControl).Assembly.Location) ?? string.Empty;
                string workerDirectory = Path.Combine(baseDirectory, "StockWorker");
                string workerPath = Path.Combine(workerDirectory, "SwMateAI.StockWorker.exe");
                if (!File.Exists(workerPath))
                {
                    SetBomStatus(status, "[LỖI] Thiếu StockWorker\\SwMateAI.StockWorker.exe.", 248, 113, 113);
                    return;
                }

                var stdout = new StringBuilder();
                var stderr = new StringBuilder();
                long lastProgressTicks = DateTime.UtcNow.Ticks;
                int childSolidWorksPid = 0;
                int timeoutTriggered = 0;
                string lastPhase = "Khởi động worker";
                object phaseSync = new object();

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = workerPath,
                        WorkingDirectory = workerDirectory,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        Arguments = "--source " + QuoteArgument(context.DocumentPath) +
                                    " --output " + QuoteArgument(outputPath) +
                                    " --watchdog-seconds " + StockWorkerNoProgressTimeoutSeconds
                    },
                    EnableRaisingEvents = true
                };

                process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data == null) return;
                    Interlocked.Exchange(ref lastProgressTicks, DateTime.UtcNow.Ticks);
                    lock (stdout) stdout.AppendLine(e.Data);
                    lock (phaseSync) lastPhase = e.Data;

                    if (e.Data.StartsWith("PROGRESS|INSTANCE|", StringComparison.OrdinalIgnoreCase))
                    {
                        string[] instanceParts = e.Data.Split('|');
                        if (e.Data.StartsWith("PROGRESS|INSTANCE|READY|", StringComparison.OrdinalIgnoreCase))
                        {
                            int parsedPid = FindProgressInt(instanceParts, "PID");
                            if (parsedPid > 0) Interlocked.Exchange(ref childSolidWorksPid, parsedPid);
                        }
                        else if (e.Data.StartsWith("PROGRESS|INSTANCE|STARTING|", StringComparison.OrdinalIgnoreCase))
                        {
                            Interlocked.Exchange(ref childSolidWorksPid, 0);
                        }

                        UpdateExtractionProgress(0, 0, "Bảng phôi — đang chuẩn bị SolidWorks nền...");
                        return;
                    }

                    if (e.Data.StartsWith("PROGRESS|STOCK|", StringComparison.OrdinalIgnoreCase))
                    {
                        string[] parts = e.Data.Split('|');
                        string stage = parts.Length > 2 ? parts[2] : string.Empty;
                        string detail = parts.Length > 3 ? parts[3] : string.Empty;
                        Dispatcher.BeginInvoke(new Action(delegate
                        {
                            SetBomStatus(status,
                                "[RUN] Bảng phôi — " + DescribeStockStage(stage) +
                                (string.IsNullOrWhiteSpace(detail) ? string.Empty : " — " + detail),
                                125, 211, 252);
                            UpdateExtractionProgress(0, 0,
                                "Bảng phôi — " + DescribeStockStage(stage) +
                                (string.IsNullOrWhiteSpace(detail) ? string.Empty : " — " + detail));
                        }));
                        return;
                    }

                    if (!e.Data.StartsWith("PROGRESS|IMAGE|", StringComparison.OrdinalIgnoreCase)) return;
                    string[] imageParts = e.Data.Split('|');
                    string progress = imageParts.Length > 2 ? imageParts[2] : string.Empty;
                    string ok = imageParts.Length > 3 ? imageParts[3].Replace("OK=", string.Empty) : "?";
                    string skip = imageParts.Length > 4 ? imageParts[4].Replace("SKIP=", string.Empty) : "?";
                    ParseProgress(progress, out double current, out double total);
                    Dispatcher.BeginInvoke(new Action(delegate
                    {
                        SetBomStatus(status,
                            "[RUN] Bảng phôi — ảnh " + progress + " — OK=" + ok + ", bỏ qua=" + skip,
                            125, 211, 252);
                        UpdateExtractionProgress(current, total,
                            "Bảng phôi — ảnh " + progress + ", OK=" + ok + ", bỏ qua=" + skip + ".");
                    }));
                };

                process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data == null) return;
                    Interlocked.Exchange(ref lastProgressTicks, DateTime.UtcNow.Ticks);
                    lock (stderr) stderr.AppendLine(e.Data);
                };

                process.Exited += delegate
                {
                    try { watchdogTimer?.Dispose(); } catch { }
                    int exitCode = -1;
                    try { process.WaitForExit(); exitCode = process.ExitCode; } catch { }
                    string outText; string errText;
                    lock (stdout) outText = stdout.ToString();
                    lock (stderr) errText = stderr.ToString();
                    bool timedOut = Interlocked.CompareExchange(ref timeoutTriggered, 0, 0) != 0;

                    Dispatcher.BeginInvoke(new Action(delegate
                    {
                        _stockWorkerRunning = false;
                        if (timedOut)
                        {
                            string phase;
                            lock (phaseSync) phase = lastPhase;
                            SetBomStatus(status,
                                "[LỖI] Bảng phôi đã được dừng an toàn vì worker không có tiến triển trong " +
                                StockWorkerNoProgressTimeoutSeconds + " giây. Phase cuối: " + phase,
                                248, 113, 113);
                        }
                        else
                        {
                            ApplyStockWorkerResult(status, exitCode, outText, errText);
                        }

                        bool success = !timedOut && exitCode == 0 &&
                                       outText.IndexOf("RESULT|OK|", StringComparison.OrdinalIgnoreCase) >= 0;
                        EndExtractionTask(success
                            ? "Bảng phôi Excel — hoàn tất."
                            : "Bảng phôi Excel — kết thúc có lỗi.");
                        try { process.Dispose(); } catch { }
                    }));
                };

                _stockWorkerRunning = true;
                SetBomStatus(status,
                    "[RUN] Đang khởi động worker Bảng phôi. SolidWorks chính vẫn có thể tiếp tục sử dụng.",
                    125, 211, 252);
                UpdateExtractionProgress(0, 0, "Bảng phôi — đang khởi động worker...");
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                watchdogTimer = new Timer(delegate
                {
                    if (Interlocked.CompareExchange(ref timeoutTriggered, 0, 0) != 0) return;
                    long ticks = Interlocked.Read(ref lastProgressTicks);
                    double idleSeconds = TimeSpan.FromTicks(Math.Max(0, DateTime.UtcNow.Ticks - ticks)).TotalSeconds;
                    if (idleSeconds <= StockWorkerNoProgressTimeoutSeconds) return;
                    if (Interlocked.Exchange(ref timeoutTriggered, 1) != 0) return;

                    int swPid = Interlocked.CompareExchange(ref childSolidWorksPid, 0, 0);
                    KillProcessSafe(swPid);
                    int workerPid = 0;
                    try { workerPid = process.Id; } catch { }
                    KillProcessSafe(workerPid);

                    Dispatcher.BeginInvoke(new Action(delegate
                    {
                        SetBomStatus(status,
                            "[CẢNH BÁO] Worker Bảng phôi không có tiến triển nên đã được dừng để bảo vệ SolidWorks.",
                            251, 191, 36);
                        UpdateExtractionProgress(0, 1, "Bảng phôi — watchdog đã dừng worker bị treo.");
                    }));
                }, null, 5000, 5000);

                handedOffToWorker = true;
            }
            catch (Exception ex)
            {
                try { watchdogTimer?.Dispose(); } catch { }
                _stockWorkerRunning = false;
                SetBomStatus(status, "[LỖI] Không khởi động được Stock worker: " + ex.Message, 248, 113, 113);
            }
            finally
            {
                if (!handedOffToWorker)
                {
                    try { watchdogTimer?.Dispose(); } catch { }
                    _stockWorkerRunning = false;
                    EndExtractionTask("Bảng phôi Excel — không chạy.");
                }
            }
        }

        private static int FindProgressInt(string[] parts, string key)
        {
            string prefix = key + "=";
            foreach (string part in parts ?? Array.Empty<string>())
            {
                if (!part.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                if (int.TryParse(part.Substring(prefix.Length), out int value)) return value;
            }
            return 0;
        }

        private static void KillProcessSafe(int pid)
        {
            if (pid <= 0) return;
            try
            {
                var target = Process.GetProcessById(pid);
                if (!target.HasExited) target.Kill();
                target.Dispose();
            }
            catch { }
        }

        private static string DescribeStockStage(string stage)
        {
            switch ((stage ?? string.Empty).ToUpperInvariant())
            {
                case "OPEN": return "đang mở Assembly (lightweight)";
                case "RESOLVE": return "đang resolve component";
                case "BUILD": return "đang đọc vật liệu và tính phôi";
                case "IMAGES": return "đang chuẩn bị ảnh chi tiết";
                case "EXPORT": return "đang ghi Excel";
                default: return string.IsNullOrWhiteSpace(stage) ? "đang xử lý" : stage;
            }
        }

        private static void ApplyStockWorkerResult(TextBlock status, int exitCode, string stdout, string stderr)
        {
            string line = (stdout ?? string.Empty)
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .LastOrDefault(value => value.StartsWith("RESULT|", StringComparison.OrdinalIgnoreCase));

            if (exitCode == 0 && !string.IsNullOrWhiteSpace(line) && line.StartsWith("RESULT|OK|", StringComparison.OrdinalIgnoreCase))
            {
                string[] parts = line.Split('|');
                string path = parts.Length > 2 ? parts[2] : string.Empty;
                SetBomStatus(status,
                    "[OK] Bảng phôi hoàn tất. Dòng=" + FindWorkerValue(parts, "ITEMS") +
                    ", ảnh=" + FindWorkerValue(parts, "IMAGES") +
                    ", bỏ qua=" + FindWorkerValue(parts, "SKIPPED") +
                    ", unloaded=" + FindWorkerValue(parts, "UNLOADED") +
                    ", cần rà vật liệu=" + FindWorkerValue(parts, "REVIEW") +
                    ", phiên nền=" + FindWorkerValue(parts, "SESSIONS") +
                    ", thời gian=" + FindWorkerValue(parts, "MS") + " ms. File=" + path +
                    ". Log=" + FindWorkerValue(parts, "LOG"),
                    52, 211, 153);
                return;
            }

            string error = string.Empty;
            if (!string.IsNullOrWhiteSpace(line) && line.StartsWith("RESULT|ERROR|", StringComparison.OrdinalIgnoreCase))
                error = line.Substring("RESULT|ERROR|".Length);
            if (string.IsNullOrWhiteSpace(error)) error = (stderr ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(error)) error = "Stock worker kết thúc với mã " + exitCode + ".";
            SetBomStatus(status, "[LỖI] Bảng phôi: " + error, 248, 113, 113);
        }

        private static string UniqueUiPath(string path)
        {
            if (!File.Exists(path)) return path;
            string dir = Path.GetDirectoryName(path) ?? string.Empty;
            string name = Path.GetFileNameWithoutExtension(path);
            string ext = Path.GetExtension(path);
            for (int i = 1; i < 10000; i++)
            {
                string candidate = Path.Combine(dir, name + "_" + i + ext);
                if (!File.Exists(candidate)) return candidate;
            }
            throw new IOException("Không thể tạo tên file đầu ra duy nhất.");
        }
    }
}
