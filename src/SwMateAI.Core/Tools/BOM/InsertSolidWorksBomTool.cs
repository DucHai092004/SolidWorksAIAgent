using System;
using System.Collections.Generic;
using System.IO;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SwMateAI.Core.BOM;

namespace SwMateAI.Core.Tools.BOM
{
    public class InsertSolidWorksBomTool : SwToolBase, IUndoableSwTool
    {
        private string _lastFeatureName = string.Empty;
        public InsertSolidWorksBomTool(ISldWorks swApp) : base(swApp) { }
        public override string Name => "InsertSolidWorksBOM";
        public override string Description => "Inserts a native SOLIDWORKS BOM table into the active Assembly.";

        public override bool CanExecute(out string reason)
        {
            reason = null;
            var model = SwApp.ActiveDoc as IModelDoc2;
            if (model == null) { reason = "No active SOLIDWORKS document."; return false; }
            if (model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
            { reason = "The active document must be an Assembly."; return false; }
            return true;
        }

        public override ToolResult Execute(Dictionary<string, object> parameters)
        {
            var model = SwApp.ActiveDoc as IModelDoc2;
            if (model == null) return ToolResult.Error("No active Assembly.");
            string template = Text(parameters, "TemplatePath");
            if (string.IsNullOrWhiteSpace(template))
                template = @"C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS\lang\english\bom-standard.sldbomtbt";
            if (!File.Exists(template)) return ToolResult.Error("BOM template not found: " + template);
            string configuration = model.ConfigurationManager.ActiveConfiguration?.Name ?? string.Empty;

            int bomType = (int)swBomType_e.swBomType_Indented;
            int numbering = (int)swNumberingType_e.swNumberingType_Detailed;
            var annotation = model.Extension.InsertBomTable3(
                template, 0, 0, bomType, configuration, false, numbering, true) as IBomTableAnnotation;
            if (annotation == null) return ToolResult.Error("SOLIDWORKS did not create the BOM table.");

            var feature = annotation.BomFeature?.GetFeature();
            if (feature == null) return ToolResult.Error("BOM table was created but its feature could not be resolved.");
            _lastFeatureName = feature.Name ?? string.Empty;
            model.EditRebuild3();
            return ToolResult.Success(new SolidWorksBomResult
            {
                FeatureName = _lastFeatureName,
                Configuration = configuration,
                TemplatePath = template,
                BomType = "Indented"
            });
        }

        public ToolResult Undo()
        {
            var model = SwApp.ActiveDoc as IModelDoc2;
            if (model == null || string.IsNullOrWhiteSpace(_lastFeatureName))
                return ToolResult.Error("No native BOM insertion is available to undo.");
            var feature = FindFeature(model, _lastFeatureName);
            if (feature == null) return ToolResult.Error("The inserted BOM feature no longer exists.");
            model.ClearSelection2(true);
            if (!feature.Select2(false, 0)) return ToolResult.Error("Could not select the inserted BOM feature for Undo.");
            model.EditDelete();
            model.EditRebuild3();
            if (FindFeature(model, _lastFeatureName) != null) return ToolResult.Error("BOM feature still exists after Undo.");
            string deleted = _lastFeatureName;
            _lastFeatureName = string.Empty;
            return ToolResult.Success("Removed native SOLIDWORKS BOM: " + deleted);
        }

        private static string Text(Dictionary<string, object> input, string key)
        {
            return input != null && input.TryGetValue(key, out var raw) ? Convert.ToString(raw)?.Trim() ?? string.Empty : string.Empty;
        }

        private static IFeature FindFeature(IModelDoc2 model, string name)
        {
            var feature = model?.FirstFeature() as IFeature;
            while (feature != null)
            {
                var found = FindRecursive(feature, name);
                if (found != null) return found;
                feature = feature.GetNextFeature() as IFeature;
            }
            return null;
        }

        private static IFeature FindRecursive(IFeature feature, string name)
        {
            if (feature == null) return null;
            if (string.Equals(feature.Name, name, StringComparison.OrdinalIgnoreCase)) return feature;
            var sub = feature.GetFirstSubFeature() as IFeature;
            while (sub != null)
            {
                var found = FindRecursive(sub, name);
                if (found != null) return found;
                sub = sub.GetNextSubFeature() as IFeature;
            }
            return null;
        }
    }
}

