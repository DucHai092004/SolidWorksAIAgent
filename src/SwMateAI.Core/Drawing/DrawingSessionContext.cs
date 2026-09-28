namespace SwMateAI.Core.Drawing
{
    public class DrawingSessionContext
    {
        public string SourceModelPath { get; set; } = string.Empty;
        public string SourceModelTitle { get; set; } = string.Empty;
        public string DrawingTitle { get; set; } = string.Empty;
        public string DrawingTemplatePath { get; set; } = string.Empty;
        public string LastPdfPath { get; set; } = string.Empty;
    }
}
