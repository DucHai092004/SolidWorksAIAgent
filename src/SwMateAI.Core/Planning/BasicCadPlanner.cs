using SwMateAI.Core.Agent;
using SwMateAI.Core.Skills;

namespace SwMateAI.Core.Planning
{
    public static class BasicCadPlanner
    {
        public static TaskPlan Build(NaturalLanguageCadCommand command)
        {
            if (IsReadIntent(command.Intent))
            {
                var readPlan = new TaskPlan { Goal = "Read requested CAD model data" };
                readPlan.Steps.Add(new PlanStep
                {
                    Index = 1,
                    SkillName = command.Intent,
                    Description = "Read data from the active SOLIDWORKS model"
                });
                return readPlan;
            }

            if (command.Intent == SkillNames.ModifyDimension)
            {
                var modifyPlan = new TaskPlan { Goal = "Modify requested CAD dimension" };
                var step = new PlanStep { Index = 1, SkillName = SkillNames.ModifyDimension, Description = $"Set {command.DimensionName} to {command.DimensionValue:0.###} mm" };
                step.Parameters["Name"] = command.DimensionName;
                step.Parameters["Value"] = command.DimensionValue;
                modifyPlan.Steps.Add(step);
                return modifyPlan;
            }

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
                var step = new PlanStep { Index = 2, SkillName = SkillNames.CreateFillet, Description = "Fillet four vertical plate edges" };
                step.Parameters["Radius"] = command.FilletRadius;
                plan.Steps.Add(step);
            }
            else if (command.ChamferDistance > 0)
            {
                var step = new PlanStep { Index = 2, SkillName = SkillNames.CreateChamfer, Description = "Chamfer four vertical plate edges" };
                step.Parameters["Distance"] = command.ChamferDistance;
                plan.Steps.Add(step);
            }
            return plan;
        }

        private static bool IsReadIntent(string intent)
        {
            return intent == SkillNames.ReadFeatureTree || intent == SkillNames.ReadFeatures ||
                   intent == SkillNames.ReadSketches || intent == SkillNames.ReadDimensions ||
                   intent == SkillNames.ReadMaterial || intent == SkillNames.ReadMassProperties ||
                   intent == SkillNames.ReadCustomProperties || intent == SkillNames.ReadSelectedObject ||
                   intent == SkillNames.ReadBoundingBox;
        }
    }
}
