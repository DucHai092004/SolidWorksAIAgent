using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
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

                using (ZipArchive zip = ZipFile.OpenRead(exported))
                {
                    Assert.IsNotNull(zip.GetEntry("xl/worksheets/sheet1.xml"));
                    Assert.IsNotNull(zip.GetEntry("xl/drawings/drawing1.xml"));
                    Assert.IsTrue(zip.Entries.Any(x => x.FullName.StartsWith("xl/media/", StringComparison.OrdinalIgnoreCase)));

                    string sheetXml;
                    using (var reader = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml").Open()))
                        sheetXml = reader.ReadToEnd();

                    StringAssert.Contains(sheetXml, "SHAFT-001");
                    StringAssert.Contains(sheetXml, "Round Bar");
                    StringAssert.Contains(sheetXml, "Ø42 x 104 mm");
                }
            }
            finally
            {
                try { Directory.Delete(root, true); } catch { }
            }
        }
    }
}
