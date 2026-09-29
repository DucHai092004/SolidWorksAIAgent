using System;
using System.IO;
using UglyToad.PdfPig;

namespace SwMateAI.Core.DrawingUnderstanding
{
    public sealed class PdfTextDrawingEvidenceProvider : IDrawingSourceEvidenceProvider
    {
        private const int MinimumTextCharactersPerPage = 10;
        private readonly DrawingVisionReadinessAnalyzer _readiness = new DrawingVisionReadinessAnalyzer();

        public bool CanAnalyze(string path)
        {
            return !string.IsNullOrWhiteSpace(path)
                && Path.GetExtension(path).Equals(".pdf", StringComparison.OrdinalIgnoreCase);
        }

        public DrawingSourceEvidenceResult Analyze(string path)
        {
            var result = new DrawingSourceEvidenceResult
            {
                Readiness = _readiness.Analyze(path)
            };

            if (!CanAnalyze(path))
            {
                result.Errors.Add("PDF text evidence provider only accepts .pdf sources.");
                return result;
            }

            if (!File.Exists(path))
            {
                result.Errors.Add("Drawing PDF does not exist.");
                return result;
            }

            try
            {
                using (var pdf = PdfDocument.Open(path))
                {
                    int pageNumber = 0;
                    foreach (var page in pdf.GetPages())
                    {
                        pageNumber++;
                        string text = page.Text ?? string.Empty;
                        if (text.Trim().Length < MinimumTextCharactersPerPage)
                            continue;

                        result.Evidence.Add(new DrawingSourceEvidence
                        {
                            SourcePath = path,
                            PageNumber = pageNumber,
                            ExtractionMethod = "PDF_TEXT",
                            RawText = text,
                            Confidence = 0.95,
                            RequiresReview = false
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add("PDF text extraction failed: " + ex.Message);
            }

            return result;
        }
    }
}
