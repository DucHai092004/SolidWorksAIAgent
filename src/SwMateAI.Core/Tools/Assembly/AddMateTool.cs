using System;
using System.Collections.Generic;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SwMateAI.Core.Models.Assembly;
using SwMateAI.Core.Readers;

namespace SwMateAI.Core.Tools.Assembly
{
    public class AddMateTool : AssemblyActionToolBase, IUndoableSwTool
    {
        private string _lastMateName = string.Empty;

        public AddMateTool(ISldWorks swApp) : base(swApp) { }
        public override string Name => "AddMate";
        public override string Description => "Adds a mate between the currently selected Assembly entities.";

        public override ToolResult Execute(Dictionary<string, object> parameters)
        {
            var model = SwApp.ActiveDoc as IModelDoc2;
            var assembly = model as IAssemblyDoc;
            var selection = model?.SelectionManager as ISelectionMgr;
            if (assembly == null || model == null) return ToolResult.Error("Active document is not an Assembly.");
            if ((selection?.GetSelectedObjectCount2(-1) ?? 0) < 2)
                return ToolResult.Error("Select two Assembly entities before AddMate.");

            string requestedType = Text(parameters, "MateType");
            int mateType = ResolveMateType(requestedType);
            double distanceMm = Mm(parameters, "Distance");
            if (mateType == (int)swMateType_e.swMateDISTANCE && distanceMm <= 0)
                return ToolResult.Error("Distance mate requires Distance > 0 mm.");
            var reader = new SolidWorksAssemblyReader(SwApp);
            var beforeNames = new HashSet<string>(reader.ReadMates().Select(x => x.Name), StringComparer.OrdinalIgnoreCase);
            int errorStatus;
            var mate = assembly.AddMate5(
                mateType,
                (int)swMateAlign_e.swMateAlignCLOSEST,
                false,
                distanceMm / 1000.0,
                0, 0, 1, 1,
                0, 0, 0,
                false, false, 0,
                out errorStatus);
            if (mate == null || errorStatus != 0)
                return ToolResult.Error("SOLIDWORKS could not create the mate. Error status: " + errorStatus);

            model.EditRebuild3();
            var after = reader.ReadMates();
            var created = after.FirstOrDefault(x => !beforeNames.Contains(x.Name));
            if (created == null)
                return ToolResult.Error("Mate was created but its Feature could not be identified for validation/undo.");

            _lastMateName = created.Name;
            return ToolResult.Success(new AssemblyMateActionResult
            {
                Action = "AddMate",
                MateName = created.Name,
                MateType = created.TypeName,
                DistanceMm = distanceMm
            });
        }
        public ToolResult Undo()
        {
            if (string.IsNullOrWhiteSpace(_lastMateName))
                return ToolResult.Error("There is no AddMate action to undo.");

            var model = SwApp.ActiveDoc as IModelDoc2;
            var assembly = model as IAssemblyDoc;
            var feature = FindFeature(model, _lastMateName);
            if (model == null || assembly == null || feature == null)
                return ToolResult.Error("Created Mate feature could not be found for Undo.");

            model.ClearSelection2(true);
            if (!feature.Select2(false, 0))
                return ToolResult.Error("Could not select created Mate for Undo.");
            if (!assembly.DeleteSelections(0))
                return ToolResult.Error("SOLIDWORKS could not delete the created Mate during Undo.");

            model.EditRebuild3();
            string deleted = _lastMateName;
            _lastMateName = string.Empty;
            return ToolResult.Success("Undo AddMate: removed " + deleted);
        }

        private static IFeature FindFeature(IModelDoc2 model, string name)
        {
            var feature = model?.FirstFeature() as IFeature;
            while (feature != null)
            {
                var found = FindFeatureRecursive(feature, name);
                if (found != null) return found;
                feature = feature.GetNextFeature() as IFeature;
            }
            return null;
        }

        private static IFeature FindFeatureRecursive(IFeature feature, string name)
        {
            if (feature == null) return null;
            if (string.Equals(feature.Name, name, StringComparison.OrdinalIgnoreCase)) return feature;
            var sub = feature.GetFirstSubFeature() as IFeature;
            while (sub != null)
            {
                var found = FindFeatureRecursive(sub, name);
                if (found != null) return found;
                sub = sub.GetNextSubFeature() as IFeature;
            }
            return null;
        }

        private static int ResolveMateType(string value)
        {
            string s = (value ?? string.Empty).Trim().ToLowerInvariant();
            if (s.Contains("concentric") || s.Contains("đồng tâm") || s.Contains("dong tam")) return (int)swMateType_e.swMateCONCENTRIC;
            if (s.Contains("parallel") || s.Contains("song song")) return (int)swMateType_e.swMatePARALLEL;
            if (s.Contains("perpendicular") || s.Contains("vuông góc") || s.Contains("vuong goc")) return (int)swMateType_e.swMatePERPENDICULAR;
            if (s.Contains("tangent") || s.Contains("tiếp tuyến") || s.Contains("tiep tuyen")) return (int)swMateType_e.swMateTANGENT;
            if (s.Contains("distance") || s.Contains("khoảng cách") || s.Contains("khoang cach")) return (int)swMateType_e.swMateDISTANCE;
            return (int)swMateType_e.swMateCOINCIDENT;
        }
    }
}
