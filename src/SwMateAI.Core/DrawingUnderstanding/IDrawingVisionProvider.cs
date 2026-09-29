namespace SwMateAI.Core.DrawingUnderstanding
{
    public interface IDrawingVisionProvider
    {
        DrawingVisionProviderResult AnalyzePage(string sourceFile, int pageNumber);
    }

    public sealed class DrawingVisionProviderResult
    {
        public bool IsSuccess { get; set; }
        public string Text { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public string Method { get; set; } = string.Empty;
        public bool RequiresReview { get; set; }
        public string ReviewReason { get; set; } = string.Empty;
    }
}
