using System.Collections.Generic;
using SwMateAI.Core.Skills;
using SwMateAI.Core.Tools;

namespace SwMateAI.Core.Tests;

[TestClass]
public class CapabilityRegistryTests
{
    [TestMethod]
    public void LiveVerifiedRegistry_MatchesCurrentIntegrationSurface()
    {
        Assert.AreEqual(37, CapabilityRegistry.LiveVerifiedSkills.Count);
        Assert.IsTrue(CapabilityRegistry.IsLiveVerified(SkillNames.CreateBOM));
        Assert.IsTrue(CapabilityRegistry.IsLiveVerified(SkillNames.ReadDrawing));
        Assert.IsFalse(CapabilityRegistry.IsLiveVerified(SkillNames.CreateCircle));
        Assert.IsFalse(CapabilityRegistry.IsLiveVerified(SkillNames.AddMate));
    }

    [TestMethod]
    public void SkillCatalog_AppliesEvidenceBackedMaturity()
    {
        var verified = SkillCatalog.ForTool(new FakeTool("GetModelInfo"));
        Assert.AreEqual(SkillNames.ObserveModel, verified.Name);
        Assert.AreEqual(SkillMaturity.Verified, verified.Maturity);
        Assert.IsTrue(verified.HasLiveRegression);
        Assert.AreEqual("SOLIDWORKS 2021", verified.VerifiedSolidWorksVersion);

        var pilot = SkillCatalog.ForTool(new FakeTool(SkillNames.CreateCircle));
        Assert.AreEqual(SkillMaturity.Pilot, pilot.Maturity);
        Assert.IsFalse(pilot.HasLiveRegression);
    }

    private sealed class FakeTool : ISwTool
    {
        public FakeTool(string name) { Name = name; }
        public string Name { get; }
        public string Description => "test";
        public ToolResult Execute(Dictionary<string, object> parameters) => ToolResult.Success(null);
    }
}
