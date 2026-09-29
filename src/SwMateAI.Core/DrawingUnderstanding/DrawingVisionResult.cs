using System.Collections.Generic;

namespace SwMateAI.Core.DrawingUnderstanding
{
    public sealed class DrawingVisionResult
    {
        public string SourceFile { get; set; } = string.Empty;
        public int PageCount { get; set; }
        public int NativeTextPageCount { get; set; }
        public int VisionRequiredPageCount { get; set; }
        public bool RequiresVision => VisionRequiredPageCount > 0;
        public bool RequiresReview { get; set; }
        public List<DrawingVisionPageInfo> Pages { get; } = new List<DrawingVisionPageInfo>();
        public List<string> Warnings { get; } = new List<string>();
    }

    public sealed class DrawingVisionPageInfo
    {
        public int PageNumber { get; set; }
        public string ExtractionMethod { get; set; } = string.Empty;
        public double ExtractionConfidence { get; set; }
        public string NativeText { get; set; } = string.Empty;
        public bool RequiresVision { get; set; }
        public bool RequiresReview { get; set; }
        public string ReviewReason { get; set; } = string.Empty;
    }
}
