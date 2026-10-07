using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml.Packaging;
using SwMateAI.Core.BOM;

namespace SwMateAI.Core.Tests
{
    [TestClass]
    public class BomExcelExporterImageStressTests
    {
        [TestMethod]
        public void TC012_ExporterHandles600RowsWith600EmbeddedThumbnails()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "SW_MATE_AI_BOM_STRESS_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            try
            {
                string image = Path.Combine(root, "thumb.png");
                File.WriteAllBytes(image, Convert.FromBase64String(
                    "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="));

                var result = new BomResult();
                for (int i = 1; i <= 600; i++)
                {
                    result.Items.Add(new BomItem
                    {
                        ItemNumber = i,
                        PartNumber = "STRESS-" + i.ToString("D4"),
                        Description = "Stress image row " + i,
                        Quantity = 1,
                        ComponentType = "Part",
                        ImagePath = image
                    });
                }

                string output = Path.Combine(root, "bom_600_images.xlsx");
                var watch = Stopwatch.StartNew();
                new BomExcelExporter().Export(result, output);
                watch.Stop();

                Assert.IsTrue(File.Exists(output));
                Assert.IsTrue(new FileInfo(output).Length > 0);
                using (SpreadsheetDocument document = SpreadsheetDocument.Open(output, false))
                {
                    var worksheet = document.WorkbookPart!.WorksheetParts.Single();
                    Assert.IsNotNull(worksheet.DrawingsPart);
                    Assert.AreEqual(600, worksheet.DrawingsPart!.ImageParts.Count());
                }

                Console.WriteLine("600 rows + 600 thumbnails exported in " + watch.ElapsedMilliseconds + " ms");
            }
            finally
            {
                try { Directory.Delete(root, true); } catch { }
            }
        }
    }
}
