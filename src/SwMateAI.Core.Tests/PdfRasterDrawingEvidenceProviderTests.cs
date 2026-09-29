using System;
using System.Collections.Generic;
using System.IO;
using SwMateAI.Core.DrawingUnderstanding;

namespace SwMateAI.Core.Tests;

[TestClass]
public class PdfRasterDrawingEvidenceProviderTests
{
    [TestMethod]
    public void RasterPage_AddsPageScopedOcrEvidence()
    {
        string path = TempPdf();
        try
        {
            var provider = new PdfRasterDrawingEvidenceProvider(
                new FakeExtractor(new PdfRasterPage
                {
                    PageNumber = 2,
                    ImageBytes = new byte[] { 1, 2, 3 },
                    ImageExtension = ".png"
                }),
                new FakeRunner
                {
                    Result = new TesseractOcrResult
                    {
                        IsSuccess = true,
                        Text = "DIM 125",
                        Confidence = 0.91
                    }
                },
                pageRenderer: new FakeRenderer());

            var result = provider.Analyze(path);
            Assert.AreEqual(1, result.Evidence.Count);
            Assert.AreEqual(2, result.Evidence[0].PageNumber);
            Assert.AreEqual("PDF_RASTER_TESSERACT", result.Evidence[0].ExtractionMethod);
            Assert.AreEqual("DIM 125", result.Evidence[0].RawText);
            Assert.AreEqual(0.91d, result.Evidence[0].Confidence, 0.0001d);
            Assert.IsFalse(result.Evidence[0].RequiresReview);
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public void NativeTextPage_IsSkippedByRasterOcrProvider()
    {
        string path = TempPdf();
        try
        {
            var runner = new FakeRunner();
            var renderer = new FakeRenderer();
            var provider = new PdfRasterDrawingEvidenceProvider(
                new FakeExtractor(new PdfRasterPage
                {
                    PageNumber = 1,
                    HasNativeText = true
                }),
                runner,
                pageRenderer: renderer);

            var result = provider.Analyze(path);
            Assert.AreEqual(0, result.Evidence.Count);
            Assert.AreEqual(0, runner.CallCount);
            Assert.AreEqual(0, renderer.CallCount);
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public void PageWithoutExtractableImage_UsesFullPageRendererBeforeOcr()
    {
        string path = TempPdf();
        try
        {
            var renderer = new FakeRenderer
            {
                Result = new PdfPageRenderResult
                {
                    IsSuccess = true,
                    ImageBytes = new byte[] { 9, 8, 7 },
                    ImageExtension = ".png"
                }
            };
            var runner = new FakeRunner
            {
                Result = new TesseractOcrResult
                {
                    IsSuccess = true,
                    Text = "RENDERED PAGE DIM 80",
                    Confidence = 0.9
                }
            };
            var provider = new PdfRasterDrawingEvidenceProvider(
                new FakeExtractor(new PdfRasterPage
                {
                    PageNumber = 3,
                    Error = "Full page rendering is required."
                }),
                runner,
                pageRenderer: renderer);

            var result = provider.Analyze(path);
            Assert.AreEqual(1, renderer.CallCount);
            Assert.AreEqual(1, runner.CallCount);
            Assert.AreEqual(1, result.Evidence.Count);
            Assert.AreEqual(3, result.Evidence[0].PageNumber);
            Assert.AreEqual("RENDERED PAGE DIM 80", result.Evidence[0].RawText);
            Assert.AreEqual(0, result.Errors.Count);
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public void RendererFailure_ReturnsErrorWithoutEvidence()
    {
        string path = TempPdf();
        try
        {
            var provider = new PdfRasterDrawingEvidenceProvider(
                new FakeExtractor(new PdfRasterPage
                {
                    PageNumber = 3,
                    Error = "Full page rendering is required."
                }),
                new FakeRunner(),
                pageRenderer: new FakeRenderer
                {
                    Result = new PdfPageRenderResult { Error = "pdftoppm unavailable" }
                });

            var result = provider.Analyze(path);
            Assert.AreEqual(0, result.Evidence.Count);
            Assert.AreEqual(1, result.Errors.Count);
            StringAssert.Contains(result.Errors[0], "rendering");
            StringAssert.Contains(result.Errors[0], "pdftoppm unavailable");
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public void LowConfidencePdfOcr_RemainsReviewGated()
    {
        string path = TempPdf();
        try
        {
            var provider = new PdfRasterDrawingEvidenceProvider(
                new FakeExtractor(new PdfRasterPage
                {
                    PageNumber = 1,
                    ImageBytes = new byte[] { 1 },
                    ImageExtension = ".png"
                }),
                new FakeRunner
                {
                    Result = new TesseractOcrResult
                    {
                        IsSuccess = true,
                        Text = "50",
                        Confidence = 0.7
                    }
                },
                pageRenderer: new FakeRenderer());

            var result = provider.Analyze(path);
            Assert.AreEqual(1, result.Evidence.Count);
            Assert.IsTrue(result.Evidence[0].RequiresReview);
        }
        finally { File.Delete(path); }
    }

    private static string TempPdf()
    {
        string path = Path.Combine(Path.GetTempPath(), "swmate_pdf_" + Guid.NewGuid().ToString("N") + ".pdf");
        File.WriteAllBytes(path, new byte[] { 1 });
        return path;
    }

    private sealed class FakeExtractor : IPdfRasterPageExtractor
    {
        private readonly IReadOnlyList<PdfRasterPage> _pages;
        public FakeExtractor(params PdfRasterPage[] pages) => _pages = pages;
        public IReadOnlyList<PdfRasterPage> Extract(string pdfPath) => _pages;
    }

    private sealed class FakeRunner : ITesseractOcrRunner
    {
        public int CallCount { get; private set; }
        public TesseractOcrResult Result { get; set; } = new TesseractOcrResult
        {
            IsSuccess = true,
            Text = "TEXT",
            Confidence = 0.9
        };

        public TesseractOcrResult Run(string imagePath, string language)
        {
            CallCount++;
            return Result;
        }
    }

    private sealed class FakeRenderer : IPdfPageRenderer
    {
        public int CallCount { get; private set; }
        public PdfPageRenderResult Result { get; set; } = new PdfPageRenderResult
        {
            IsSuccess = true,
            ImageBytes = new byte[] { 1, 2 },
            ImageExtension = ".png"
        };

        public PdfPageRenderResult Render(string pdfPath, int pageNumber)
        {
            CallCount++;
            return Result;
        }
    }
}
