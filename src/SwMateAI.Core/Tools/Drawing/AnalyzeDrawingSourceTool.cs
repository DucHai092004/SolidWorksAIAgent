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
                new TesseractCliDrawingEvidenceProvider()
            });
        }

        public string Name => "AnalyzeDrawingSource";
        public string Description =>
            "Analyzes an external Drawing/PDF/image source for native/text/raster readiness and returns available evidence. Raster images use Tesseract OCR when available and remain review-gated when evidence is weak or unavailable.";

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
