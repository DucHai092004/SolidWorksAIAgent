using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SwMateAI.Core.DrawingUnderstanding;

namespace SwMateAI.Core.Tests;

[TestClass]
public class DrawingVisionPdfReadinessTests
{
    [TestMethod]
    public void TextPdf_DoesNotRequireVision()
    {
        string path = TempPdf("DRAWING NUMBER A-001 MATERIAL C45 DIMENSION 100 MM");
        try
        {
            var result = new DrawingVisionReadinessAnalyzer().Analyze(path);
            Assert.AreEqual(DrawingVisionSourceKind.TextPdf, result.SourceKind);
            Assert.AreEqual(1, result.PageCount);
            Assert.AreEqual(1, result.TextPageCount);
            Assert.AreEqual(0, result.RasterPageCount);
            Assert.IsFalse(result.RequiresVision);
            Assert.IsFalse(result.RequiresReview);
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public void TextlessPdf_RequiresVisionAndReview()
    {
        string path = TempPdf(string.Empty);
        try
        {
            var result = new DrawingVisionReadinessAnalyzer().Analyze(path);
            Assert.AreEqual(DrawingVisionSourceKind.RasterPdf, result.SourceKind);
            Assert.AreEqual(1, result.RasterPageCount);
            Assert.IsTrue(result.RequiresVision);
            Assert.IsTrue(result.RequiresReview);
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public void MixedPdf_RequiresVisionAndReviewForIncompleteEvidence()
    {
        string path = TempPdf("DRAWING NUMBER A-002 MATERIAL S45C DIMENSION 50 MM", string.Empty);
        try
        {
            var result = new DrawingVisionReadinessAnalyzer().Analyze(path);
            Assert.AreEqual(DrawingVisionSourceKind.MixedPdf, result.SourceKind);
            Assert.AreEqual(2, result.PageCount);
            Assert.AreEqual(1, result.TextPageCount);
            Assert.AreEqual(1, result.RasterPageCount);
            Assert.IsTrue(result.RequiresVision);
            Assert.IsTrue(result.RequiresReview);
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public void PdfTextProvider_ReturnsRawEvidenceWithConfidence()
    {
        string path = TempPdf("DRAWING NUMBER A-003 MATERIAL SCM440 DIMENSION 25 MM");
        try
        {
            var result = new PdfTextDrawingEvidenceProvider().Analyze(path);
            Assert.AreEqual(1, result.Evidence.Count);
            Assert.AreEqual("PDF_TEXT", result.Evidence[0].ExtractionMethod);
            Assert.AreEqual(0.95d, result.Evidence[0].Confidence, 0.0001d);
            Assert.IsFalse(result.Evidence[0].RequiresReview);
            StringAssert.Contains(result.Evidence[0].RawText, "A-003");
            Assert.IsFalse(result.Readiness.RequiresVision);
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public void PdfTextProvider_DoesNotInventEvidenceForTextlessPage()
    {
        string path = TempPdf(string.Empty);
        try
        {
            var result = new PdfTextDrawingEvidenceProvider().Analyze(path);
            Assert.AreEqual(0, result.Evidence.Count);
            Assert.IsTrue(result.Readiness.RequiresVision);
            Assert.IsTrue(result.Readiness.RequiresReview);
            Assert.AreEqual(DrawingVisionSourceKind.RasterPdf, result.Readiness.SourceKind);
        }
        finally { File.Delete(path); }
    }

    private static string TempPdf(params string[] pageTexts)
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".pdf");
        WriteMinimalPdf(path, pageTexts);
        return path;
    }

    private static void WriteMinimalPdf(string path, IReadOnlyList<string> pageTexts)
    {
        int pageCount = pageTexts.Count;
        int objectCount = 3 + pageCount * 2;
        var objects = new string[objectCount + 1];
        objects[1] = "<< /Type /Catalog /Pages 2 0 R >>";
        string kids = string.Join(" ", Enumerable.Range(0, pageCount).Select(i => (4 + i * 2) + " 0 R"));
        objects[2] = $"<< /Type /Pages /Kids [{kids}] /Count {pageCount} >>";
        objects[3] = "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>";

        for (int i = 0; i < pageCount; i++)
        {
            int pageObject = 4 + i * 2;
            int contentObject = pageObject + 1;
            objects[pageObject] = $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 3 0 R >> >> /Contents {contentObject} 0 R >>";
            string text = EscapePdfText(pageTexts[i] ?? string.Empty);
            string stream = text.Length == 0 ? string.Empty : $"BT\n/F1 12 Tf\n72 720 Td\n({text}) Tj\nET\n";
            int length = Encoding.ASCII.GetByteCount(stream);
            objects[contentObject] = $"<< /Length {length} >>\nstream\n{stream}endstream";
        }

        var builder = new StringBuilder("%PDF-1.4\n");
        var offsets = new int[objectCount + 1];
        for (int id = 1; id <= objectCount; id++)
        {
            offsets[id] = Encoding.ASCII.GetByteCount(builder.ToString());
            builder.Append(id).Append(" 0 obj\n").Append(objects[id]).Append("\nendobj\n");
        }

        int xrefOffset = Encoding.ASCII.GetByteCount(builder.ToString());
        builder.Append("xref\n0 ").Append(objectCount + 1).Append("\n");
        builder.Append("0000000000 65535 f \n");
        for (int id = 1; id <= objectCount; id++)
            builder.Append(offsets[id].ToString("D10")).Append(" 00000 n \n");
        builder.Append("trailer\n<< /Size ").Append(objectCount + 1).Append(" /Root 1 0 R >>\n");
        builder.Append("startxref\n").Append(xrefOffset).Append("\n%%EOF\n");
        File.WriteAllBytes(path, Encoding.ASCII.GetBytes(builder.ToString()));
    }

    private static string EscapePdfText(string value)
    {
        return (value ?? string.Empty).Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
    }
}
