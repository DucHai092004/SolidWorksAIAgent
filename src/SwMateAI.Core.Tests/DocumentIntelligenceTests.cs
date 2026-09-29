using System;
using System.IO;
using SwMateAI.Core.DocumentIntelligence;
using SwMateAI.Core.Manufacturing;

namespace SwMateAI.Core.Tests;

[TestClass]
public class DocumentIntelligenceTests
{
    [TestMethod]
    public void CsvParser_ReadsVietnameseHeaders_AndNormalizesDoubleExtension()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".csv");
        try
        {
            File.WriteAllLines(path, new[]
            {
                "Mã chi tiết,Vật liệu phôi",
                "DCNX.02.06.00.00.STEP.SLDPRT,S45C"
            });
            var records = new CsvMaterialSourceParser().Parse(path);
            Assert.AreEqual(1, records.Count);
            Assert.AreEqual("DCNX.02.06.00.00", records[0].NormalizedPartCode);
            Assert.AreEqual("S45C", records[0].StockMaterial);
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public void CsvParser_ReadsSemicolonSeparatedMaterial()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".csv");
        try
        {
            File.WriteAllText(path, "Part Number;Stock Material\nSHAFT-002.stp;42CrMo4\n");
            var records = new CsvMaterialSourceParser().Parse(path);
            Assert.AreEqual(1, records.Count);
            Assert.AreEqual("SHAFT-002", records[0].NormalizedPartCode);
            Assert.AreEqual("42CrMo4", records[0].StockMaterial);
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public void Enricher_ConflictingExactMaterials_RequiresReview()
    {
        var part = new BreakdownItem { PartNumber = "SHAFT-002", Material = "C45" };
        var import = new MaterialImportResult();
        import.Records.Add(new MaterialSourceRecord
        {
            NormalizedPartCode = "SHAFT-002", StockMaterial = "C45", SourceFile = "first.csv"
        });
        import.Records.Add(new MaterialSourceRecord
        {
            NormalizedPartCode = "SHAFT-002", StockMaterial = "42CrMo4", SourceFile = "second.csv"
        });
        var summary = new StockMaterialEnricher().Enrich(new[] { part }, import);
        Assert.AreEqual(1, summary.NeedsReview);
        Assert.IsTrue(part.StockMaterialNeedsReview);
        Assert.AreEqual("CONFLICT", part.StockMatchMethod);
        Assert.AreEqual(string.Empty, part.StockMaterial);
    }

    [TestMethod]
    public void PdfParser_ExtractsLabeledMaterialInReadingOrder()
    {
        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData", "materials_text.pdf");
        var records = new PdfMaterialSourceParser().Parse(path);
        Assert.AreEqual(1, records.Count);
        Assert.AreEqual("SHAFT-002", records[0].NormalizedPartCode);
        Assert.AreEqual("C45", records[0].StockMaterial);
    }

    [TestMethod]
    public void PdfParser_ReportsImageOnlyPageForReview()
    {
        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData", "materials_image_only.pdf");
        var parser = new PdfMaterialSourceParser();
        Assert.AreEqual(0, parser.Parse(path).Count);
        CollectionAssert.AreEqual(new[] { 1 }, new System.Collections.Generic.List<int>(parser.ImageOnlyPages));
    }

    [TestMethod]
    public void XlsxParser_ReadsSharedStringsWithHeaderOnRowFiveAndBlankColumn()
    {
        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData", "materials_shared_header_row5.xlsx");
        var records = new XlsxMaterialSourceParser().Parse(path);
        Assert.AreEqual(1, records.Count);
        Assert.AreEqual("SHAFT-002", records[0].NormalizedPartCode);
        Assert.AreEqual("42CrMo4", records[0].StockMaterial);
        Assert.AreEqual(6, records[0].RowNumber);
    }

    [TestMethod]
    public void Matcher_ExactMatch_AutoAccepts()
    {
        var source = new MaterialSourceRecord
        {
            RawPartCode = "P-001", NormalizedPartCode = "P-001",
            StockMaterial = "C45", ExtractionConfidence = 1
        };
        var match = new PartCodeMatcher().Find("P-001.SLDPRT", new[] { source });
        Assert.IsNotNull(match);
        Assert.IsTrue(match!.AutoAccept);
        Assert.AreEqual(1d, match.Confidence, 0.0001);
    }

    [TestMethod]
    public void Matcher_SimilarButDigitDifferent_DoesNotAutoAccept()
    {
        var source = new MaterialSourceRecord
        {
            RawPartCode = "PART-1001", NormalizedPartCode = "PART-1001",
            StockMaterial = "C45", ExtractionConfidence = 1
        };
        var match = new PartCodeMatcher().Find("PART-1002", new[] { source });
        Assert.IsTrue(match == null || !match.AutoAccept);
    }

    [TestMethod]
    public void Discovery_SkipsOutputAndBuildFolders()
    {
        string root = Path.Combine(Path.GetTempPath(), "swmate_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(Path.Combine(root, "Docs"));
            Directory.CreateDirectory(Path.Combine(root, "SW-MATE_AI_Output"));
            Directory.CreateDirectory(Path.Combine(root, "bin"));
            File.WriteAllText(Path.Combine(root, "Docs", "materials.csv"), "Part Number,Stock Material");
            File.WriteAllText(Path.Combine(root, "SW-MATE_AI_Output", "generated.csv"), "x");
            File.WriteAllText(Path.Combine(root, "bin", "build.csv"), "x");
            var files = new ProjectDocumentDiscovery().Discover(root);
            Assert.AreEqual(1, files.Count);
            StringAssert.EndsWith(files[0], "materials.csv");
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
