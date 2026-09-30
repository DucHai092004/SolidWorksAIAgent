namespace SwMateAI.Core.DrawingUnderstanding
{
    public enum DrawingVisionSourceKind
    {
        Unknown = 0,
        NativeDrawing,
        TextPdf,
        RasterPdf,
        MixedPdf,
        RasterImage,
        Unsupported
    }

    public class DrawingVisionReadinessResult
    {
        public string SourcePath { get; set; } = string.Empty;
        public DrawingVisionSourceKind SourceKind { get; set; }
        public int PageCount { get; set; }
        public int TextPageCount { get; set; }
        public int RasterPageCount { get; set; }
        public int ExtractedTextCharacterCount { get; set; }
        public bool RequiresVision { get; set; }
        public bool RequiresReview { get; set; }
        public bool CanUseNativeSemanticReader { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}
