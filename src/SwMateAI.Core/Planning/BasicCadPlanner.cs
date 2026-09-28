using SwMateAI.Core.Agent;
using SwMateAI.Core.Skills;

namespace SwMateAI.Core.Planning
{
    public static class BasicCadPlanner
    {
        public static TaskPlan Build(NaturalLanguageCadCommand command)
        {
            if (command.Intent == SkillNames.CreateSection)
            {
                var sectionPlan = new TaskPlan { Goal = "Create section Drawing view" };
                var step = new PlanStep
                {
                    Index = 1,
                    SkillName = SkillNames.CreateSection,
                    Description = "Create section " + command.SectionLabel + "-" + command.SectionLabel +
                                  " with a " + command.SectionDirection.ToLowerInvariant() + " cutting line"
                };
                step.Parameters["Label"] = command.SectionLabel;
                step.Parameters["Direction"] = command.SectionDirection;
                if (!string.IsNullOrWhiteSpace(command.SectionSourceViewName))
                    step.Parameters["SourceViewName"] = command.SectionSourceViewName;
                sectionPlan.Steps.Add(step);
                return sectionPlan;
            }

            if (command.Intent == SkillNames.InsertIsometricView)
            {
                var isoPlan = new TaskPlan { Goal = "Insert isometric Drawing view" };
                isoPlan.Steps.Add(new PlanStep
                {
                    Index = 1,
                    SkillName = SkillNames.InsertIsometricView,
                    Description = "Insert an isometric model view on the active Drawing sheet"
                });
                return isoPlan;
            }

            if (command.Intent == SkillNames.InsertStandardViews)
            {
                var viewsPlan = new TaskPlan { Goal = "Insert standard Drawing views" };
                var step = new PlanStep
                {
                    Index = 1,
                    SkillName = SkillNames.InsertStandardViews,
                    Description = "Insert standard orthographic views using " + command.DrawingProjection + " angle projection"
                };
                step.Parameters["Projection"] = command.DrawingProjection;
                viewsPlan.Steps.Add(step);
                return viewsPlan;
            }

            if (command.Intent == SkillNames.CreateSheet)
            {
                var sheetPlan = new TaskPlan { Goal = "Create Drawing sheet" };
                var step = new PlanStep
                {
                    Index = 1,
                    SkillName = SkillNames.CreateSheet,
                    Description = "Create " + command.SheetPaperSize + " Drawing sheet at scale " +
                                  command.SheetScaleNumerator.ToString("0.###") + ":" +
                                  command.SheetScaleDenominator.ToString("0.###")
                };
                if (!string.IsNullOrWhiteSpace(command.SheetName)) step.Parameters["Name"] = command.SheetName;
                step.Parameters["PaperSize"] = command.SheetPaperSize;
                step.Parameters["ScaleNumerator"] = command.SheetScaleNumerator;
                step.Parameters["ScaleDenominator"] = command.SheetScaleDenominator;
                sheetPlan.Steps.Add(step);
                return sheetPlan;
            }

            if (command.Intent == SkillNames.CreateDrawing)
            {
                var drawingPlan = new TaskPlan { Goal = "Create Drawing from active model" };
                drawingPlan.Steps.Add(new PlanStep
                {
                    Index = 1,
                    SkillName = SkillNames.CreateDrawing,
                    Description = "Create a new Drawing using the default SOLIDWORKS Drawing template"
                });
                return drawingPlan;
            }

            if (command.Intent == SkillNames.InsertDimensions ||
                command.Intent == SkillNames.InsertDrawingBOM ||
                command.Intent == SkillNames.InsertBalloon ||
                command.Intent == SkillNames.FillTitleBlock ||
                command.Intent == SkillNames.ExportPDF ||
                command.Intent == SkillNames.ExportDXF)
            {
                var finishPlan = new TaskPlan { Goal = "Complete Drawing automation task" };
                finishPlan.Steps.Add(new PlanStep
                {
                    Index = 1,
                    SkillName = command.Intent,
                    Description = "Execute Drawing skill: " + command.Intent
                });
                return finishPlan;
            }

            if (command.Intent == SkillNames.DeleteMate)
            {
                var actionPlan = new TaskPlan { Goal = "Modify Assembly" };
                var step = new PlanStep { Index = 1, SkillName = SkillNames.DeleteMate,
                    Description = $"Delete Mate {command.MateName}" };
                step.Parameters["MateName"] = command.MateName;
                actionPlan.Steps.Add(step);
                return actionPlan;
            }

            if (command.Intent == SkillNames.AddMate)
            {
                var actionPlan = new TaskPlan { Goal = "Modify Assembly" };
                var step = new PlanStep { Index = 1, SkillName = SkillNames.AddMate,
                    Description = $"Add {command.MateType} Mate to the selected Assembly entities" };
                step.Parameters["MateType"] = command.MateType;
                step.Parameters["Distance"] = command.MateDistance;
                actionPlan.Steps.Add(step);
                return actionPlan;
            }

            if (command.Intent == SkillNames.ReplaceComponent)
            {
                var actionPlan = new TaskPlan { Goal = "Modify Assembly" };
                var step = new PlanStep { Index = 1, SkillName = SkillNames.ReplaceComponent,
                    Description = $"Replace {command.ComponentName} with {command.ReplacementPath}" };
                step.Parameters["ComponentName"] = command.ComponentName;
                step.Parameters["NewPath"] = command.ReplacementPath;
                actionPlan.Steps.Add(step);
                return actionPlan;
            }

            if (command.Intent == SkillNames.InsertComponent)
            {
                var actionPlan = new TaskPlan { Goal = "Modify Assembly" };
                var step = new PlanStep { Index = 1, SkillName = SkillNames.InsertComponent,
                    Description = $"Insert {command.ComponentPath} at X={command.PositionX:0.###}, Y={command.PositionY:0.###}, Z={command.PositionZ:0.###} mm" };
                step.Parameters["Path"] = command.ComponentPath;
                step.Parameters["X"] = command.PositionX;
                step.Parameters["Y"] = command.PositionY;
                step.Parameters["Z"] = command.PositionZ;
                actionPlan.Steps.Add(step);
                return actionPlan;
            }

            if (command.Intent == SkillNames.MoveComponent)
            {
                var actionPlan = new TaskPlan { Goal = "Modify Assembly" };
                var step = new PlanStep { Index = 1, SkillName = SkillNames.MoveComponent,
                    Description = $"Move {command.ComponentName} to X={command.PositionX:0.###}, Y={command.PositionY:0.###}, Z={command.PositionZ:0.###} mm" };
                step.Parameters["ComponentName"] = command.ComponentName;
                step.Parameters["X"] = command.PositionX;
                step.Parameters["Y"] = command.PositionY;
                step.Parameters["Z"] = command.PositionZ;
                actionPlan.Steps.Add(step);
                return actionPlan;
            }

            if (command.Intent == SkillNames.InsertSolidWorksBOM)
            {
                var nativeBomPlan = new TaskPlan { Goal = "Insert native SOLIDWORKS BOM" };
                nativeBomPlan.Steps.Add(new PlanStep { Index = 1, SkillName = SkillNames.InsertSolidWorksBOM, Description = "Insert native SOLIDWORKS BOM table into active Assembly" });
                return nativeBomPlan;
            }

            if (command.Intent == SkillNames.CreateBOM)
            {
                var bomPlan = new TaskPlan { Goal = "Create Assembly BOM" };
                var step = new PlanStep { Index = 1, SkillName = SkillNames.CreateBOM, Description = "Build BOM from active Assembly components" };
                step.Parameters["ExportExcel"] = command.BomExportExcel;
                step.Parameters["ExportCsv"] = command.BomExportCsv;
                if (!string.IsNullOrWhiteSpace(command.BomOutputFolder)) step.Parameters["OutputFolder"] = command.BomOutputFolder;
                bomPlan.Steps.Add(step);
                return bomPlan;
            }

            if (command.Intent == SkillNames.ApplyStockMaterials)
            {
                var materialPlan = new TaskPlan { Goal = "Apply stock materials from related documents" };
                materialPlan.Steps.Add(new PlanStep
                {
                    Index = 1,
                    SkillName = SkillNames.ApplyStockMaterials,
                    Description = "Read Excel/CSV/PDF material sources and write Stock Material custom properties to matched Parts"
                });
                return materialPlan;
            }

            if (command.Intent == SkillNames.ExportManufacturingBreakdown)
            {
                var exportPlan = new TaskPlan { Goal = "Export stock-material table" };
                var step = new PlanStep { Index = 1, SkillName = SkillNames.ExportManufacturingBreakdown,
                    Description = "Read related material documents, calculate stock by configured rules, capture images and export Excel" };
                if (!string.IsNullOrWhiteSpace(command.ExportPath)) step.Parameters["OutputPath"] = command.ExportPath;
                exportPlan.Steps.Add(step);
                return exportPlan;
            }

            if (command.Intent == SkillNames.BuildManufacturingBreakdown)
            {
                var breakdownPlan = new TaskPlan { Goal = "Build manufacturing breakdown" };
                var step = new PlanStep { Index = 1, SkillName = SkillNames.BuildManufacturingBreakdown,
                    Description = "Scan Assembly, read stock material documents and calculate stock by configured rules" };
                breakdownPlan.Steps.Add(step);
                return breakdownPlan;
            }

            if (command.Intent == SkillNames.AnalyzeFeatureImpact)
            {
                var impactPlan = new TaskPlan { Goal = "Analyze feature change impact" };
                var impactStep = new PlanStep
                {
                    Index = 1,
                    SkillName = SkillNames.AnalyzeFeatureImpact,
                    Description = $"Find downstream dependencies of {command.TargetFeatureName}"
                };
                impactStep.Parameters["FeatureName"] = command.TargetFeatureName;
                impactPlan.Steps.Add(impactStep);
                return impactPlan;
            }

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
                   intent == SkillNames.ReadFeatureDependencies || intent == SkillNames.AnalyzeFeatureImpact ||
                   intent == SkillNames.ReadSketches || intent == SkillNames.ReadDimensions ||
                   intent == SkillNames.ReadMaterial || intent == SkillNames.ReadMassProperties ||
                   intent == SkillNames.ReadCustomProperties || intent == SkillNames.ReadSelectedObject ||
                   intent == SkillNames.ReadBoundingBox || intent == SkillNames.ReadAssembly ||
                   intent == SkillNames.ReadComponents || intent == SkillNames.ReadMates ||
                   intent == SkillNames.CheckInterference || intent == SkillNames.BuildManufacturingBreakdown ||
                   intent == SkillNames.ExportManufacturingBreakdown || intent == SkillNames.CreateBOM;
        }
    }
}
