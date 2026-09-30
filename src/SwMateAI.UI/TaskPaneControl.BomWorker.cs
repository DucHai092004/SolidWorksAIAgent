using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows.Media;
using SwMateAI.Core.Agent;
using SwMateAI.Core.BOM;
using SwMateAI.Core.Tools;

namespace SwMateAI.UI
{
    public partial class TaskPaneControl
    {
        private bool _bomWorkerRunning;

        private void StartBomExcelWorker(
            AgentCore agent,
            string outputText,
            TextBlock status)
        {
            if (_bomWorkerRunning)
            {
                SetBomStatus(status, "[RUN] BOM có ảnh đang được xuất. Vui lòng không chạy thêm lần nữa.", 251, 191, 36);
                return;
            }

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
                SetBomStatus(status, "[LỖI] Không thể đọc BOM từ Assembly đang mở.", 248, 113, 113);
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

            string manifest = Path.Combine(
                Path.GetTempPath(),
                "SW-MATE_AI_BOM_" + Guid.NewGuid().ToString("N") + ".txt");

            try { WriteBomManifest(manifest, context.DocumentPath, bom); }
            catch (Exception ex)
            {
                SetBomStatus(status, "[LỖI] Không tạo được dữ liệu BOM tạm: " + ex.Message, 248, 113, 113);
                return;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = workerPath,
                WorkingDirectory = workerDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                Arguments = "--manifest " + QuoteArgument(manifest) +
                            (string.IsNullOrWhiteSpace(folder) ? string.Empty : " --output " + QuoteArgument(folder))
            };

            var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            process.Exited += delegate
            {
                string stdout = string.Empty;
                string stderr = string.Empty;
                int exitCode = -1;
                try
                {
                    stdout = process.StandardOutput.ReadToEnd();
                    stderr = process.StandardError.ReadToEnd();
                    exitCode = process.ExitCode;
                }
                catch { }
                finally
                {
                    try { if (File.Exists(manifest)) File.Delete(manifest); } catch { }
                }

                Dispatcher.BeginInvoke(new Action(delegate
                {
                    _bomWorkerRunning = false;
                    ApplyBomWorkerResult(status, exitCode, stdout, stderr);
                    try { process.Dispose(); } catch { }
                }));
            };

            try
            {
                _bomWorkerRunning = true;
                SetBomStatus(
                    status,
                    "[RUN] Đang tạo BOM có ảnh theo chế độ ổn định. Ảnh được xử lý tuần tự ở tiến trình riêng.",
                    125, 211, 252);
                process.Start();
            }
            catch (Exception ex)
            {
                _bomWorkerRunning = false;
                try { if (File.Exists(manifest)) File.Delete(manifest); } catch { }
                try { process.Dispose(); } catch { }
                SetBomStatus(status, "[LỖI] Không khởi động được BOM worker: " + ex.Message, 248, 113, 113);
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
                        Encode(item.PartNumber),
                        Encode(item.Description),
                        Encode(item.Material),
                        Encode(item.ComponentType),
                        Encode(item.Configuration),
                        Encode(item.SourcePath),
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
                string items = FindWorkerValue(parts, "ITEMS");
                string images = FindWorkerValue(parts, "IMAGES");
                string skipped = FindWorkerValue(parts, "SKIPPED");
                string milliseconds = FindWorkerValue(parts, "MS");
                SetBomStatus(
                    status,
                    "[OK] BOM Excel hoàn tất. Dòng=" + items + ", ảnh=" + images +
                    ", bỏ qua=" + skipped + ", thời gian=" + milliseconds + " ms. File=" + path,
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
                if (part.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return part.Substring(prefix.Length);
            return "?";
        }

        private static string QuoteArgument(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }

        private static void SetBomStatus(TextBlock status, string text, byte red, byte green, byte blue)
        {
            status.Text = text;
            status.Foreground = new SolidColorBrush(Color.FromRgb(red, green, blue));
        }
    }
}
