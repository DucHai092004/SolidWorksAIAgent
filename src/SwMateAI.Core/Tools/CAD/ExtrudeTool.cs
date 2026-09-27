using System;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SwMateAI.Core.Tools.CAD
{
    public class ExtrudeTool : SwToolBase
    {
        public override string Name => "Extrude";
        public override string Description => "Creates a blind boss extrude from the active sketch. Depth is in millimeters.";

        public ExtrudeTool(ISldWorks swApp) : base(swApp) { }

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
                double depthMm = GetPositiveDouble(parameters, "Depth", 20.0);
                double depthM = depthMm / 1000.0;
                var model = SwApp.ActiveDoc as IModelDoc2;

                model.SketchManager.InsertSketch(true);

                var feature = model.FeatureManager.FeatureExtrusion2(
                    true, false, false,
                    (int)swEndConditions_e.swEndCondBlind,
                    (int)swEndConditions_e.swEndCondBlind,
                    depthM, 0.0,
                    false, false, false, false,
                    0.0, 0.0,
                    false, false, false, false,
                    true, true, true,
                    (int)swStartConditions_e.swStartSketchPlane,
                    0.0, false);

                if (feature == null) return ToolResult.Error("SOLIDWORKS did not create the Boss-Extrude.");
                model.ViewZoomtofit2();
                return ToolResult.Success($"Boss-Extrude created: {depthMm:0.###} mm");
            }
            catch (Exception ex) { return ToolResult.Error($"Extrude failed: {ex.Message}"); }
        }

        private static double GetPositiveDouble(Dictionary<string, object> parameters, string key, double fallback)
        {
            if (!parameters.TryGetValue(key, out var raw) || raw == null) return fallback;
            double value = Convert.ToDouble(raw, System.Globalization.CultureInfo.InvariantCulture);
            if (value <= 0) throw new ArgumentOutOfRangeException(key, "Value must be greater than zero.");
            return value;
        }
    }
}
