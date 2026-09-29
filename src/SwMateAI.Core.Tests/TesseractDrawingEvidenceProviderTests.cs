using System;
using System.IO;
using SwMateAI.Core.DrawingUnderstanding;

namespace SwMateAI.Core.Tests;

[TestClass]
public class TesseractDrawingEvidenceProviderTests
{
    [TestMethod]
    public void CanAnalyze_RasterImagesOnly()
    {
        var provider = new TesseractCliDrawingEvidenceProvider(new FakeRunner());
        Assert.IsTrue(provider.CanAnalyze("drawing.png"));
        Assert.IsTrue(provider.CanAnalyze("drawing.TIFF"));
        Assert.IsFalse(provider.CanAnalyze("drawing.pdf"));
        Assert.IsFalse(provider.CanAnalyze("drawing.slddrw"));
    }

    [TestMethod]
    public void SuccessfulOcr_AddsEvidenceWithConfidence()
    {
        string path = TempRaster();
        try
        {
            var provider = new TesseractCliDrawingEvidenceProvider(new FakeRunner
            {
                Result = new TesseractOcrResult
                {
                    IsSuccess = true,
                    Text = "DIM 100 MATERIAL C45",
                    Confidence = 0.92
                }
            });

            var result = provider.Analyze(path);
            Assert.AreEqual(1, result.Evidence.Count);
            Assert.AreEqual("TESSERACT_OCR", result.Evidence[0].ExtractionMethod);
            Assert.AreEqual("DIM 100 MATERIAL C45", result.Evidence[0].RawText);
            Assert.AreEqual(0.92d, result.Evidence[0].Confidence, 0.0001d);
            Assert.IsFalse(result.Evidence[0].RequiresReview);
            Assert.AreEqual(0, result.Errors.Count);
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public void LowConfidenceOcr_RemainsReviewGated()
    {
        string path = TempRaster();
        try
        {
            var provider = new TesseractCliDrawingEvidenceProvider(new FakeRunner
            {
                Result = new TesseractOcrResult
                {
                    IsSuccess = true,
                    Text = "100",
                    Confidence = 0.60
                }
            });

            var result = provider.Analyze(path);
            Assert.AreEqual(1, result.Evidence.Count);
            Assert.IsTrue(result.Evidence[0].RequiresReview);
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public void RunnerFailure_ReturnsErrorWithoutInventingEvidence()
    {
        string path = TempRaster();
        try
        {
            var provider = new TesseractCliDrawingEvidenceProvider(new FakeRunner
            {
                Result = new TesseractOcrResult { Error = "tesseract unavailable" }
            });

            var result = provider.Analyze(path);
            Assert.AreEqual(0, result.Evidence.Count);
            Assert.AreEqual(1, result.Errors.Count);
            StringAssert.Contains(result.Errors[0], "tesseract unavailable");
        }
        finally { File.Delete(path); }
    }

    private static string TempRaster()
    {
        string path = Path.Combine(Path.GetTempPath(), "swmate_ocr_" + Guid.NewGuid().ToString("N") + ".png");
        File.WriteAllBytes(path, new byte[] { 0 });
        return path;
    }

    private sealed class FakeRunner : ITesseractOcrRunner
    {
        public TesseractOcrResult Result { get; set; } = new TesseractOcrResult
        {
            IsSuccess = true,
            Text = "TEXT",
            Confidence = 0.9
        };

        public TesseractOcrResult Run(string imagePath, string language) => Result;
    }
}
