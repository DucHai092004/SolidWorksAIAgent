using System;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using SwMateAI.Core.BOM;

namespace SwMateAI.Core.Tests
{
    [TestClass]
    public class BomExcelExporterTests
    {
        private string _tempFolder = string.Empty;

        [TestInitialize]
        public void Setup()
        {
            _tempFolder = Path.Combine(Path.GetTempPath(), "SW-MATE_AI_Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempFolder);
        }

        [TestCleanup]
        public void Cleanup()
        {
            try
            {
                if (Directory.Exists(_tempFolder)) Directory.Delete(_tempFolder, true);
            }
            catch { }
        }

        [TestMethod]
        public void Export_WritesUnicodeRowsAndEmbedsThumbnail()
        {
            string image = Path.Combine(_tempFolder, "Trục_Đỡ_Ø25.png");
            File.WriteAllBytes(image, OnePixelPng());

            var result = new BomResult();
            result.Items.Add(new BomItem
            {
                ItemNumber = 1,
                PartNumber = "Trục_Đỡ_Ø25",
                Description = "Chi tiết đỡ tiếng Việt",
                Quantity = 2,
                Material = "C45",
                ComponentType = "Part",
                Configuration = "Default",
                ImagePath = image
            });

            string output = Path.Combine(_tempFolder, "bom_unicode.xlsx");
            string actual = new BomExcelExporter().Export(result, output);

            Assert.AreEqual(output, actual);
            Assert.IsTrue(File.Exists(output));
            Assert.IsTrue(new FileInfo(output).Length > 0);

            using (SpreadsheetDocument document = SpreadsheetDocument.Open(output, false))
            {
                WorksheetPart worksheet = document.WorkbookPart!.WorksheetParts.Single();
                Assert.IsNotNull(worksheet.DrawingsPart, "The BOM worksheet must contain a drawing part.");
                Assert.AreEqual(1, worksheet.DrawingsPart!.ImageParts.Count(), "Exactly one thumbnail should be embedded.");

                string xml = worksheet.Worksheet.OuterXml;
                StringAssert.Contains(xml, "Trục_Đỡ_Ø25");
                StringAssert.Contains(xml, "Chi tiết đỡ tiếng Việt");
            }
        }

        [TestMethod]
        public void Export_AllowsEmptyCustomPropertyValues()
        {
            var result = new BomResult();
            result.Items.Add(new BomItem
            {
                ItemNumber = 1,
                PartNumber = "P-001",
                Description = string.Empty,
                Quantity = 1,
                Material = string.Empty,
                ComponentType = "Part",
                Configuration = string.Empty
            });

            string output = Path.Combine(_tempFolder, "bom_empty_properties.xlsx");
            new BomExcelExporter().Export(result, output);

            using (SpreadsheetDocument document = SpreadsheetDocument.Open(output, false))
            {
                SheetData data = document.WorkbookPart!.WorksheetParts.Single()
                    .Worksheet.GetFirstChild<SheetData>()!;
                Assert.AreEqual(2, data.Elements<Row>().Count());
                Assert.AreEqual(8, data.Elements<Row>().Last().Elements<Cell>().Count());
            }
        }

        [TestMethod]
        public void Export_SupportsLargeBomWithoutExcelAutomation()
        {
            var result = new BomResult();
            for (int i = 1; i <= 600; i++)
            {
                result.Items.Add(new BomItem
                {
                    ItemNumber = i,
                    PartNumber = "P-" + i.ToString("D4"),
                    Description = "Large BOM row " + i,
                    Quantity = 1,
                    ComponentType = "Part"
                });
            }

            string output = Path.Combine(_tempFolder, "bom_600_rows.xlsx");
            new BomExcelExporter().Export(result, output);

            using (SpreadsheetDocument document = SpreadsheetDocument.Open(output, false))
            {
                SheetData data = document.WorkbookPart!.WorksheetParts.Single()
                    .Worksheet.GetFirstChild<SheetData>()!;
                Assert.AreEqual(601, data.Elements<Row>().Count());
            }
        }

        [TestMethod]
        public void Export_DoesNotOverwriteExistingWorkbook()
        {
            string output = Path.Combine(_tempFolder, "existing.xlsx");
            File.WriteAllText(output, "keep-me");

            Assert.ThrowsException<IOException>(() =>
                new BomExcelExporter().Export(new BomResult(), output));
            Assert.AreEqual("keep-me", File.ReadAllText(output));
        }

        private static byte[] OnePixelPng()
        {
            return Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
        }
    }
}
