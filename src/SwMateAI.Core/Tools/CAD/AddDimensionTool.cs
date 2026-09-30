using System;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SwMateAI.Core.Tools.CAD
{
    public class AddDimensionTool : SwToolBase
    {
        public override string Name => "AddDimension";
        public override string Description => "Adds a driving dimension to the currently selected sketch entity/entities.";
        public AddDimensionTool(ISldWorks swApp) : base(swApp) { }

        public override bool CanExecute(out string reason)
        {
            reason = null;
            var model = SwApp.ActiveDoc as IModelDoc2;
            if (model == null) { reason = "No active SOLIDWORKS document."; return false; }
            if (model.GetType() != (int)swDocumentTypes_e.swDocPART) { reason = "The active document must be a Part."; return false; }
            if (model.SketchManager.ActiveSketch == null) { reason = "A sketch must be actively edited."; return false; }
            var sel = model.SelectionManager as ISelectionMgr;
            if (sel == null || sel.GetSelectedObjectCount2(-1) < 1) { reason = "Select one or more sketch entities first."; return false; }
            return true;
        }

        public override ToolResult Execute(Dictionary<string, object> parameters)
        {
            try
            {
                var model = SwApp.ActiveDoc as IModelDoc2;
                double valueMm = Positive(parameters, "Value", 10.0);
                double x = Number(parameters, "X", 30.0) / 1000.0;
                double y = Number(parameters, "Y", 30.0) / 1000.0;
                double z = Number(parameters, "Z", 0.0) / 1000.0;

                var display = model.AddDimension2(x, y, z) as IDisplayDimension;
                if (display == null) return ToolResult.Error("SOLIDWORKS did not create the dimension. Check the selected sketch entities.");

                var dimension = display.GetDimension2(0);
                if (dimension == null) return ToolResult.Error("The created display dimension has no driving dimension object.");
                dimension.SystemValue = valueMm / 1000.0;

                if (!model.EditRebuild3()) return ToolResult.Error("Dimension was created but the model did not rebuild successfully.");
                return ToolResult.Success($"Dimension added: {dimension.FullName} = {valueMm:0.###} mm.");
            }
            catch (Exception ex) { return ToolResult.Error($"AddDimension failed: {ex.Message}"); }
        }
        private static double Number(Dictionary<string, object> p, string key, double fallback)
        {
            if (!p.TryGetValue(key, out var raw) || raw == null) return fallback;
            return Convert.ToDouble(raw, System.Globalization.CultureInfo.InvariantCulture);
        }

        private static double Positive(Dictionary<string, object> p, string key, double fallback)
        {
            double value = Number(p, key, fallback);
            if (value <= 0) throw new ArgumentOutOfRangeException(key, "Value must be greater than zero.");
            return value;
        }
    }
}
