using System;
using System.Collections.Generic;
using SwMateAI.Core.DrawingUnderstanding;

namespace SwMateAI.Core.Tools.Drawing
{
    public sealed class AnalyzeDrawingSourceTool : ISwTool
    {
        private readonly DrawingSourceEvidencePipeline _pipeline;

        public AnalyzeDrawingSourceTool(DrawingSourceEvidencePipeline pipeline = null)
        {
            _pipeline = pipeline ?? new DrawingSourceEvidencePipeline(new IDrawingSourceEvidenceProvider[]
            {
                new PdfTextDrawingEvidenceProvider(),
                new PdfRasterDrawingEvidenceProvider(),
                new TesseractCliDrawingEvidenceProvider()
            });
        }

        public string Name => "AnalyzeDrawingSource";
        public string Description =>
            "Analyzes an external Drawing/PDF/image source for native/text/raster readiness and returns available evidence. Text PDFs use native extraction; raster images and extractable image-only PDF pages use Tesseract OCR when available. Weak or unavailable evidence remains review-gated.";

        public ToolResult Execute(Dictionary<string, object> parameters)
        {
            try
            {
                string path = string.Empty;
                if (parameters != null && parameters.TryGetValue("Path", out var raw))
                    path = Convert.ToString(raw) ?? string.Empty;

                if (string.IsNullOrWhiteSpace(path))
                    return ToolResult.Error("AnalyzeDrawingSource requires a Path parameter.");

                var result = _pipeline.Analyze(path);
                return ToolResult.Success(result);
            }
            catch (Exception ex)
            {
                return ToolResult.Error("AnalyzeDrawingSource failed: " + ex.Message);
            }
        }
    }
}
