using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace SwMateAI.Core.DrawingUnderstanding
{
    public sealed class PdfRasterPage
    {
        public int PageNumber { get; set; }
        public bool HasNativeText { get; set; }
        public byte[] ImageBytes { get; set; } = Array.Empty<byte>();
        public string ImageExtension { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
    }

    public interface IPdfRasterPageExtractor
    {
        IReadOnlyList<PdfRasterPage> Extract(string pdfPath);
    }

    public sealed class PdfRasterDrawingEvidenceProvider : IDrawingSourceEvidenceProvider
    {
        private readonly IPdfRasterPageExtractor _extractor;
        private readonly ITesseractOcrRunner _runner;
        private readonly string _language;

        public PdfRasterDrawingEvidenceProvider(
            IPdfRasterPageExtractor extractor = null,
            ITesseractOcrRunner runner = null,
            string language = "eng")
        {
            _extractor = extractor ?? new PdfPigRasterPageExtractor();
            _runner = runner ?? new TesseractOcrRunner();
            _language = string.IsNullOrWhiteSpace(language) ? "eng" : language.Trim();
        }

        public bool CanAnalyze(string path) =>
            !string.IsNullOrWhiteSpace(path) &&
            Path.GetExtension(path).Equals(".pdf", StringComparison.OrdinalIgnoreCase);

        public DrawingSourceEvidenceResult Analyze(string path)
        {
            var result = new DrawingSourceEvidenceResult
            {
                Readiness = new DrawingVisionReadinessAnalyzer().Analyze(path)
            };

            if (!File.Exists(path))
            {
                result.Errors.Add("PDF drawing source was not found: " + path);
                return result;
            }

            IReadOnlyList<PdfRasterPage> pages;
            try
            {
                pages = _extractor.Extract(path) ?? Array.Empty<PdfRasterPage>();
            }
            catch (Exception ex)
            {
                result.Errors.Add("PDF raster extraction failed: " + ex.Message);
                return result;
            }

            foreach (var page in pages)
            {
                if (page == null || page.HasNativeText) continue;
                if (!string.IsNullOrWhiteSpace(page.Error))
                {
                    result.Errors.Add("Page " + page.PageNumber + ": " + page.Error);
                    continue;
                }
                if (page.ImageBytes == null || page.ImageBytes.Length == 0)
                {
                    result.Errors.Add("Page " + page.PageNumber + ": no extractable raster image was found.");
                    continue;
                }

                string extension = NormalizeExtension(page.ImageExtension);
                string tempPath = Path.Combine(Path.GetTempPath(),
                    "swmate_pdf_ocr_" + Guid.NewGuid().ToString("N") + extension);

                try
                {
                    File.WriteAllBytes(tempPath, page.ImageBytes);
                    TesseractOcrResult ocr = _runner.Run(tempPath, _language);
                    if (ocr == null || !ocr.IsSuccess)
                    {
                        result.Errors.Add("Page " + page.PageNumber + ": " +
                            (string.IsNullOrWhiteSpace(ocr?.Error) ? "OCR did not return a usable result." : ocr.Error));
                        continue;
                    }

                    string text = (ocr.Text ?? string.Empty).Trim();
                    if (text.Length == 0)
                    {
                        result.Errors.Add("Page " + page.PageNumber + ": OCR returned no text.");
                        continue;
                    }

                    double confidence = Math.Max(0d, Math.Min(1d, ocr.Confidence));
                    result.Evidence.Add(new DrawingSourceEvidence
                    {
                        SourcePath = path,
                        PageNumber = page.PageNumber,
                        ExtractionMethod = "PDF_RASTER_TESSERACT",
                        RawText = text,
                        Confidence = confidence,
                        RequiresReview = confidence < 0.85d
                    });
                }
                catch (Exception ex)
                {
                    result.Errors.Add("Page " + page.PageNumber + ": OCR processing failed: " + ex.Message);
                }
                finally
                {
                    try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
                }
            }

            return result;
        }

        private static string NormalizeExtension(string extension)
        {
            string value = (extension ?? string.Empty).Trim().ToLowerInvariant();
            if (value == ".jpg" || value == ".jpeg") return ".jpg";
            if (value == ".bmp") return ".bmp";
            if (value == ".tif" || value == ".tiff") return ".tif";
            return ".png";
        }
    }

    public sealed class PdfPigRasterPageExtractor : IPdfRasterPageExtractor
    {
        private const int NativeTextThreshold = 10;

        public IReadOnlyList<PdfRasterPage> Extract(string pdfPath)
        {
            var result = new List<PdfRasterPage>();
            using (var pdf = PdfDocument.Open(pdfPath))
            {
                int pageNumber = 0;
                foreach (var page in pdf.GetPages())
                {
                    pageNumber++;
                    string nativeText = (page.Text ?? string.Empty).Trim();
                    if (nativeText.Length >= NativeTextThreshold)
                    {
                        result.Add(new PdfRasterPage
                        {
                            PageNumber = pageNumber,
                            HasNativeText = true
                        });
                        continue;
                    }

                    IPdfImage image = null;
                    try
                    {
                        image = page.GetImages()
                            .Where(i => i != null && !i.IsImageMask)
                            .OrderByDescending(i => (long)i.WidthInSamples * i.HeightInSamples)
                            .FirstOrDefault();
                    }
                    catch (Exception ex)
                    {
                        result.Add(new PdfRasterPage
                        {
                            PageNumber = pageNumber,
                            Error = "Could not inspect PDF images: " + ex.Message
                        });
                        continue;
                    }

                    if (image == null)
                    {
                        result.Add(new PdfRasterPage
                        {
                            PageNumber = pageNumber,
                            Error = "Page has insufficient native text and no embedded raster image. Full page rendering is required."
                        });
                        continue;
                    }

                    if (image.TryGetPng(out byte[] png) && png != null && png.Length > 0)
                    {
                        result.Add(new PdfRasterPage
                        {
                            PageNumber = pageNumber,
                            ImageBytes = png,
                            ImageExtension = ".png"
                        });
                        continue;
                    }

                    byte[] raw = image.RawMemory.ToArray();
                    if (LooksLikeJpeg(raw))
                    {
                        result.Add(new PdfRasterPage
                        {
                            PageNumber = pageNumber,
                            ImageBytes = raw,
                            ImageExtension = ".jpg"
                        });
                        continue;
                    }

                    result.Add(new PdfRasterPage
                    {
                        PageNumber = pageNumber,
                        Error = "Embedded image could not be converted to PNG and is not a directly usable JPEG stream. Full page rendering is required."
                    });
                }
            }
            return result;
        }

        private static bool LooksLikeJpeg(byte[] bytes) =>
            bytes != null && bytes.Length >= 3 &&
            bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;
    }
}
