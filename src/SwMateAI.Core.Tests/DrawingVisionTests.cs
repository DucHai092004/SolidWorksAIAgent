using System;
using System.IO;
using SwMateAI.Core.DrawingUnderstanding;

namespace SwMateAI.Core.Tests;

[TestClass]
public class DrawingVisionTests
{
    [TestMethod]
    public void PdfWithNativeText_DoesNotRequireVision()
    {
        string path = Fixture("materials_text.pdf");
        var result = new PdfDrawingVisionReader().Read(path);

        Assert.IsTrue(result.PageCount > 0);
        Assert.AreEqual(result.PageCount, result.NativeTextPageCount);
        Assert.AreEqual(0, result.VisionRequiredPageCount);
        Assert.IsFalse(result.RequiresVision);
        Assert.IsFalse(result.RequiresReview);
        Assert.AreEqual("PDF_TEXT", result.Pages[0].ExtractionMethod);
        Assert.IsTrue(result.Pages[0].NativeText.Length >= 10);
    }

    [TestMethod]
    public void ImageOnlyPdf_RequiresVisionAndReview_WhenProviderMissing()
    {
        string path = Fixture("materials_image_only.pdf");
        var result = new PdfDrawingVisionReader().Read(path);

        Assert.IsTrue(result.PageCount > 0);
        Assert.IsTrue(result.VisionRequiredPageCount > 0);
        Assert.IsTrue(result.RequiresVision);
        Assert.IsTrue(result.RequiresReview);
        Assert.IsTrue(result.Pages[0].RequiresVision);
        Assert.IsTrue(result.Pages[0].RequiresReview);
        Assert.AreEqual("VISION_REQUIRED", result.Pages[0].ExtractionMethod);
        StringAssert.Contains(result.Pages[0].ReviewReason, "no Vision/OCR provider");
    }

    private static string Fixture(string name)
    {
        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData", name);
        Assert.IsTrue(File.Exists(path), "Missing test fixture: " + path);
        return path;
    }
}
