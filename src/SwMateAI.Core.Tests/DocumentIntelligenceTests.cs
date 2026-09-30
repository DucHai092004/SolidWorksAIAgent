using System;
using System.IO;
using SwMateAI.Core.DocumentIntelligence;

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
