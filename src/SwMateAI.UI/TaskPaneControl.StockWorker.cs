using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using SwMateAI.Core.Agent;

namespace SwMateAI.UI
{
    public partial class TaskPaneControl
    {
        private bool _stockWorkerRunning;

        private void StartStockExportWorker(AgentCore agent, string outputText, TextBlock status)
        {
            if (_stockWorkerRunning)
            {
                SetBomStatus(status, "[RUN] Bảng phôi đang được xuất. Vui lòng chờ hoàn tất.", 251, 191, 36);
                return;
            }

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
            try { folder = ResolveOutputFolder(agent, outputText); Directory.CreateDirectory(folder); }
            catch (Exception ex)
            {
                SetBomStatus(status, "[LỖI] Thư mục đầu ra: " + ex.Message, 248, 113, 113);
                return;
            }

            string name = Path.GetFileNameWithoutExtension(context.DocumentPath);
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
                                " --output " + QuoteArgument(outputPath)
                },
                EnableRaisingEvents = true
            };

            process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
            {
                if (e.Data == null) return;
                lock (stdout) stdout.AppendLine(e.Data);
                if (e.Data.StartsWith("PROGRESS|STOCK|", StringComparison.OrdinalIgnoreCase))
                {
                    Dispatcher.BeginInvoke(new Action(delegate
                    {
                        SetBomStatus(status,
                            "[RUN] Đang quét Assembly, đọc vật liệu và tính phôi trong SolidWorks nền riêng...",
                            125, 211, 252);
                    }));
                }
            };
            process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
            {
                if (e.Data == null) return;
                lock (stderr) stderr.AppendLine(e.Data);
            };
            process.Exited += delegate
            {
                int exitCode = -1;
                try { process.WaitForExit(); exitCode = process.ExitCode; } catch { }
                string outText; string errText;
                lock (stdout) outText = stdout.ToString();
                lock (stderr) errText = stderr.ToString();
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    _stockWorkerRunning = false;
                    ApplyStockWorkerResult(status, exitCode, outText, errText);
                    try { process.Dispose(); } catch { }
                }));
            };

            try
            {
                _stockWorkerRunning = true;
                SetBomStatus(status,
                    "[RUN] Đang khởi động worker Bảng phôi. SolidWorks chính vẫn có thể tiếp tục sử dụng.",
                    125, 211, 252);
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
            }
            catch (Exception ex)
            {
                _stockWorkerRunning = false;
                try { process.Dispose(); } catch { }
                SetBomStatus(status, "[LỖI] Không khởi động được Stock worker: " + ex.Message, 248, 113, 113);
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
                    ", ảnh preview=" + FindWorkerValue(parts, "IMAGES") +
                    ", thời gian=" + FindWorkerValue(parts, "MS") + " ms. File=" + path,
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
