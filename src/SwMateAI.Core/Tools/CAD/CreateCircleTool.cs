using System;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SwMateAI.Core.Tools.CAD
{
    public class CreateCircleTool : SwToolBase
    {
        public override string Name => "CreateCircle";
        public override string Description => "Creates a circle in the active sketch. X, Y and Diameter are in millimeters.";
        public CreateCircleTool(ISldWorks swApp) : base(swApp) { }

        public override bool CanExecute(out string reason)
        {
            reason = null;
            var model = SwApp.ActiveDoc as IModelDoc2;
            if (model == null) { reason = "No active SOLIDWORKS document."; return false; }
            if (model.GetType() != (int)swDocumentTypes_e.swDocPART) { reason = "The active document must be a Part."; return false; }
            if (model.SketchManager.ActiveSketch == null) { reason = "No sketch is currently being edited."; return false; }
            return true;
        }

        public override ToolResult Execute(Dictionary<string, object> parameters)
        {
            try
            {
                double x = GetDouble(parameters, "X", 0) / 1000.0;
                double y = GetDouble(parameters, "Y", 0) / 1000.0;
                double diameter = GetPositiveDouble(parameters, "Diameter", 10);
                double radius = diameter / 2000.0;
                var model = SwApp.ActiveDoc as IModelDoc2;
                var segment = model.SketchManager.CreateCircleByRadius(x, y, 0, radius);
                if (segment == null) return ToolResult.Error("SOLIDWORKS did not create the circle.");
                return ToolResult.Success($"Circle created: Ø{diameter:0.###} mm at X={x * 1000:0.###}, Y={y * 1000:0.###} mm");
            }
            catch (Exception ex) { return ToolResult.Error($"CreateCircle failed: {ex.Message}"); }
        }

        private static double GetDouble(Dictionary<string, object> p, string key, double fallback)
        {
            if (!p.TryGetValue(key, out var raw) || raw == null) return fallback;
            return Convert.ToDouble(raw, System.Globalization.CultureInfo.InvariantCulture);
        }
        private static double GetPositiveDouble(Dictionary<string, object> p, string key, double fallback)
        {
            double value = GetDouble(p, key, fallback);
            if (value <= 0) throw new ArgumentOutOfRangeException(key, "Value must be greater than zero.");
            return value;
        }
    }
}