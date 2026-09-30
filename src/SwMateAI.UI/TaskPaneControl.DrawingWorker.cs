using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using SwMateAI.Core.Agent;
using SwMateAI.Core.Tools;

namespace SwMateAI.UI
{
    public partial class TaskPaneControl
    {
        private bool _drawingWorkerRunning;

        private void StartDrawingExportWorker(
            AgentCore agent,
            string outputText,
            bool batch,
            bool saveDrawing,
            bool exportPdf,
            TextBlock status,
            string label)
        {
            if (_drawingWorkerRunning)
            {
                SetBomStatus(status, "[RUN] Đang có một tác vụ Drawing/PDF chạy. Vui lòng chờ hoàn tất.", 251, 191, 36);
                return;
            }

            string folder;
            try { folder = PrepareOutputFolder(outputText); }
            catch (Exception ex)
            {
                SetBomStatus(status, "[LỖI] Thư mục đầu ra: " + ex.Message, 248, 113, 113);
                return;
            }

            ToolResult preflight = agent.ExecuteTool(
                "ExportDrawingPackage",
                new Dictionary<string, object>
                {
                    ["Batch"] = batch,
                    ["SaveDrawing"] = saveDrawing,
                    ["ExportPdf"] = exportPdf,
                    ["Projection"] = "Third",
                    ["OutputFolder"] = folder,
                    ["PreviewOnly"] = true,
                    ["PauseMilliseconds"] = batch ? 250 : 100
                });

            if (!preflight.IsSuccess || !(preflight.Data is IDictionary<string, object> map))
            {
                SetBomStatus(status, "[LỖI] " + label + ": " + preflight.ErrorMessage, 248, 113, 113);
                return;
            }

            string[] sources = map.TryGetValue("SourceFiles", out object rawSources) && rawSources is string[] array
                ? array
                : Array.Empty<string>();
            string template = map.TryGetValue("TemplatePath", out object rawTemplate) ? Convert.ToString(rawTemplate) : string.Empty;
            string resolvedFolder = map.TryGetValue("OutputFolder", out object rawFolder) ? Convert.ToString(rawFolder) : folder;
            int pause = map.TryGetValue("PauseMilliseconds", out object rawPause) && int.TryParse(Convert.ToString(rawPause), out int parsedPause)
                ? parsedPause
                : (batch ? 250 : 100);

            if (sources.Length == 0)
            {
                SetBomStatus(status, "[LỖI] Không tìm thấy file nguồn để xuất.", 248, 113, 113);
                return;
            }

            if (batch)
            {
                var answer = MessageBox.Show(
                    "Sẽ xử lý tuần tự " + sources.Length + " chi tiết trong SolidWorks nền riêng.\n" +
                    "Lỗi một chi tiết sẽ được ghi log và tiếp tục chi tiết tiếp theo.\n\nTiếp tục?",
                    "SW-MATE AI - Xuất hàng loạt",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (answer != MessageBoxResult.Yes) return;
            }

            string baseDirectory = Path.GetDirectoryName(typeof(TaskPaneControl).Assembly.Location) ?? string.Empty;
            string workerDirectory = Path.Combine(baseDirectory, "DrawingWorker");
            string workerPath = Path.Combine(workerDirectory, "SwMateAI.DrawingWorker.exe");
            if (!File.Exists(workerPath))
            {
                SetBomStatus(status, "[LỖI] Thiếu DrawingWorker\\SwMateAI.DrawingWorker.exe.", 248, 113, 113);
                return;
            }

            string manifest = Path.Combine(Path.GetTempPath(), "SW_MATE_DRAW_" + Guid.NewGuid().ToString("N") + ".txt");
            try
            {
                WriteDrawingManifest(manifest, template, resolvedFolder, saveDrawing, exportPdf, pause, sources);
            }
            catch (Exception ex)
            {
                SetBomStatus(status, "[LỖI] Không tạo được manifest Drawing: " + ex.Message, 248, 113, 113);
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
                    Arguments = "--manifest " + QuoteArgument(manifest)
                },
                EnableRaisingEvents = true
            };

            process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
            {
                if (e.Data == null) return;
                lock (stdout) stdout.AppendLine(e.Data);
                if (!e.Data.StartsWith("PROGRESS|FILE|", StringComparison.OrdinalIgnoreCase)) return;
                string[] parts = e.Data.Split('|');
                string progress = parts.Length > 2 ? parts[2] : string.Empty;
                string state = parts.Length > 3 ? parts[3] : string.Empty;
                string file = parts.Length > 4 ? parts[4] : string.Empty;
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    SetBomStatus(status,
                        "[RUN] " + label + " — " + progress + " — " + state + " — " + file,
                        state == "ERROR" ? (byte)251 : (byte)125,
                        state == "ERROR" ? (byte)191 : (byte)211,
                        state == "ERROR" ? (byte)36 : (byte)252);
                }));
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
                try { if (File.Exists(manifest)) File.Delete(manifest); } catch { }

                Dispatcher.BeginInvoke(new Action(delegate
                {
                    _drawingWorkerRunning = false;
                    ApplyDrawingWorkerResult(status, label, exitCode, outText, errText);
                    try { process.Dispose(); } catch { }
                }));
            };

            try
            {
                _drawingWorkerRunning = true;
                SetBomStatus(status,
                    "[RUN] " + label + " — đã chuẩn bị " + sources.Length + " file. Đang khởi động SolidWorks nền riêng...",
                    125, 211, 252);
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
            }
            catch (Exception ex)
            {
                _drawingWorkerRunning = false;
                try { if (File.Exists(manifest)) File.Delete(manifest); } catch { }
                try { process.Dispose(); } catch { }
                SetBomStatus(status, "[LỖI] Không khởi động được Drawing worker: " + ex.Message, 248, 113, 113);
            }
        }

        private static void WriteDrawingManifest(
            string path,
            string template,
            string outputFolder,
            bool saveDrawing,
            bool exportPdf,
            int pauseMilliseconds,
            IEnumerable<string> sources)
        {
            using (var writer = new StreamWriter(path, false, new UTF8Encoding(false)))
            {
                writer.WriteLine("VERSION|1");
                writer.WriteLine("TEMPLATE|" + Encode(template));
                writer.WriteLine("OUTPUT|" + Encode(outputFolder));
                writer.WriteLine("PROJECTION|" + Encode("Third"));
                writer.WriteLine("SAVEDRAWING|" + (saveDrawing ? "1" : "0"));
                writer.WriteLine("EXPORTPDF|" + (exportPdf ? "1" : "0"));
                writer.WriteLine("PAUSE|" + pauseMilliseconds);
                foreach (string source in sources) writer.WriteLine("SOURCE|" + Encode(source));
            }
        }

        private static void ApplyDrawingWorkerResult(TextBlock status, string label, int exitCode, string stdout, string stderr)
        {
            string line = (stdout ?? string.Empty)
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .LastOrDefault(value => value.StartsWith("RESULT|", StringComparison.OrdinalIgnoreCase));

            if (exitCode == 0 && !string.IsNullOrWhiteSpace(line) && line.StartsWith("RESULT|OK|", StringComparison.OrdinalIgnoreCase))
            {
                string[] parts = line.Split('|');
                string folder = parts.Length > 2 ? parts[2] : string.Empty;
                SetBomStatus(status,
                    "[OK] " + label + " hoàn tất. Thành công=" + FindWorkerValue(parts, "SUCCEEDED") +
                    ", lỗi=" + FindWorkerValue(parts, "FAILED") +
                    ", thời gian=" + FindWorkerValue(parts, "MS") + " ms. Thư mục=" + folder +
                    ". Log=" + FindWorkerValue(parts, "LOG"),
                    52, 211, 153);
                return;
            }

            string error = string.Empty;
            if (!string.IsNullOrWhiteSpace(line) && line.StartsWith("RESULT|ERROR|", StringComparison.OrdinalIgnoreCase))
                error = line.Substring("RESULT|ERROR|".Length);
            if (string.IsNullOrWhiteSpace(error)) error = (stderr ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(error)) error = "Drawing worker kết thúc với mã " + exitCode + ".";
            SetBomStatus(status, "[LỖI] " + label + ": " + error, 248, 113, 113);
        }
    }
}
