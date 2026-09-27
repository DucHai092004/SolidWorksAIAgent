using System;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SwMateAI.Core.Tools.CAD
{
    public class CreateRectangleTool : SwToolBase
    {
        public override string Name => "CreateRectangle";
        public override string Description => "Creates a centered rectangle in the active sketch. Width and Height are in millimeters.";

        public CreateRectangleTool(ISldWorks swApp) : base(swApp) { }

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
                double widthMm = GetPositiveDouble(parameters, "Width", 60.0);
                double heightMm = GetPositiveDouble(parameters, "Height", 40.0);
                double halfW = widthMm / 2000.0;
                double halfH = heightMm / 2000.0;

                var model = SwApp.ActiveDoc as IModelDoc2;
                object segments = model.SketchManager.CreateCornerRectangle(-halfW, -halfH, 0, halfW, halfH, 0);
                if (segments == null) return ToolResult.Error("SOLIDWORKS did not create the rectangle.");

                return ToolResult.Success($"Rectangle created: {widthMm:0.###} x {heightMm:0.###} mm");
            }
            catch (Exception ex) { return ToolResult.Error($"CreateRectangle failed: {ex.Message}"); }
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
