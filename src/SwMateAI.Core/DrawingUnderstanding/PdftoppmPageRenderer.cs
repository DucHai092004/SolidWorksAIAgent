using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace SwMateAI.Core.DrawingUnderstanding
{
    public sealed class PdfPageRenderResult
    {
        public bool IsSuccess { get; set; }
        public byte[] ImageBytes { get; set; } = Array.Empty<byte>();
        public string ImageExtension { get; set; } = ".png";
        public string Error { get; set; } = string.Empty;
    }

    public interface IPdfPageRenderer
    {
        PdfPageRenderResult Render(string pdfPath, int pageNumber);
    }

    public sealed class PdftoppmPageRenderer : IPdfPageRenderer
    {
        private readonly string _configuredExecutable;
        private readonly int _timeoutMs;
        private readonly int _dpi;

        public PdftoppmPageRenderer(string executablePath = null, int timeoutMs = 30000, int dpi = 300)
        {
            _configuredExecutable = executablePath;
            _timeoutMs = timeoutMs > 0 ? timeoutMs : 30000;
            _dpi = dpi > 0 ? dpi : 300;
        }

        public PdfPageRenderResult Render(string pdfPath, int pageNumber)
        {
            if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath))
                return new PdfPageRenderResult { Error = "PDF file was not found." };
            if (pageNumber <= 0)
                return new PdfPageRenderResult { Error = "PDF page number must be greater than zero." };

            string executable = ResolveExecutable();
            string tempRoot = Path.Combine(Path.GetTempPath(), "swmate_pdf_page_" + Guid.NewGuid().ToString("N"));
            string outputPath = tempRoot + "-" + pageNumber + ".png";

            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = "-f " + pageNumber + " -singlefile -r " + _dpi + " -png " + Quote(pdfPath) + " " + Quote(tempRoot),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            try
            {
                using (var process = Process.Start(startInfo))
                {
                    if (process == null)
                        return new PdfPageRenderResult { Error = "Could not start pdftoppm." };

                    process.StandardOutput.ReadToEnd();
                    string stderr = process.StandardError.ReadToEnd();
                    if (!process.WaitForExit(_timeoutMs))
                    {
                        try { process.Kill(); } catch { }
                        return new PdfPageRenderResult { Error = "pdftoppm timed out." };
                    }

                    if (process.ExitCode != 0)
                    {
                        return new PdfPageRenderResult
                        {
                            Error = "pdftoppm exited with code " + process.ExitCode + ": " + (stderr ?? string.Empty).Trim()
                        };
                    }

                    if (!File.Exists(outputPath))
                        return new PdfPageRenderResult { Error = "pdftoppm completed but did not create the expected PNG page." };

                    return new PdfPageRenderResult
                    {
                        IsSuccess = true,
                        ImageBytes = File.ReadAllBytes(outputPath),
                        ImageExtension = ".png"
                    };
                }
            }
            catch (Exception ex)
            {
                return new PdfPageRenderResult { Error = "Could not execute pdftoppm: " + ex.Message };
            }
            finally
            {
                try { if (File.Exists(outputPath)) File.Delete(outputPath); } catch { }
            }
        }

        private string ResolveExecutable()
        {
            if (!string.IsNullOrWhiteSpace(_configuredExecutable)) return _configuredExecutable;

            string env = Environment.GetEnvironmentVariable("SWMATE_PDFTOPPM_PATH");
            if (!string.IsNullOrWhiteSpace(env)) return env;

            return "pdftoppm.exe";
        }

        private static string Quote(string value) => "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
    }
}
