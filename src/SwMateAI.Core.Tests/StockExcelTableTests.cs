using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SwMateAI.Core.Manufacturing;

namespace SwMateAI.Core.Tests
{
    [TestClass]
    public class StockExcelTableTests
    {
        [TestMethod]
        public void TC029_MixedModel_KeepsDistinctStockGroupsInExportColumns()
        {
            var result = new BreakdownResult();
            result.Items.Add(new BreakdownItem
            {
                PartNumber = "SM-001", Quantity = 1, Material = "SUS304",
                ManufacturingForm = "Sheet Metal", StockType = "Sheet Metal",
                StockSize = "300 x 120 x 2 mm", FlatBlankLengthMm = 300,
                FlatBlankWidthMm = 120, SheetMetalThicknessMm = 2, IsLoaded = true
            });
            result.Items.Add(new BreakdownItem
            {
                PartNumber = "WLD-001", Quantity = 4, Material = "SS400",
                ManufacturingForm = "Weldment", StockType = "Weldment Profile",
                StockSize = "RHS 40x20x2 | L=500 mm | A1=45° | A2=45°",
                WeldmentCutLengthMm = 500, WeldmentAngle1Deg = 45,
                WeldmentAngle2Deg = 45, IsLoaded = true
            });
            result.Items.Add(new BreakdownItem
            {
                PartNumber = "BLK-001", Quantity = 2, Material = "C45",
                ManufacturingForm = "Generic", StockType = "Block",
                StockSize = "110 x 70 x 22 mm", IsLoaded = true
            });

            BreakdownTable table = new BreakdownTableGenerator().Generate(result);
            int groupColumn = table.Headers.IndexOf("Nhóm CAD / Cut-List");
            int stockTypeColumn = table.Headers.IndexOf("Loại phôi");
            int technologyColumn = table.Headers.IndexOf("Công nghệ gia công");
            int supplierColumn = table.Headers.IndexOf("Nhà gia công");

            Assert.AreEqual(3, table.Rows.Count);
            Assert.IsTrue(groupColumn >= 0 && stockTypeColumn >= 0);
            Assert.IsTrue(technologyColumn >= 0 && supplierColumn >= 0);
            Assert.IsTrue(table.Rows.Exists(r => Convert.ToString(r[groupColumn]) == "Sheet Metal" && Convert.ToString(r[stockTypeColumn]) == "Sheet Metal"));
            Assert.IsTrue(table.Rows.Exists(r => Convert.ToString(r[groupColumn]) == "Weldment" && Convert.ToString(r[stockTypeColumn]) == "Weldment Profile"));
            Assert.IsTrue(table.Rows.Exists(r => Convert.ToString(r[groupColumn]) == "Generic" && Convert.ToString(r[stockTypeColumn]) == "Block"));
            Assert.IsTrue(table.Rows.TrueForAll(r => string.IsNullOrEmpty(Convert.ToString(r[technologyColumn]))));
            Assert.IsTrue(table.Rows.TrueForAll(r => string.IsNullOrEmpty(Convert.ToString(r[supplierColumn]))));
        }

        [TestMethod]
        public void TC030_MissingCadMaterial_KeepsDimensionsAndShowsWarning()
        {
            var result = new BreakdownResult();
            result.Items.Add(new BreakdownItem
            {
                PartNumber = "NO-MAT",
                Quantity = 1,
                Material = string.Empty,
                FinishedXmm = 100,
                FinishedYmm = 50,
                FinishedZmm = 10,
                StockType = "Plate",
                StockSize = "110 x 60 x 12 mm",
                IsLoaded = true
            });

            BreakdownTable table = new BreakdownTableGenerator().Generate(result);
            int finishedColumn = table.Headers.IndexOf("Kích thước thành phẩm");
            int statusColumn = table.Headers.IndexOf("Trạng thái");

            Assert.AreEqual("100 x 50 x 10 mm", Convert.ToString(table.Rows[0][finishedColumn]));
            Assert.AreEqual("Thiếu vật liệu CAD", Convert.ToString(table.Rows[0][statusColumn]));
        }

        [TestMethod]
        public void TC031_VirtualPart_IsMarkedAndRetainsStockData()
        {
            var result = new BreakdownResult();
            result.Items.Add(new BreakdownItem
            {
                PartNumber = "VIRTUAL-01",
                Quantity = 2,
                Material = "C45",
                IsVirtual = true,
                IsLoaded = true,
                FinishedXmm = 80,
                FinishedYmm = 40,
                FinishedZmm = 12,
                StockType = "Block",
                StockSize = "90 x 50 x 14 mm"
            });

            BreakdownTable table = new BreakdownTableGenerator().Generate(result);
            int virtualColumn = table.Headers.IndexOf("Virtual Part");
            int stockColumn = table.Headers.IndexOf("Kích thước phôi");

            Assert.AreEqual("Có", Convert.ToString(table.Rows[0][virtualColumn]));
            Assert.AreEqual("90 x 50 x 14 mm", Convert.ToString(table.Rows[0][stockColumn]));
        }
    }
}
