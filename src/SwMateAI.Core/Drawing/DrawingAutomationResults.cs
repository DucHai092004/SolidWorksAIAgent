using System.Collections.Generic;

namespace SwMateAI.Core.Drawing
{
    public class DrawingAnnotationResult
    {
        public string ViewName { get; set; } = string.Empty;
        public int InsertedCount { get; set; }
    }

    public class DrawingBomResult
    {
        public string ViewName { get; set; } = string.Empty;
        public string TemplatePath { get; set; } = string.Empty;
        public string Configuration { get; set; } = string.Empty;
    }

    public class DrawingBalloonResult
    {
        public string ViewName { get; set; } = string.Empty;
        public int BalloonCount { get; set; }
    }

    public class DrawingTitleBlockResult
    {
        public int PropertiesWritten { get; set; }
        public List<string> PropertyNames { get; } = new List<string>();
    }

    public class DrawingExportResult
    {
        public string Format { get; set; } = string.Empty;
        public string OutputPath { get; set; } = string.Empty;
        public int Errors { get; set; }
        public int Warnings { get; set; }
    }
}
