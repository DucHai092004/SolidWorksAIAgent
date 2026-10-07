using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SwMateAI.Core.Manufacturing;

namespace SwMateAI.Core.Tests
{
    [TestClass]
    public class CutListStockExpanderTests
    {
        [TestMethod]
        public void TC023_SheetMetalCutList_UsesExactFlatBlankDimensions()
        {
            var baseItem = new BreakdownItem
            {
                PartNumber = "SM-001",
                Quantity = 2,
                IsVirtual = false
            };
            var properties = new Dictionary<string, string>
            {
                ["Bounding Box Length"] = "320.5 mm",
                ["Bounding Box Width"] = "145,25 mm",
                ["Sheet Metal Thickness"] = "2 mm",
                ["QUANTITY"] = "3"
            };

            BreakdownItem item = CutListStockExpander.CreateFromProperties(baseItem, properties, 1);

            Assert.IsNotNull(item);
            Assert.AreEqual("Sheet Metal", item.ManufacturingForm);
            Assert.AreEqual("Sheet Metal", item.StockType);
            Assert.AreEqual(320.5, item.FlatBlankLengthMm, 0.001);
            Assert.AreEqual(145.25, item.FlatBlankWidthMm, 0.001);
            Assert.AreEqual(2.0, item.SheetMetalThicknessMm, 0.001);
            Assert.AreEqual("320.5 x 145.25 x 2 mm", item.StockSize);
            Assert.AreEqual(6, item.Quantity);
            StringAssert.Contains(item.ManufacturingEvidence, "Bounding Box Length/Width");
        }

        [TestMethod]
        public void TC024_WeldmentCutList_PreservesLengthAnglesAndQuantity()
        {
            var baseItem = new BreakdownItem
            {
                PartNumber = "FRAME-001",
                Quantity = 2,
                IsVirtual = true
            };
            var properties = new Dictionary<string, string>
            {
                ["DESCRIPTION"] = "RHS 40x20x2",
                ["LENGTH"] = "502.75 mm",
                ["ANGLE1"] = "45 deg",
                ["ANGLE2"] = "30 deg",
                ["QUANTITY"] = "4"
            };

            BreakdownItem item = CutListStockExpander.CreateFromProperties(baseItem, properties, 2);

            Assert.IsNotNull(item);
            Assert.AreEqual("Weldment", item.ManufacturingForm);
            Assert.AreEqual("Weldment Profile", item.StockType);
            Assert.AreEqual("RHS 40x20x2", item.WeldmentProfileDescription);
            Assert.AreEqual(502.75, item.WeldmentCutLengthMm, 0.001);
            Assert.AreEqual(45.0, item.WeldmentAngle1Deg, 0.001);
            Assert.AreEqual(30.0, item.WeldmentAngle2Deg, 0.001);
            Assert.AreEqual(4, item.WeldmentCutQuantity);
            Assert.AreEqual(8, item.Quantity);
            Assert.IsTrue(item.IsVirtual, "P2 expansion must preserve Virtual Part identity.");
            StringAssert.Contains(item.StockSize, "L=502.75 mm");
            StringAssert.Contains(item.StockSize, "A1=45°");
            StringAssert.Contains(item.StockSize, "A2=30°");
        }

        [TestMethod]
        public void GenericCutListProperties_DoNotInventManufacturingType()
        {
            var baseItem = new BreakdownItem { PartNumber = "GENERIC" };
            var properties = new Dictionary<string, string>
            {
                ["Description"] = "Generic body"
            };

            Assert.IsNull(CutListStockExpander.CreateFromProperties(baseItem, properties));
        }
    }
}
