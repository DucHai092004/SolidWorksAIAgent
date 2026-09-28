using System;
using System.IO;
using SwMateAI.Core.Common;
using SwMateAI.Core.Manufacturing;

namespace SwMateAI.Core.Tests;

[TestClass]
public class ManufacturingRulesTests
{
    [DataTestMethod]
    [DataRow("DCNX.02.04.00.00.stp.sldprt", "DCNX.02.04.00.00")]
    [DataRow("ABC.step.sldasm", "ABC")]
    [DataRow("PART.01.SLDPRT", "PART.01")]
    public void PartCodeNormalizer_StripsOnlyKnownCadSuffixes(string input, string expected)
        => Assert.AreEqual(expected, PartCodeNormalizer.CleanDisplay(input));

    [TestMethod]
    public void RoundStock_UsesDiameterPlus2_LengthPlus4()
    {
        var item = new BreakdownItem { StockType = "Round Bar", FinishedXmm = 40, FinishedYmm = 40, FinishedZmm = 100 };
        new StockCalculator().Calculate(item, new StockCalculationOptions());
        Assert.AreEqual("Ø42 x 104 mm", item.StockSize);
    }

    [TestMethod]
    public void PrismaticStock_UsesLengthWidthPlus10_ThicknessPlus2Fallback()
    {
        var item = new BreakdownItem { StockType = "Block", FinishedXmm = 100, FinishedYmm = 60, FinishedZmm = 17 };
        new StockCalculator().Calculate(item, new StockCalculationOptions { UseStandardThicknessTable = false });
        Assert.AreEqual("110 x 70 x 19 mm", item.StockSize);
    }

    [TestMethod]
    public void PrismaticStock_SelectsNextApprovedThickness()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(path, new[] { "Material,ThicknessMm", "C45,18", "C45,20", "C45,25" });
            var item = new BreakdownItem { StockType = "Block", StockMaterial = "C45", FinishedXmm = 100, FinishedYmm = 60, FinishedZmm = 17 };
            new StockCalculator().Calculate(item, new StockCalculationOptions { StandardThicknessCatalogPath = path });
            Assert.AreEqual("110 x 70 x 20 mm", item.StockSize);
            StringAssert.Contains(item.StockThicknessBasis, "Approved thickness table");
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public void StockWeight_DoesNotReuseCadDensityForDifferentStockMaterial()
    {
        var item = new BreakdownItem
        {
            Material = "Aluminum 6061", StockMaterial = "C45",
            StockVolumeMm3 = 1000000, DensityKgM3 = 2700
        };
        new StockWeightCalculator().Calculate(item);
        Assert.AreEqual(0d, item.StockWeightKg);
    }
}
