using System;
using System.Collections.Generic;
using SwMateAI.Core.DrawingUnderstanding;

namespace SwMateAI.Core.Tools.Drawing
{
    public sealed class AnalyzeDrawingSourceTool : ISwTool
    {
        public string Name => "AnalyzeDrawingSource";
        public string Description =>
            "Analyzes an external Drawing/PDF/image source for native/text/raster readiness and returns available evidence without guessing raster semantics.";

        public ToolResult Execute(Dictionary<string, object> parameters)
        {
            try
            {
                string path = string.Empty;
                if (parameters != null && parameters.TryGetValue("Path", out var raw))
                    path = Convert.ToString(raw) ?? string.Empty;

                if (string.IsNullOrWhiteSpace(path))
                    return ToolResult.Error("AnalyzeDrawingSource requires a Path parameter.");

                var result = new DrawingSourceEvidencePipeline().Analyze(path);
                return ToolResult.Success(result);
            }
            catch (Exception ex)
            {
                return ToolResult.Error("AnalyzeDrawingSource failed: " + ex.Message);
            }
        }
    }
}
