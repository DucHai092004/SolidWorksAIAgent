using SwMateAI.Core.Agent;
using SwMateAI.Core.Planning;
using SwMateAI.Core.Skills;

namespace SwMateAI.Core.Tests;

[TestClass]
public class DrawingUnderstandingTests
{
    [TestMethod]
    public void ReadDrawingIntent_BuildsSingleReadDrawingStep()
    {
        var plan = BasicCadPlanner.Build(new NaturalLanguageCadCommand
        {
            Intent = SkillNames.ReadDrawing
        });

        Assert.IsNotNull(plan);
        Assert.AreEqual(1, plan.Steps.Count);
        Assert.AreEqual(SkillNames.ReadDrawing, plan.Steps[0].SkillName);
    }
}
