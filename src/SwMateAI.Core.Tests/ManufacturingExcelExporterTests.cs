using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SwMateAI.Core.Manufacturing;

namespace SwMateAI.Core.Tests
{
    [TestClass]
    public class ManufacturingExcelExporterTests
    {
        [TestMethod]
        public void TC027_ExporterCreatesXlsxAndEmbedsImageWithoutExcelCom()
        {
            string root = Path.Combine(Path.GetTempPath(), "SWMATE_TC027_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                string imagePath = Path.Combine(root, "part.png");
                File.WriteAllBytes(imagePath, Convert.FromBase64String(
                    "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Y9ZK7sAAAAASUVORK5CYII="));

                var table = new BreakdownTable();
                table.Headers.AddRange(new[]
                {
                    "Hình ảnh", "Part Number", "Tên chi tiết", "Số lượng", "Loại phôi", "Kích thước phôi"
                });
                table.Rows.Add(new List<object>
                {
                    imagePath, "SHAFT-001", "Trục", 2, "Round Bar", "Ø42 x 104 mm"
                });

                string output = Path.Combine(root, "BangPhoi.xlsx");
                string exported = new ExcelExporter().Export(table, output);

                Assert.AreEqual(output, exported);
                Assert.IsTrue(File.Exists(exported));
                Assert.IsTrue(new FileInfo(exported).Length > 0);

                using (SpreadsheetDocument document = SpreadsheetDocument.Open(exported, false))
                {
                    WorkbookPart workbook = document.WorkbookPart;
                    Assert.IsNotNull(workbook);
                    WorksheetPart worksheet = workbook.WorksheetParts.Single();
                    Assert.IsNotNull(worksheet);
                    Assert.IsNotNull(worksheet.DrawingsPart);
                    Assert.AreEqual(1, worksheet.DrawingsPart.ImageParts.Count());

                    string worksheetText = worksheet.Worksheet.InnerText;
                    StringAssert.Contains(worksheetText, "SHAFT-001");
                    StringAssert.Contains(worksheetText, "Round Bar");
                    StringAssert.Contains(worksheetText, "Ø42 x 104 mm");
                }
            }
            finally
            {
                try { Directory.Delete(root, true); } catch { }
            }
        }
    }
}
