using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SwMateAI.Core.Drawing;

namespace SwMateAI.Core.Tools.Drawing
{
    /// <summary>
    /// Phase 6 Drawing Automation: inserts an isometric model view
    /// of the source Part/Assembly into the active Drawing sheet.
    /// </summary>
    public class InsertIsometricViewTool : SwToolBase
    {
        private readonly DrawingSessionContext _session;

        public InsertIsometricViewTool(ISldWorks swApp, DrawingSessionContext session) : base(swApp)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public override string Name => "InsertIsometricView";
        public override string Description =>
            "Inserts an isometric drawing view of the source model into the active Drawing sheet.";
        public override bool CanExecute(out string reason)
        {
            reason = null;
            var model = SwApp.ActiveDoc as IModelDoc2;
            if (model == null)
            {
                reason = "Open a Drawing before inserting an isometric view.";
                return false;
            }

            if (model.GetType() != (int)swDocumentTypes_e.swDocDRAWING)
            {
                reason = "The active document must be a Drawing.";
                return false;
            }

            return true;
        }

        public override ToolResult Execute(Dictionary<string, object> parameters)
        {
            try
            {
                var drawing = SwApp.ActiveDoc as IDrawingDoc;
                if (drawing == null)
                    return ToolResult.Error("The active document is not a Drawing.");

                string modelPath = GetString(parameters, "ModelPath");
                if (string.IsNullOrWhiteSpace(modelPath)) modelPath = _session.SourceModelPath;
                if (string.IsNullOrWhiteSpace(modelPath))
                    return ToolResult.Error("Save the source Part/Assembly first, or provide ModelPath.");
                if (!File.Exists(modelPath))
                    return ToolResult.Error("Source model file was not found: " + modelPath);
                var sheet = drawing.GetCurrentSheet() as ISheet;
                if (sheet == null)
                    return ToolResult.Error("The active Drawing has no current sheet.");

                double widthM = 0, heightM = 0;
                sheet.GetSize(ref widthM, ref heightM);
                double defaultXmm = widthM > 0 ? widthM * 1000.0 * 0.75 : 300.0;
                double defaultYmm = heightM > 0 ? heightM * 1000.0 * 0.70 : 200.0;
                double xmm = GetDouble(parameters, "Xmm", defaultXmm);
                double ymm = GetDouble(parameters, "Ymm", defaultYmm);
                if (xmm <= 0 || ymm <= 0)
                    return ToolResult.Error("Isometric view X/Y positions must be greater than zero.");

                int beforeCount = CountModelViews(drawing);
                var view = drawing.CreateDrawViewFromModelView3(
                    modelPath,
                    "*Isometric",
                    xmm / 1000.0,
                    ymm / 1000.0,
                    0.0);

                if (view == null)
                    return ToolResult.Error("SOLIDWORKS did not create the isometric view.");

                int afterCount = CountModelViews(drawing);
                if (afterCount <= beforeCount)
                    return ToolResult.Error("Drawing model view count did not increase.");

                string viewName = view.GetName2();
                return ToolResult.Success(new DrawingIsometricViewResult
                {
                    ModelPath = modelPath,
                    ViewName = viewName,
                    Xmm = xmm,
                    Ymm = ymm,
                    TotalModelViewCount = afterCount
                });
            }
            catch (Exception ex)
            {
                return ToolResult.Error("InsertIsometricView failed: " + ex.Message);
            }
        }

        private static int CountModelViews(IDrawingDoc drawing)
        {
            int count = 0;
            var sheetView = drawing.GetFirstView() as IView;
            var view = sheetView?.GetNextView() as IView;
            while (view != null)
            {
                count++;
                view = view.GetNextView() as IView;
            }
            return count;
        }

        private static string GetString(Dictionary<string, object> parameters, string key)
        {
            if (parameters == null || !parameters.TryGetValue(key, out var value) || value == null)
                return string.Empty;
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }
        private static double GetDouble(Dictionary<string, object> parameters, string key, double fallback)
        {
            if (parameters == null || !parameters.TryGetValue(key, out var value) || value == null)
                return fallback;
            if (value is double d) return d;
            if (value is float f) return f;
            if (value is int i) return i;
            if (double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture),
                NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                return parsed;
            if (double.TryParse(Convert.ToString(value, CultureInfo.CurrentCulture),
                NumberStyles.Float, CultureInfo.CurrentCulture, out parsed))
                return parsed;
            return fallback;
        }
    }
}
