using System;
using SwMateAI.Core.DrawingUnderstanding;

namespace SwMateAI.Core.Tests;

[TestClass]
public class DrawingSourceEvidencePipelineTests
{
    [TestMethod]
    public void RasterImageWithoutVisionProvider_LeavesEvidenceEmptyAndRequiresReview()
    {
        var pipeline = new DrawingSourceEvidencePipeline();
        var result = pipeline.Analyze(@"C:\Temp\drawing.png");

        Assert.AreEqual(0, result.Evidence.Count);
        Assert.IsTrue(result.Readiness.RequiresVision);
        Assert.IsTrue(result.Readiness.RequiresReview);
    }

    [TestMethod]
    public void FutureVisionProvider_CanAddEvidenceWithoutClearingReviewGate()
    {
        var pipeline = new DrawingSourceEvidencePipeline(new IDrawingSourceEvidenceProvider[]
        {
            new FakeVisionProvider()
        });

        var result = pipeline.Analyze(@"C:\Temp\drawing.png");

        Assert.AreEqual(1, result.Evidence.Count);
        Assert.AreEqual("VISION_TEST", result.Evidence[0].ExtractionMethod);
        Assert.AreEqual(0.8d, result.Evidence[0].Confidence, 0.0001d);
        Assert.IsTrue(result.Evidence[0].RequiresReview);
        Assert.IsTrue(result.Readiness.RequiresReview);
    }

    [TestMethod]
    public void ProviderFailure_IsContainedAsError()
    {
        var pipeline = new DrawingSourceEvidencePipeline(new IDrawingSourceEvidenceProvider[]
        {
            new ThrowingProvider()
        });

        var result = pipeline.Analyze(@"C:\Temp\drawing.png");

        Assert.AreEqual(0, result.Evidence.Count);
        Assert.AreEqual(1, result.Errors.Count);
        StringAssert.Contains(result.Errors[0], "provider failed");
    }

    private sealed class FakeVisionProvider : IDrawingSourceEvidenceProvider
    {
        public bool CanAnalyze(string path) => true;

        public DrawingSourceEvidenceResult Analyze(string path)
        {
            var result = new DrawingSourceEvidenceResult();
            result.Evidence.Add(new DrawingSourceEvidence
            {
                SourcePath = path ?? string.Empty,
                PageNumber = 1,
                ExtractionMethod = "VISION_TEST",
                RawText = "DIMENSION 100",
                Confidence = 0.8d,
                RequiresReview = true
            });
            return result;
        }
    }

    private sealed class ThrowingProvider : IDrawingSourceEvidenceProvider
    {
        public bool CanAnalyze(string path) => true;
        public DrawingSourceEvidenceResult Analyze(string path) =>
            throw new InvalidOperationException("provider failed intentionally");
    }
}
