using System;
using System.IO;
using UglyToad.PdfPig;

namespace SwMateAI.Core.DrawingUnderstanding
{
    public sealed class PdfDrawingVisionReader
    {
        private const int NativeTextThreshold = 10;
        private readonly IDrawingVisionProvider _visionProvider;

        public PdfDrawingVisionReader(IDrawingVisionProvider visionProvider = null)
        {
            _visionProvider = visionProvider;
        }

        public DrawingVisionResult Read(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("PDF path is required.", nameof(path));
            if (!File.Exists(path))
                throw new FileNotFoundException("Drawing PDF was not found.", path);
            if (!Path.GetExtension(path).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException("Only PDF drawing documents are supported by this reader.");

            var result = new DrawingVisionResult { SourceFile = path };

            using (var pdf = PdfDocument.Open(path))
            {
                foreach (var page in pdf.GetPages())
                {
                    result.PageCount++;
                    string nativeText = (page.Text ?? string.Empty).Trim();
                    var pageInfo = new DrawingVisionPageInfo
                    {
                        PageNumber = result.PageCount
                    };

                    if (nativeText.Length >= NativeTextThreshold)
                    {
                        result.NativeTextPageCount++;
                        pageInfo.NativeText = nativeText;
                        pageInfo.ExtractionMethod = "PDF_TEXT";
                        pageInfo.ExtractionConfidence = 0.99;
                    }
                    else
                    {
                        result.VisionRequiredPageCount++;
                        pageInfo.RequiresVision = true;
                        ApplyVision(path, pageInfo, result);
                    }

                    if (pageInfo.RequiresReview) result.RequiresReview = true;
                    result.Pages.Add(pageInfo);
                }
            }

            return result;
        }

        private void ApplyVision(string path, DrawingVisionPageInfo page, DrawingVisionResult result)
        {
            if (_visionProvider == null)
            {
                page.ExtractionMethod = "VISION_REQUIRED";
                page.RequiresReview = true;
                page.ReviewReason = "Page has insufficient native PDF text and no Vision/OCR provider is configured.";
                result.Warnings.Add($"Page {page.PageNumber}: Vision/OCR required.");
                return;
            }

            DrawingVisionProviderResult vision;
            try
            {
                vision = _visionProvider.AnalyzePage(path, page.PageNumber);
            }
            catch (Exception ex)
            {
                page.ExtractionMethod = "VISION_ERROR";
                page.RequiresReview = true;
                page.ReviewReason = "Vision/OCR provider failed: " + ex.Message;
                result.Warnings.Add($"Page {page.PageNumber}: Vision/OCR provider failed.");
                return;
            }

            if (vision == null || !vision.IsSuccess)
            {
                page.ExtractionMethod = vision?.Method ?? "VISION_FAILED";
                page.ExtractionConfidence = vision?.Confidence ?? 0d;
                page.RequiresReview = true;
                page.ReviewReason = vision?.ReviewReason ?? "Vision/OCR provider did not return a usable result.";
                result.Warnings.Add($"Page {page.PageNumber}: Vision/OCR result requires review.");
                return;
            }

            page.NativeText = vision.Text ?? string.Empty;
            page.ExtractionMethod = string.IsNullOrWhiteSpace(vision.Method) ? "VISION_PROVIDER" : vision.Method;
            page.ExtractionConfidence = vision.Confidence;
            page.RequiresReview = vision.RequiresReview;
            page.ReviewReason = vision.ReviewReason ?? string.Empty;
        }
    }
}
