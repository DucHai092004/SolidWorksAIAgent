using System;
using System.IO;
using SwMateAI.Core.DrawingUnderstanding;

namespace SwMateAI.Core.Tests;

[TestClass]
public class DrawingVisionReadinessTests
{
    [TestMethod]
    public void NativeDrawing_UsesSemanticReaderWithoutVision()
    {
        var result = new DrawingVisionReadinessAnalyzer().Analyze(@"C:\Temp\Part01.SLDDRW");

        Assert.AreEqual(DrawingVisionSourceKind.NativeDrawing, result.SourceKind);
        Assert.IsTrue(result.CanUseNativeSemanticReader);
        Assert.IsFalse(result.RequiresVision);
        Assert.IsFalse(result.RequiresReview);
    }

    [TestMethod]
    public void RasterImage_RequiresVisionAndReview()
    {
        var result = new DrawingVisionReadinessAnalyzer().Analyze(@"C:\Temp\drawing.png");

        Assert.AreEqual(DrawingVisionSourceKind.RasterImage, result.SourceKind);
        Assert.IsTrue(result.RequiresVision);
        Assert.IsTrue(result.RequiresReview);
        Assert.IsFalse(result.CanUseNativeSemanticReader);
    }

    [TestMethod]
    public void UnsupportedSource_RequiresReviewWithoutGuessingVisionCapability()
    {
        var result = new DrawingVisionReadinessAnalyzer().Analyze(@"C:\Temp\drawing.dwg");

        Assert.AreEqual(DrawingVisionSourceKind.Unsupported, result.SourceKind);
        Assert.IsFalse(result.RequiresVision);
        Assert.IsTrue(result.RequiresReview);
    }

    [TestMethod]
    public void MissingPdf_RequiresReviewAndDoesNotPretendItWasInspected()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".pdf");
        var result = new DrawingVisionReadinessAnalyzer().Analyze(path);

        Assert.AreEqual(DrawingVisionSourceKind.Unknown, result.SourceKind);
        Assert.IsTrue(result.RequiresReview);
        Assert.AreEqual(0, result.PageCount);
        Assert.AreEqual(0, result.TextPageCount);
        Assert.AreEqual(0, result.RasterPageCount);
    }
}
