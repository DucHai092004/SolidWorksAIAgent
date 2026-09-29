using System;
using System.Collections.Generic;
using SwMateAI.Core.DrawingUnderstanding;

namespace SwMateAI.Core.Tools.Drawing
{
    public sealed class ReadDrawingDocumentTool : ISwTool
    {
        private readonly IDrawingVisionProvider _visionProvider;

        public ReadDrawingDocumentTool(IDrawingVisionProvider visionProvider = null)
        {
            _visionProvider = visionProvider;
        }

        public string Name => "ReadDrawingDocument";

        public string Description =>
            "Reads an external drawing PDF. Native PDF text is extracted directly; pages without enough native text are marked for Vision/OCR and review.";

        public ToolResult Execute(Dictionary<string, object> parameters)
        {
            string path = GetString(parameters, "Path");
            if (string.IsNullOrWhiteSpace(path)) path = GetString(parameters, "FilePath");
            if (string.IsNullOrWhiteSpace(path))
                return ToolResult.Error("ReadDrawingDocument requires a PDF Path parameter.");

            try
            {
                var result = new PdfDrawingVisionReader(_visionProvider).Read(path);
                return ToolResult.Success(result);
            }
            catch (Exception ex)
            {
                return ToolResult.Error("ReadDrawingDocument failed: " + ex.Message);
            }
        }

        private static string GetString(Dictionary<string, object> parameters, string key)
        {
            if (parameters == null || !parameters.TryGetValue(key, out var value) || value == null)
                return string.Empty;
            return Convert.ToString(value) ?? string.Empty;
        }
    }
}
