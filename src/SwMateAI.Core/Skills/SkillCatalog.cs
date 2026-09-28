using SwMateAI.Core.Tools;

namespace SwMateAI.Core.Skills
{
    public static class SkillCatalog
    {
        public static SkillMetadata ForTool(ISwTool tool)
        {
            var m = new SkillMetadata
            {
                ToolName = tool.Name,
                Description = tool.Description,
                Category = "CAD.Basic",
                RiskLevel = SkillRiskLevel.Low
            };

            switch (tool.Name)
            {
                case "GetModelInfo": Configure(m, SkillNames.ObserveModel, "System.Observe", SkillRiskLevel.ReadOnly); break;
                case "CreatePart": Configure(m, SkillNames.CreateNewPart, "CAD.Document", SkillRiskLevel.Low); break;
                case SkillNames.CreateSketch: Configure(m, SkillNames.CreateSketch, "CAD.Sketch", SkillRiskLevel.Low, part: true); break;
                case SkillNames.CreateRectangle: Configure(m, SkillNames.CreateRectangle, "CAD.Sketch", SkillRiskLevel.Low, part: true); break;
                case SkillNames.CreateCircle: Configure(m, SkillNames.CreateCircle, "CAD.Sketch", SkillRiskLevel.Low, part: true); break;
                case "Extrude": Configure(m, SkillNames.CreateExtrude, "CAD.Feature", SkillRiskLevel.Medium, part: true); break;
                case "CutExtrude": Configure(m, SkillNames.CreateExtrudeCut, "CAD.Feature", SkillRiskLevel.Medium, part: true); break;
                case SkillNames.AddDimension: Configure(m, SkillNames.AddDimension, "CAD.Dimension", SkillRiskLevel.Medium, part: true, selection: true); break;
                case SkillNames.ModifyDimension: Configure(m, SkillNames.ModifyDimension, "CAD.Dimension", SkillRiskLevel.Medium, part: true); break;
                case "FilletPlateCorners": Configure(m, SkillNames.CreateFillet, "CAD.Feature", SkillRiskLevel.Medium, part: true); break;
                case "ChamferPlateCorners": Configure(m, SkillNames.CreateChamfer, "CAD.Feature", SkillRiskLevel.Medium, part: true); break;
                case SkillNames.CreatePlate: Configure(m, SkillNames.CreatePlate, "Workflow.Composite", SkillRiskLevel.Medium, composite: true); break;
                case SkillNames.CreatePlateWithHole: Configure(m, SkillNames.CreatePlateWithHole, "Workflow.Composite", SkillRiskLevel.Medium, composite: true); break;
                case SkillNames.ReadFeatureTree: Configure(m, SkillNames.ReadFeatureTree, "ModelReader", SkillRiskLevel.ReadOnly, document: true); break;
                case SkillNames.ReadFeatures: Configure(m, SkillNames.ReadFeatures, "ModelReader", SkillRiskLevel.ReadOnly, document: true); break;
                case SkillNames.ReadFeatureDependencies: Configure(m, SkillNames.ReadFeatureDependencies, "ModelReader.DesignIntent", SkillRiskLevel.ReadOnly, document: true); break;
                case SkillNames.AnalyzeFeatureImpact: Configure(m, SkillNames.AnalyzeFeatureImpact, "ModelReader.DesignIntent", SkillRiskLevel.ReadOnly, document: true); break;
                case SkillNames.ReadSketches: Configure(m, SkillNames.ReadSketches, "ModelReader", SkillRiskLevel.ReadOnly, document: true); break;
                case SkillNames.ReadDimensions: Configure(m, SkillNames.ReadDimensions, "ModelReader", SkillRiskLevel.ReadOnly, document: true); break;
                case SkillNames.ReadMaterial: Configure(m, SkillNames.ReadMaterial, "ModelReader", SkillRiskLevel.ReadOnly, part: true); break;
                case SkillNames.ReadMassProperties: Configure(m, SkillNames.ReadMassProperties, "ModelReader", SkillRiskLevel.ReadOnly, part: true); break;
                case SkillNames.ReadCustomProperties: Configure(m, SkillNames.ReadCustomProperties, "ModelReader", SkillRiskLevel.ReadOnly, document: true); break;
                case SkillNames.ReadSelectedObject: Configure(m, SkillNames.ReadSelectedObject, "ModelReader", SkillRiskLevel.ReadOnly, document: true); break;
                case SkillNames.ReadBoundingBox: Configure(m, SkillNames.ReadBoundingBox, "ModelReader", SkillRiskLevel.ReadOnly, part: true); break;
                case SkillNames.ReadAssembly: Configure(m, SkillNames.ReadAssembly, "Assembly.Reader", SkillRiskLevel.ReadOnly, document: true); break;
                case SkillNames.ReadComponents: Configure(m, SkillNames.ReadComponents, "Assembly.Reader", SkillRiskLevel.ReadOnly, document: true); break;
                case SkillNames.ReadMates: Configure(m, SkillNames.ReadMates, "Assembly.Reader", SkillRiskLevel.ReadOnly, document: true); break;
                case SkillNames.CheckInterference: Configure(m, SkillNames.CheckInterference, "Assembly.Validation", SkillRiskLevel.ReadOnly, assembly: true); break;
                case SkillNames.InsertComponent: Configure(m, SkillNames.InsertComponent, "Assembly.Action", SkillRiskLevel.Medium, assembly: true, confirm: true, undo: true); break;
                case SkillNames.MoveComponent: Configure(m, SkillNames.MoveComponent, "Assembly.Action", SkillRiskLevel.Medium, assembly: true, confirm: true, undo: true); break;
                default: m.Name = tool.Name; break;
            }

            if (m.Name != tool.Name) m.Aliases.Add(tool.Name);
            return m;
        }

        private static void Configure(
            SkillMetadata m,
            string name,
            string category,
            SkillRiskLevel risk,
            bool part = false,
            bool document = false,
            bool selection = false,
            bool composite = false,
            bool assembly = false,
            bool confirm = false,
            bool undo = false)
        {
            m.Name = name;
            m.Category = category;
            m.RiskLevel = risk;
            m.RequiresPartDocument = part;
            m.RequiresActiveDocument = part || assembly || document;
            m.RequiresAssemblyDocument = assembly;
            m.RequiresSelection = selection;
            m.IsComposite = composite;
            m.RequiresConfirmation = confirm;
            m.SupportsUndo = undo;
        }
    }
}
