using SwMateAI.Core.Agent;
using SwMateAI.Core.Skills;

namespace SwMateAI.Core.Tests;

[TestClass]
public class NaturalLanguageParserTests
{
    [DataTestMethod]
    [DataRow("Tạo bảng phôi", SkillNames.ExportManufacturingBreakdown)]
    [DataRow("Xuất bảng vật liệu phôi Excel", SkillNames.ExportManufacturingBreakdown)]
    [DataRow("Nhập vật liệu phôi vào các Part", SkillNames.ApplyStockMaterials)]
    [DataRow("Apply stock material", SkillNames.ApplyStockMaterials)]
    public void ManufacturingCommands_AreRecognized(string text, string expectedIntent)
    {
        Assert.IsTrue(NaturalLanguageCadParser.TryParse(text, out var command, out var error), error);
        Assert.AreEqual(expectedIntent, command.Intent);
    }

    [TestMethod]
    public void ExistingBomCommand_RemainsBom()
    {
        Assert.IsTrue(NaturalLanguageCadParser.TryParse("Tạo BOM và xuất Excel", out var command, out var error), error);
        Assert.AreEqual(SkillNames.CreateBOM, command.Intent);
        Assert.IsTrue(command.BomExportExcel);
    }
}
