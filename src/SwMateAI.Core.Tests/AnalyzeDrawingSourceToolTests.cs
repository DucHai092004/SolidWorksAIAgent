using System.Collections.Generic;
using SwMateAI.Core.DrawingUnderstanding;
using SwMateAI.Core.Skills;
using SwMateAI.Core.Tools.Drawing;

namespace SwMateAI.Core.Tests;

[TestClass]
public class AnalyzeDrawingSourceToolTests
{
    [TestMethod]
    public void MissingPath_ReturnsToolError()
    {
        var tool = new AnalyzeDrawingSourceTool(null);
        var result = tool.Execute(new Dictionary<string, object>());

        Assert.IsFalse(result.IsSuccess);
    }

    [TestMethod]
    public void RasterImage_ReturnsReviewGatedReadiness()
    {
        var tool = new AnalyzeDrawingSourceTool(null);
        var result = tool.Execute(new Dictionary<string, object>
        {
            ["Path"] = @"C:\Temp\drawing.png"
        });

        Assert.IsTrue(result.IsSuccess);
        var data = result.Data as DrawingSourceEvidenceResult;
        Assert.IsNotNull(data);
        Assert.IsTrue(data!.Readiness.RequiresVision);
        Assert.IsTrue(data.Readiness.RequiresReview);
        Assert.AreEqual(0, data.Evidence.Count);
    }

    [TestMethod]
    public void SkillMetadata_IsReadOnlyAndDoesNotRequireActiveDocument()
    {
        var metadata = SkillCatalog.ForTool(new AnalyzeDrawingSourceTool(null));

        Assert.AreEqual(SkillNames.AnalyzeDrawingSource, metadata.Name);
        Assert.AreEqual("Drawing.ExternalReader", metadata.Category);
        Assert.AreEqual(SkillRiskLevel.ReadOnly, metadata.RiskLevel);
        Assert.IsFalse(metadata.RequiresActiveDocument);
        Assert.IsFalse(metadata.RequiresConfirmation);
    }
}
