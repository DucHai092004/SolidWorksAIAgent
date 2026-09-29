using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace SwMateAI.Core.DrawingUnderstanding
{
    public sealed class TesseractOcrResult
    {
        public bool IsSuccess { get; set; }
        public string Text { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public string Error { get; set; } = string.Empty;
    }

    public interface ITesseractOcrRunner
    {
        TesseractOcrResult Run(string imagePath, string language);
    }

    public sealed class TesseractCliDrawingEvidenceProvider : IDrawingSourceEvidenceProvider
    {
        private static readonly HashSet<string> RasterExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff"
        };

        private readonly ITesseractOcrRunner _runner;
        private readonly string _language;

        public TesseractCliDrawingEvidenceProvider(ITesseractOcrRunner runner = null, string language = "eng")
        {
            _runner = runner ?? new TesseractOcrRunner();
            _language = string.IsNullOrWhiteSpace(language) ? "eng" : language.Trim();
        }

        public bool CanAnalyze(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            return RasterExtensions.Contains(Path.GetExtension(path));
        }

        public DrawingSourceEvidenceResult Analyze(string path)
        {
            var result = new DrawingSourceEvidenceResult
            {
                Readiness = new DrawingVisionReadinessAnalyzer().Analyze(path)
            };

            if (!File.Exists(path))
            {
                result.Errors.Add("Raster drawing source was not found: " + path);
                return result;
            }

            TesseractOcrResult ocr;
            try
            {
                ocr = _runner.Run(path, _language);
            }
            catch (Exception ex)
            {
                result.Errors.Add("Tesseract OCR failed: " + ex.Message);
                return result;
            }

            if (ocr == null || !ocr.IsSuccess)
            {
                result.Errors.Add(string.IsNullOrWhiteSpace(ocr?.Error)
                    ? "Tesseract OCR did not return a usable result."
                    : ocr.Error);
                return result;
            }

            string text = (ocr.Text ?? string.Empty).Trim();
            if (text.Length == 0)
            {
                result.Errors.Add("Tesseract OCR completed but returned no text.");
                return result;
            }

            double confidence = Math.Max(0d, Math.Min(1d, ocr.Confidence));
            result.Evidence.Add(new DrawingSourceEvidence
            {
                SourcePath = path,
                ExtractionMethod = "TESSERACT_OCR",
                RawText = text,
                Confidence = confidence,
                RequiresReview = confidence < 0.85d
            });
            return result;
        }
    }

    public sealed class TesseractOcrRunner : ITesseractOcrRunner
    {
        private readonly string _configuredExecutable;
        private readonly int _timeoutMs;

        public TesseractOcrRunner(string executablePath = null, int timeoutMs = 30000)
        {
            _configuredExecutable = executablePath;
            _timeoutMs = timeoutMs > 0 ? timeoutMs : 30000;
        }

        public TesseractOcrResult Run(string imagePath, string language)
        {
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
                return new TesseractOcrResult { Error = "OCR image file was not found." };

            string executable = ResolveExecutable();
            if (string.IsNullOrWhiteSpace(executable))
            {
                return new TesseractOcrResult
                {
                    Error = "Tesseract executable was not found. Install Tesseract OCR or set SWMATE_TESSERACT_PATH."
                };
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = Quote(imagePath) + " stdout -l " + SafeLanguage(language) + " --psm 6 tsv",
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
                        return new TesseractOcrResult { Error = "Could not start Tesseract OCR process." };

                    string stdout = process.StandardOutput.ReadToEnd();
                    string stderr = process.StandardError.ReadToEnd();
                    if (!process.WaitForExit(_timeoutMs))
                    {
                        try { process.Kill(); } catch { }
                        return new TesseractOcrResult { Error = "Tesseract OCR timed out." };
                    }

                    if (process.ExitCode != 0)
                    {
                        return new TesseractOcrResult
                        {
                            Error = "Tesseract OCR exited with code " + process.ExitCode + ": " + (stderr ?? string.Empty).Trim()
                        };
                    }

                    return ParseTsv(stdout);
                }
            }
            catch (Exception ex)
            {
                return new TesseractOcrResult { Error = "Could not execute Tesseract OCR: " + ex.Message };
            }
        }

        private string ResolveExecutable()
        {
            if (!string.IsNullOrWhiteSpace(_configuredExecutable))
                return _configuredExecutable;

            string env = Environment.GetEnvironmentVariable("SWMATE_TESSERACT_PATH");
            if (!string.IsNullOrWhiteSpace(env)) return env;

            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string common = Path.Combine(programFiles, "Tesseract-OCR", "tesseract.exe");
            if (File.Exists(common)) return common;

            return "tesseract.exe";
        }

        private static TesseractOcrResult ParseTsv(string tsv)
        {
            var words = new List<string>();
            var confidences = new List<double>();
            string[] lines = (tsv ?? string.Empty).Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string line in lines.Skip(1))
            {
                string[] columns = line.Split('\t');
                if (columns.Length < 12) continue;
                string text = columns[11]?.Trim();
                if (string.IsNullOrWhiteSpace(text)) continue;

                words.Add(text);
                if (double.TryParse(columns[10], NumberStyles.Float, CultureInfo.InvariantCulture, out double confidence) && confidence >= 0)
                    confidences.Add(confidence / 100d);
            }

            if (words.Count == 0)
                return new TesseractOcrResult { Error = "Tesseract OCR returned no recognized words." };

            return new TesseractOcrResult
            {
                IsSuccess = true,
                Text = string.Join(" ", words),
                Confidence = confidences.Count > 0 ? confidences.Average() : 0d
            };
        }

        private static string Quote(string value) => "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";

        private static string SafeLanguage(string language)
        {
            string value = string.IsNullOrWhiteSpace(language) ? "eng" : language.Trim();
            foreach (char c in value)
                if (!(char.IsLetterOrDigit(c) || c == '+' || c == '_' || c == '-')) return "eng";
            return value;
        }
    }
}
