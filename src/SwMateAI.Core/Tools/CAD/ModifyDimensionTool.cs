using System;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SwMateAI.Core.Tools.CAD
{
    public class ModifyDimensionTool : SwToolBase
    {
        public override string Name => "ModifyDimension";
        public override string Description => "Changes a driving dimension by full name or by selecting a dimension in SOLIDWORKS.";
        public ModifyDimensionTool(ISldWorks swApp) : base(swApp) { }

        public override bool CanExecute(out string reason)
        {
            reason = null;
            var model = SwApp.ActiveDoc as IModelDoc2;
            if (model == null) { reason = "No active SOLIDWORKS document."; return false; }
            if (model.GetType() != (int)swDocumentTypes_e.swDocPART) { reason = "The active document must be a Part."; return false; }
            return true;
        }

        public override ToolResult Execute(Dictionary<string, object> parameters)
        {
            try
            {
                var model = SwApp.ActiveDoc as IModelDoc2;
                double valueMm = Positive(parameters, "Value", 10.0);
                string name = Text(parameters, "Name");

                IDimension dimension = null;
                if (!string.IsNullOrWhiteSpace(name))
                    dimension = model.Parameter(name) as IDimension;

                if (dimension == null)
                    dimension = GetSelectedDimension(model);

                if (dimension == null)
                    return ToolResult.Error("Dimension not found. Enter a full name such as D1@Sketch1 or select a dimension first.");

                dimension.SystemValue = valueMm / 1000.0;
                if (!model.EditRebuild3()) return ToolResult.Error("Dimension value changed but SOLIDWORKS rebuild failed.");

                return ToolResult.Success($"Dimension modified: {dimension.FullName} = {valueMm:0.###} mm.");
            }
            catch (Exception ex) { return ToolResult.Error($"ModifyDimension failed: {ex.Message}"); }
        }
        private static IDimension GetSelectedDimension(IModelDoc2 model)
        {
            var sel = model.SelectionManager as ISelectionMgr;
            if (sel == null || sel.GetSelectedObjectCount2(-1) < 1) return null;

            object selected = sel.GetSelectedObject6(1, -1);
            if (selected is IDisplayDimension display) return display.GetDimension2(0);
            if (selected is IDimension dimension) return dimension;
            return null;
        }

        private static string Text(Dictionary<string, object> p, string key)
        {
            if (!p.TryGetValue(key, out var raw) || raw == null) return string.Empty;
            return Convert.ToString(raw, System.Globalization.CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
        }

        private static double Positive(Dictionary<string, object> p, string key, double fallback)
        {
            if (!p.TryGetValue(key, out var raw) || raw == null) return fallback;
            double value = Convert.ToDouble(raw, System.Globalization.CultureInfo.InvariantCulture);
            if (value <= 0) throw new ArgumentOutOfRangeException(key, "Value must be greater than zero.");
            return value;
        }
    }
}
