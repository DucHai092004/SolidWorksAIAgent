using System;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SwMateAI.Core.Tools.CAD
{
    public class CutExtrudeTool : SwToolBase
    {
        public override string Name => "CutExtrude";
        public override string Description => "Creates a blind cut from the active sketch. Depth is in millimeters.";
        public CutExtrudeTool(ISldWorks swApp) : base(swApp) { }

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
                double depthMm = GetPositiveDouble(parameters, "Depth", 10);
                var model = SwApp.ActiveDoc as IModelDoc2;
                model.SketchManager.InsertSketch(true);
                var feature = model.FeatureManager.FeatureCut3(
                    true, false, false,
                    (int)swEndConditions_e.swEndCondBlind,
                    (int)swEndConditions_e.swEndCondBlind,
                    depthMm / 1000.0, 0.0,
                    false, false, false, false,
                    0.0, 0.0,
                    false, false, false, false,
                    false, true, true, true, true,
                    false, 0.0, 0.0, false);
                if (feature == null) return ToolResult.Error("SOLIDWORKS did not create Cut-Extrude.");
                model.ViewZoomtofit2();
                return ToolResult.Success($"Cut-Extrude created: {depthMm:0.###} mm");
            }
            catch (Exception ex) { return ToolResult.Error($"CutExtrude failed: {ex.Message}"); }
        }

        private static double GetPositiveDouble(Dictionary<string, object> p, string key, double fallback)
        {
            if (!p.TryGetValue(key, out var raw) || raw == null) return fallback;
            double value = Convert.ToDouble(raw, System.Globalization.CultureInfo.InvariantCulture);
            if (value <= 0) throw new ArgumentOutOfRangeException(key, "Value must be greater than zero.");
            return value;
        }
    }
}