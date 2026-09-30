using System.Linq;
using SolidWorks.Interop.swconst;
using SwMateAI.Core.BOM;

namespace SwMateAI.Core.Tests;

[TestClass]
public class BomHierarchyTests
{
    [DataTestMethod]
    [DataRow(BomMode.TopLevel, BomChildDisplay.Show, 1, 0, 0)]
    [DataRow(BomMode.TopLevel, BomChildDisplay.Hide, 1, 0, 0)]
    [DataRow(BomMode.TopLevel, BomChildDisplay.Promote, 0, 2, 0)]
    [DataRow(BomMode.PartsOnly, BomChildDisplay.Show, 0, 2, 0)]
    [DataRow(BomMode.PartsOnly, BomChildDisplay.Hide, 0, 0, 0)]
    [DataRow(BomMode.PartsOnly, BomChildDisplay.Promote, 0, 2, 0)]
    [DataRow(BomMode.Indented, BomChildDisplay.Show, 1, 2, 1)]
    [DataRow(BomMode.Indented, BomChildDisplay.Hide, 1, 0, 0)]
    [DataRow(BomMode.Indented, BomChildDisplay.Promote, 0, 2, 0)]
    public void Resolver_HandlesModeAndChildDisplay(
        BomMode mode,
        BomChildDisplay display,
        int expectedAssemblies,
        int expectedParts,
        int expectedPartLevel)
    {
        var subassembly = Fixture(display);
        var resolved = BomHierarchyResolver.Resolve(
            new[] { subassembly },
            new BomBuildOptions { Mode = mode, RespectChildDisplay = true });

        Assert.AreEqual(expectedAssemblies,
            resolved.Count(x => x.Node.ComponentType == "Assembly"));
        Assert.AreEqual(expectedParts,
            resolved.Count(x => x.Node.ComponentType == "Part"));

        foreach (var part in resolved.Where(x => x.Node.ComponentType == "Part"))
            Assert.AreEqual(expectedPartLevel, part.Level);
    }

    [TestMethod]
    public void Resolver_SkipsSuppressedAndExcludedNodes()
    {
        var shown = Part("P1", "P-001");
        var suppressed = Part("P2", "P-002");
        suppressed.IsSuppressed = true;
        var excluded = Part("P3", "P-003");
        excluded.ExcludeFromBom = true;

        var resolved = BomHierarchyResolver.Resolve(
            new[] { shown, suppressed, excluded },
            new BomBuildOptions { Mode = BomMode.PartsOnly });

        Assert.AreEqual(1, resolved.Count);
        Assert.AreEqual("P-001", resolved[0].Node.PartNumber);
    }

    [DataTestMethod]
    [DataRow((int)swComponentSuppressionState_e.swComponentSuppressed, true)]
    [DataRow((int)swComponentSuppressionState_e.swComponentLightweight, false)]
    [DataRow((int)swComponentSuppressionState_e.swComponentFullyResolved, false)]
    [DataRow((int)swComponentSuppressionState_e.swComponentResolved, false)]
    [DataRow((int)swComponentSuppressionState_e.swComponentFullyLightweight, false)]
    [DataRow((int)swComponentSuppressionState_e.swComponentInternalIdMismatch, false)]
    public void SuppressionPolicy_OnlySkipsActuallySuppressedComponents(int state, bool expectedSkip)
    {
        Assert.AreEqual(expectedSkip, BomSuppressionPolicy.ShouldSkip(state));
    }

    private static BomHierarchyNode Fixture(BomChildDisplay display)
    {
        var assembly = new BomHierarchyNode
        {
            IdentityKey = "SUBASM|DEFAULT",
            ComponentName = "SubAsm-1",
            PartNumber = "SUBASM",
            ComponentType = "Assembly",
            ChildDisplay = display,
            IsLoaded = true
        };
        assembly.Children.Add(Part("PartA-1", "PART-A"));
        assembly.Children.Add(Part("PartA-2", "PART-A"));
        return assembly;
    }

    private static BomHierarchyNode Part(string name, string partNumber)
    {
        return new BomHierarchyNode
        {
            IdentityKey = partNumber + "|DEFAULT",
            ComponentName = name,
            PartNumber = partNumber,
            ComponentType = "Part",
            IsLoaded = true
        };
    }
}
