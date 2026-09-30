using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Media;
using SwMateAI.Core.Agent;
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
                new Dictionary<string, object>
                {
                    ["Mode"] = "LegacyFlat",
                    ["RespectChildDisplay"] = true,
                    ["ExportExcel"] = false,
                    ["ExportCsv"] = false
                });

            if (!preflight.IsSuccess)
            {
                SetBomStatus(status, "[LỖI] Không thể đọc BOM: " + preflight.ErrorMessage, 248, 113, 113);
                return;
            }

            string baseDirectory = Path.GetDirectoryName(typeof(TaskPaneControl).Assembly.Location) ?? string.Empty;
            string workerPath = Path.Combine(baseDirectory, "SwMateAI.BomWorker.exe");
            if (!File.Exists(workerPath))
            {
                SetBomStatus(status, "[LỖI] Thiếu SwMateAI.BomWorker.exe. Hãy build lại AddIn.", 248, 113, 113);
                return;
            }

            string folder;
            try
            {
                folder = PrepareOutputFolder(outputText);
            }
            catch (Exception ex)
            {
                SetBomStatus(status, "[LỖI] Thư mục đầu ra: " + ex.Message, 248, 113, 113);
                return;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = workerPath,
                WorkingDirectory = baseDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            if (!string.IsNullOrWhiteSpace(folder))
                startInfo.Arguments = "--output " + QuoteArgument(folder);

            var process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };

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
                    "[RUN] Đang xuất BOM Excel có ảnh ở tiến trình riêng. SolidWorks vẫn có thể tiếp tục sử dụng.",
                    125, 211, 252);
                process.Start();
            }
            catch (Exception ex)
            {
                _bomWorkerRunning = false;
                try { process.Dispose(); } catch { }
                SetBomStatus(status, "[LỖI] Không khởi động được BOM worker: " + ex.Message, 248, 113, 113);
            }
        }

        private static void ApplyBomWorkerResult(
            TextBlock status,
            int exitCode,
            string stdout,
            string stderr)
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
                string milliseconds = FindWorkerValue(parts, "MS");
                SetBomStatus(
                    status,
                    "[OK] BOM Excel hoàn tất. Dòng=" + items + ", ảnh=" + images +
                    ", thời gian=" + milliseconds + " ms. File=" + path,
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

        private static void SetBomStatus(
            TextBlock status,
            string text,
            byte red,
            byte green,
            byte blue)
        {
            status.Text = text;
            status.Foreground = new SolidColorBrush(Color.FromRgb(red, green, blue));
        }
    }
}
