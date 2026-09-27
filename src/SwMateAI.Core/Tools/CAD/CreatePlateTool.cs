using System;
using System.Collections.Generic;
using System.Globalization;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SwMateAI.Core.Tools.CAD
{
    public class CreatePlateTool : SwToolBase
    {
        public override string Name => "CreatePlate";
        public override string Description => "Creates a new rectangular solid plate.";
        public CreatePlateTool(ISldWorks swApp) : base(swApp) { }

        public override bool CanExecute(out string reason)
        {
            reason = null;
            var template = SwApp.GetUserPreferenceStringValue((int)swUserPreferenceStringValue_e.swDefaultTemplatePart);
            if (!string.IsNullOrWhiteSpace(template)) return true;
            reason = "No default Part template is configured in SOLIDWORKS."; return false;
        }

        public override ToolResult Execute(Dictionary<string, object> p)
        {
            try
            {
                double w = Positive(p, "Width", 100), h = Positive(p, "Height", 60), t = Positive(p, "Thickness", 10);
                string template = SwApp.GetUserPreferenceStringValue((int)swUserPreferenceStringValue_e.swDefaultTemplatePart);
                var model = SwApp.NewDocument(template, 0, 0, 0) as IModelDoc2;
                if (model == null) return ToolResult.Error("Could not create Part.");

                IFeature plane = model.FirstFeature() as IFeature;
                while (plane != null && plane.GetTypeName2() != "RefPlane") plane = plane.GetNextFeature() as IFeature;
                if (plane == null || !plane.Select2(false, 0)) return ToolResult.Error("Could not select reference plane.");
                model.SketchManager.InsertSketch(true);
                if (model.SketchManager.CreateCornerRectangle(-w/2000.0, -h/2000.0, 0, w/2000.0, h/2000.0, 0) == null)
                    return ToolResult.Error("Could not create rectangle.");
                model.SketchManager.InsertSketch(true);
                var boss = model.FeatureManager.FeatureExtrusion2(true,false,false,
                    (int)swEndConditions_e.swEndCondBlind,(int)swEndConditions_e.swEndCondBlind,t/1000.0,0.0,
                    false,false,false,false,0.0,0.0,false,false,false,false,true,true,true,
                    (int)swStartConditions_e.swStartSketchPlane,0.0,false);
                if (boss == null) return ToolResult.Error("Could not create Boss-Extrude.");
                model.ViewZoomtofit2();
                return ToolResult.Success($"Plate created: {w:0.###} x {h:0.###} x {t:0.###} mm.");
            }
            catch (Exception ex) { return ToolResult.Error($"CreatePlate failed: {ex.Message}"); }
        }

        private static double Positive(Dictionary<string, object> p, string key, double fallback)
        {
            if (!p.TryGetValue(key, out var raw) || raw == null) return fallback;
            double v = Convert.ToDouble(raw, CultureInfo.InvariantCulture);
            if (v <= 0) throw new ArgumentOutOfRangeException(key);
            return v;
        }
    }
}