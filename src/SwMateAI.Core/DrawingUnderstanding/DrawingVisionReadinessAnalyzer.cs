using System;
using System.IO;
using UglyToad.PdfPig;

namespace SwMateAI.Core.DrawingUnderstanding
{
    public sealed class DrawingVisionReadinessAnalyzer
    {
        private const int MinimumTextCharactersPerPage = 10;

        public DrawingVisionReadinessResult Analyze(string path)
        {
            var result = new DrawingVisionReadinessResult
            {
                SourcePath = path ?? string.Empty
            };

            if (string.IsNullOrWhiteSpace(path))
                return Review(result, DrawingVisionSourceKind.Unknown, false, "Drawing source path is empty.");

            string extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension == ".slddrw")
            {
                result.SourceKind = DrawingVisionSourceKind.NativeDrawing;
                result.CanUseNativeSemanticReader = true;
                result.Reason = "Native SOLIDWORKS Drawing should use the semantic SLDDRW reader first.";
                return result;
            }

            if (IsRasterImage(extension))
                return Review(result, DrawingVisionSourceKind.RasterImage, true,
                    "Raster drawing image requires Vision/OCR evidence before semantic values can be trusted.");

            if (extension != ".pdf")
                return Review(result, DrawingVisionSourceKind.Unsupported, false,
                    "Unsupported drawing source; no semantic values should be inferred automatically.");

            if (!File.Exists(path))
                return Review(result, DrawingVisionSourceKind.Unknown, false, "Drawing source file does not exist.");

            try
            {
                using (var pdf = PdfDocument.Open(path))
                {
                    foreach (var page in pdf.GetPages())
                    {
                        result.PageCount++;
                        string text = page.Text ?? string.Empty;
                        int textLength = text.Trim().Length;
                        result.ExtractedTextCharacterCount += textLength;
                        if (textLength < MinimumTextCharactersPerPage) result.RasterPageCount++;
                        else result.TextPageCount++;
                    }
                }
            }
            catch (Exception ex)
            {
                return Review(result, DrawingVisionSourceKind.Unknown, false,
                    "PDF could not be inspected safely: " + ex.Message);
            }

            if (result.RasterPageCount == 0)
            {
                result.SourceKind = DrawingVisionSourceKind.TextPdf;
                result.Reason = "PDF contains extractable text on every page; Vision is not required for text access.";
                return result;
            }

            result.SourceKind = result.TextPageCount == 0
                ? DrawingVisionSourceKind.RasterPdf
                : DrawingVisionSourceKind.MixedPdf;
            result.RequiresVision = true;
            result.RequiresReview = true;
            result.Reason = "One or more PDF pages lack sufficient extractable text and require Vision/OCR evidence.";
            return result;
        }

        private static DrawingVisionReadinessResult Review(
            DrawingVisionReadinessResult result,
            DrawingVisionSourceKind kind,
            bool requiresVision,
            string reason)
        {
            result.SourceKind = kind;
            result.RequiresVision = requiresVision;
            result.RequiresReview = true;
            result.Reason = reason ?? string.Empty;
            return result;
        }

        private static bool IsRasterImage(string extension)
        {
            return extension == ".png" || extension == ".jpg" || extension == ".jpeg"
                || extension == ".bmp" || extension == ".tif" || extension == ".tiff";
        }
    }
}
