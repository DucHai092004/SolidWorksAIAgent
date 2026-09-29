using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SwMateAI.Core.DrawingUnderstanding;

namespace SwMateAI.Core.Tests;

[TestClass]
public class DrawingVisionTests
{
    [TestMethod]
    public void PdfWithNativeText_DoesNotRequireVision()
    {
        string path = WriteMinimalPdf("Part Number P-001 Material C45");
        try
        {
            var result = new PdfDrawingVisionReader().Read(path);
            Assert.IsTrue(result.PageCount > 0);
            Assert.AreEqual(result.PageCount, result.NativeTextPageCount);
            Assert.AreEqual(0, result.VisionRequiredPageCount);
            Assert.IsFalse(result.RequiresVision);
            Assert.IsFalse(result.RequiresReview);
            Assert.AreEqual("PDF_TEXT", result.Pages[0].ExtractionMethod);
            Assert.IsTrue(result.Pages[0].NativeText.Length >= 10);
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public void PdfWithoutNativeText_RequiresVisionAndReview_WhenProviderMissing()
    {
        string path = WriteMinimalPdf(string.Empty);
        try
        {
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
        finally { File.Delete(path); }
    }

    private static string WriteMinimalPdf(string text)
    {
        string path = Path.Combine(Path.GetTempPath(), "swmate_vision_" + Guid.NewGuid().ToString("N") + ".pdf");
        string escaped = (text ?? string.Empty).Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
        string stream = string.IsNullOrEmpty(escaped)
            ? string.Empty
            : "BT /F1 12 Tf 72 720 Td (" + escaped + ") Tj ET";

        var body = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int> { 0 };
        AppendObject(body, offsets, 1, "<< /Type /Catalog /Pages 2 0 R >>");
        AppendObject(body, offsets, 2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        AppendObject(body, offsets, 3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>");
        AppendObject(body, offsets, 4, "<< /Length " + Encoding.ASCII.GetByteCount(stream) + " >>\nstream\n" + stream + "\nendstream");
        AppendObject(body, offsets, 5, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");

        int xref = Encoding.ASCII.GetByteCount(body.ToString());
        body.Append("xref\n0 6\n0000000000 65535 f \n");
        for (int i = 1; i <= 5; i++) body.Append(offsets[i].ToString("D10")).Append(" 00000 n \n");
        body.Append("trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n").Append(xref).Append("\n%%EOF\n");
        File.WriteAllText(path, body.ToString(), Encoding.ASCII);
        return path;
    }

    private static void AppendObject(StringBuilder body, List<int> offsets, int number, string content)
    {
        offsets.Add(Encoding.ASCII.GetByteCount(body.ToString()));
        body.Append(number).Append(" 0 obj\n").Append(content).Append("\nendobj\n");
    }
}
