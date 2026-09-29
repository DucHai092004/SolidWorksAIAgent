using System.Collections.Generic;

namespace SwMateAI.Core.DrawingUnderstanding
{
    public class DrawingSourceEvidence
    {
        public string SourcePath { get; set; } = string.Empty;
        public int? PageNumber { get; set; }
        public string ExtractionMethod { get; set; } = string.Empty;
        public string RawText { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public bool RequiresReview { get; set; }
    }

    public class DrawingSourceEvidenceResult
    {
        public DrawingVisionReadinessResult Readiness { get; set; } = new DrawingVisionReadinessResult();
        public List<DrawingSourceEvidence> Evidence { get; } = new List<DrawingSourceEvidence>();
        public List<string> Errors { get; } = new List<string>();
    }

    public interface IDrawingSourceEvidenceProvider
    {
        bool CanAnalyze(string path);
        DrawingSourceEvidenceResult Analyze(string path);
    }
}
