using System.Collections.Generic;
using SwMateAI.Core.Skills;
using SwMateAI.Core.Tools.Drawing;

namespace SwMateAI.Core.Tests;

[TestClass]
public class DrawingDocumentToolTests
{
    [TestMethod]
    public void MissingPath_ReturnsToolError()
    {
        var tool = new ReadDrawingDocumentTool();
        var result = tool.Execute(new Dictionary<string, object>());

        Assert.IsFalse(result.IsSuccess);
        StringAssert.Contains(result.ErrorMessage, "requires a PDF Path");
    }

    [TestMethod]
    public void SkillMetadata_IsReadOnlyAndDoesNotRequireActiveDocument()
    {
        var metadata = SkillCatalog.ForTool(new ReadDrawingDocumentTool());

        Assert.AreEqual(SkillNames.ReadDrawingDocument, metadata.Name);
        Assert.AreEqual("Drawing.Vision", metadata.Category);
        Assert.AreEqual(SkillRiskLevel.ReadOnly, metadata.RiskLevel);
        Assert.IsFalse(metadata.RequiresActiveDocument);
        Assert.IsFalse(metadata.RequiresConfirmation);
    }
}
