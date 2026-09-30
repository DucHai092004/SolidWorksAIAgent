using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using SwMateAI.Core.Agent;
using SwMateAI.Core.BOM;
using SwMateAI.Core.Tools;

namespace SwMateAI.UI
{
    public partial class TaskPaneControl
    {
        private bool _bomWorkerRunning;

        private void StartBomExcelWorker(AgentCore agent, string outputText, TextBlock status)
        {
            if (_bomWorkerRunning)
            {
                SetBomStatus(status, "[RUN] BOM có ảnh đang được xuất. Vui lòng chờ hoàn tất.", 251, 191, 36);
                return;
            }
            if (!TryBeginExtractionTask("BOM Excel + ảnh", status)) return;

            bool handedOffToWorker = false;
            string manifest = string.Empty;
            try
            {
                ToolResult preflight = agent.ExecuteTool(
                    "CreateBOM",
                    new System.Collections.Generic.Dictionary<string, object>
                    {
                        ["Mode"] = "LegacyFlat",
                        ["RespectChildDisplay"] = true,
                        ["ExportExcel"] = false,
                        ["ExportCsv"] = false,
                        ["CaptureImages"] = false
                    });

                if (!preflight.IsSuccess || !(preflight.Data is BomResult bom))
                {
                    SetBomStatus(status,
                        "[LỖI] Không thể đọc BOM từ Assembly đang mở. " + (preflight.ErrorMessage ?? string.Empty),
                        248, 113, 113);
                    return;
                }
                if (bom.Items.Count == 0)
                {
                    SetBomStatus(status, "[LỖI] BOM không có dòng dữ liệu để xuất.", 248, 113, 113);
                    return;
                }

                AgentContext context = agent.ObserveContext();
                if (string.IsNullOrWhiteSpace(context.DocumentPath) || !File.Exists(context.DocumentPath))
                {
                    SetBomStatus(status, "[LỖI] Hãy lưu Assembly trước khi xuất BOM có ảnh.", 248, 113, 113);
                    return;
                }

                string baseDirectory = Path.GetDirectoryName(typeof(TaskPaneControl).Assembly.Location) ?? string.Empty;
                string workerDirectory = Path.Combine(baseDirectory, "BomWorker");
                string workerPath = Path.Combine(workerDirectory, "SwMateAI.BomWorker.exe");
                if (!File.Exists(workerPath))
                {
                    SetBomStatus(status, "[LỖI] Thiếu BomWorker\\SwMateAI.BomWorker.exe.", 248, 113, 113);
                    return;
                }

                string folder;
                try { folder = PrepareOutputFolder(outputText); }
                catch (Exception ex)
                {
                    SetBomStatus(status, "[LỖI] Thư mục đầu ra: " + ex.Message, 248, 113, 113);
                    return;
                }

                manifest = Path.Combine(Path.GetTempPath(), "SW-MATE_AI_BOM_" + Guid.NewGuid().ToString("N") + ".txt");
                try { WriteBomManifest(manifest, context.DocumentPath, bom); }
                catch (Exception ex)
                {
                    SetBomStatus(status, "[LỖI] Không tạo được dữ liệu BOM tạm: " + ex.Message, 248, 113, 113);
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
                        Arguments = "--manifest " + QuoteArgument(manifest) +
                                    (string.IsNullOrWhiteSpace(folder) ? string.Empty : " --output " + QuoteArgument(folder))
                    },
                    EnableRaisingEvents = true
                };

                process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data == null) return;
                    lock (stdout) stdout.AppendLine(e.Data);

                    if (e.Data.StartsWith("PROGRESS|INSTANCE|", StringComparison.OrdinalIgnoreCase))
                    {
                        UpdateExtractionProgress(0, 0, "BOM + ảnh — đang chuẩn bị SolidWorks nền...");
                        return;
                    }

                    if (!e.Data.StartsWith("PROGRESS|IMAGE|", StringComparison.OrdinalIgnoreCase)) return;
                    string[] parts = e.Data.Split('|');
                    string progress = parts.Length > 2 ? parts[2] : string.Empty;
                    string ok = parts.Length > 3 ? parts[3].Replace("OK=", string.Empty) : "?";
                    string skip = parts.Length > 4 ? parts[4].Replace("SKIP=", string.Empty) : "?";
                    ParseProgress(progress, out double current, out double total);

                    Dispatcher.BeginInvoke(new Action(delegate
                    {
                        SetBomStatus(status,
                            "[RUN] BOM + ảnh — " + progress + " — ảnh OK=" + ok + ", bỏ qua=" + skip,
                            125, 211, 252);
                        UpdateExtractionProgress(current, total,
                            "BOM + ảnh — đã xử lý " + progress + ", ảnh OK=" + ok + ", bỏ qua=" + skip + ".");
                    }));
                };

                process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data == null) return;
                    lock (stderr) stderr.AppendLine(e.Data);
                };

                string manifestToDelete = manifest;
                process.Exited += delegate
                {
                    int exitCode = -1;
                    try { process.WaitForExit(); exitCode = process.ExitCode; } catch { }
                    string outText; string errText;
                    lock (stdout) outText = stdout.ToString();
                    lock (stderr) errText = stderr.ToString();
                    try { if (File.Exists(manifestToDelete)) File.Delete(manifestToDelete); } catch { }

                    Dispatcher.BeginInvoke(new Action(delegate
                    {
                        _bomWorkerRunning = false;
                        ApplyBomWorkerResult(status, exitCode, outText, errText);
                        bool success = exitCode == 0 && outText.IndexOf("RESULT|OK|", StringComparison.OrdinalIgnoreCase) >= 0;
                        EndExtractionTask(success
                            ? "BOM Excel + ảnh — hoàn tất."
                            : "BOM Excel + ảnh — kết thúc có lỗi.");
                        try { process.Dispose(); } catch { }
                    }));
                };

                _bomWorkerRunning = true;
                SetBomStatus(status,
                    "[RUN] Đã đọc " + bom.Items.Count + " dòng BOM. Đang khởi động worker ảnh theo chế độ ổn định...",
                    125, 211, 252);
                UpdateExtractionProgress(0, bom.Items.Count, "BOM + ảnh — chuẩn bị " + bom.Items.Count + " dòng.");
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                handedOffToWorker = true;
            }
            catch (Exception ex)
            {
                _bomWorkerRunning = false;
                try { if (!string.IsNullOrWhiteSpace(manifest) && File.Exists(manifest)) File.Delete(manifest); } catch { }
                SetBomStatus(status, "[LỖI] Không khởi động được BOM worker: " + ex.Message, 248, 113, 113);
            }
            finally
            {
                if (!handedOffToWorker)
                {
                    _bomWorkerRunning = false;
                    EndExtractionTask("BOM Excel + ảnh — không chạy.");
                }
            }
        }

        private static void WriteBomManifest(string path, string assemblyPath, BomResult bom)
        {
            using (var writer = new StreamWriter(path, false, new UTF8Encoding(false)))
            {
                writer.WriteLine("VERSION|1");
                writer.WriteLine("ASSEMBLY|" + Encode(assemblyPath));
                foreach (BomItem item in bom.Items)
                {
                    writer.WriteLine(string.Join("|", new[]
                    {
                        "ITEM",
                        item.ItemNumber.ToString(),
                        item.Level.ToString(),
                        item.Quantity.ToString(),
                        Encode(item.PartNumber), Encode(item.Description), Encode(item.Material),
                        Encode(item.ComponentType), Encode(item.Configuration), Encode(item.SourcePath),
                        Encode(item.RepresentativeComponentName),
                        item.IsVirtual ? "1" : "0",
                        item.IsLoaded ? "1" : "0"
                    }));
                }
            }
        }

        private static string Encode(string value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));
        }

        private static void ApplyBomWorkerResult(TextBlock status, int exitCode, string stdout, string stderr)
        {
            string line = (stdout ?? string.Empty)
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .LastOrDefault(value => value.StartsWith("RESULT|", StringComparison.OrdinalIgnoreCase));

            if (exitCode == 0 && !string.IsNullOrWhiteSpace(line) && line.StartsWith("RESULT|OK|", StringComparison.OrdinalIgnoreCase))
            {
                string[] parts = line.Split('|');
                string path = parts.Length > 2 ? parts[2] : string.Empty;
                SetBomStatus(status,
                    "[OK] BOM Excel hoàn tất. Dòng=" + FindWorkerValue(parts, "ITEMS") +
                    ", ảnh=" + FindWorkerValue(parts, "IMAGES") +
                    ", bỏ qua=" + FindWorkerValue(parts, "SKIPPED") +
                    ", thời gian=" + FindWorkerValue(parts, "MS") + " ms. File=" + path,
                    52, 211, 153);
                return;
            }

            string error = string.Empty;
            if (!string.IsNullOrWhiteSpace(line) && line.StartsWith("RESULT|ERROR|", StringComparison.OrdinalIgnoreCase))
                error = line.Substring("RESULT|ERROR|".Length);
            if (string.IsNullOrWhiteSpace(error)) error = (stderr ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(error)) error = "BOM worker kết thúc với mã " + exitCode + ".";
            SetBomStatus(status, "[LỖI] " + error, 248, 113, 113);
        }

        private static string FindWorkerValue(string[] parts, string key)
        {
            string prefix = key + "=";
            foreach (string part in parts ?? Array.Empty<string>())
                if (part.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return part.Substring(prefix.Length);
            return "?";
        }

        private static void ParseProgress(string value, out double current, out double total)
        {
            current = 0;
            total = 0;
            string[] parts = (value ?? string.Empty).Split('/');
            if (parts.Length != 2) return;
            double.TryParse(parts[0], out current);
            double.TryParse(parts[1], out total);
        }

        private static string QuoteArgument(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }

        private static void SetBomStatus(TextBlock status, string text, byte red, byte green, byte blue)
        {
            status.Text = text;
            status.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(red, green, blue));
        }
    }
}
