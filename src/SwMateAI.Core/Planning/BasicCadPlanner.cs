using SwMateAI.Core.Agent;

namespace SwMateAI.Core.Planning
{
    public static class BasicCadPlanner
    {
        public static TaskPlan Build(NaturalLanguageCadCommand command)
        {
            var plan = new TaskPlan { Goal = "Create requested CAD part" };
            var baseStep = new PlanStep { Index = 1, SkillName = command.Intent, Description = "Create base plate geometry" };
            baseStep.Parameters["Width"] = command.Width;
            baseStep.Parameters["Height"] = command.Height;
            baseStep.Parameters["Thickness"] = command.Thickness;
            if (command.Holes.Count > 0)
            {
                baseStep.Parameters["Holes"] = command.Holes;
                baseStep.Parameters["HoleDepth"] = command.Thickness;
            }
            plan.Steps.Add(baseStep);

            if (command.FilletRadius > 0)
            {
                var step = new PlanStep { Index = 2, SkillName = "FilletPlateCorners", Description = "Fillet four vertical plate edges" };
                step.Parameters["Radius"] = command.FilletRadius;
                plan.Steps.Add(step);
            }
            else if (command.ChamferDistance > 0)
            {
                var step = new PlanStep { Index = 2, SkillName = "ChamferPlateCorners", Description = "Chamfer four vertical plate edges" };
                step.Parameters["Distance"] = command.ChamferDistance;
                plan.Steps.Add(step);
            }
            return plan;
        }
    }
}
