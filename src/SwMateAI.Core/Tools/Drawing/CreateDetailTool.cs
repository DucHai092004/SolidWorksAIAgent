using System;
using System.Collections.Generic;
using System.Globalization;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SwMateAI.Core.Drawing;

namespace SwMateAI.Core.Tools.Drawing
{
    /// <summary>
    /// Phase 6 Drawing Automation: creates a circular detail view
    /// from an existing model view on the active Drawing sheet.
    /// </summary>
    public class CreateDetailTool : SwToolBase
    {
        public CreateDetailTool(ISldWorks swApp) : base(swApp) { }

        public override string Name => "CreateDetail";
        public override string Description =>
            "Creates a circular detail view from a source drawing view.";

        public override bool CanExecute(out string reason)
        {
            reason = null;
            var model = SwApp.ActiveDoc as IModelDoc2;
            if (model == null || model.GetType() != (int)swDocumentTypes_e.swDocDRAWING)
            {
                reason = "Open a Drawing before creating a detail view.";
                return false;
            }
            return true;
        }
        public override ToolResult Execute(Dictionary<string, object> parameters)
        {
            try
            {
                var model = SwApp.ActiveDoc as IModelDoc2;
                var drawing = model as IDrawingDoc;
                if (model == null || drawing == null)
                    return ToolResult.Error("The active document is not a Drawing.");

                string requestedView = GetString(parameters, "SourceViewName");
                string label = GetString(parameters, "Label");
                if (string.IsNullOrWhiteSpace(label)) label = "A";
                double scaleNum = GetDouble(parameters, "ScaleNumerator", 2.0);
                double scaleDen = GetDouble(parameters, "ScaleDenominator", 1.0);
                if (scaleNum <= 0 || scaleDen <= 0)
                    return ToolResult.Error("Detail view scale values must be greater than zero.");

                var sourceView = FindSourceView(drawing, requestedView);
                if (sourceView == null)
                    return ToolResult.Error("No suitable source model view was found on the active sheet.");

                var outline = sourceView.GetOutline() as double[];
                if (outline == null || outline.Length < 4)
                    return ToolResult.Error("Could not read the source view outline.");

                if (!drawing.ActivateView(sourceView.GetName2()))
                    return ToolResult.Error("Could not activate source view '" + sourceView.GetName2() + "'.");

                model.ClearSelection2(true);
                double viewScale = Math.Max(sourceView.ScaleDecimal, 1e-6);
                double defaultRadiusM = Math.Max(
                    Math.Min(outline[2] - outline[0], outline[3] - outline[1]) /
                    viewScale * 0.22,
                    0.008);
                double radiusMm = GetDouble(parameters, "RadiusMm", defaultRadiusM * 1000.0);
                double centerXmm = GetDouble(parameters, "CenterXmm", 0.0);
                double centerYmm = GetDouble(parameters, "CenterYmm", 0.0);
                if (radiusMm <= 0)
                    return ToolResult.Error("Detail radius must be greater than zero.");

                var circle = model.SketchManager.CreateCircle(
                    centerXmm / 1000.0,
                    centerYmm / 1000.0,
                    0.0,
                    centerXmm / 1000.0 + radiusMm / 1000.0,
                    centerYmm / 1000.0,
                    0.0);
                if (circle == null)
                    return ToolResult.Error("Could not create the detail circle in the source view.");

                var sheet = drawing.GetCurrentSheet() as ISheet;
                double sheetWidth = 0, sheetHeight = 0;
                sheet?.GetSize(ref sheetWidth, ref sheetHeight);
                GetDetailPlacement(outline, sheetWidth, sheetHeight,
                    out double defaultX, out double defaultY);
                double xMm = GetDouble(parameters, "Xmm", defaultX * 1000.0);
                double yMm = GetDouble(parameters, "Ymm", defaultY * 1000.0);
                int beforeCount = CountModelViews(drawing);
                var detailView = drawing.CreateDetailViewAt4(
                    xMm / 1000.0,
                    yMm / 1000.0,
                    0.0,
                    (int)swDetViewStyle_e.swDetViewSTANDARD,
                    scaleNum,
                    scaleDen,
                    label,
                    (int)swDetCircleShowType_e.swDetCircleCIRCLE,
                    true,
                    true,
                    false,
                    5) as IView;

                if (detailView == null || detailView.GetDetail() == null)
                    return ToolResult.Error("SOLIDWORKS did not create a valid detail view.");

                model.EditRebuild3();
                int afterCount = CountModelViews(drawing);
                if (afterCount <= beforeCount)
                    return ToolResult.Error("Drawing model view count did not increase.");

                return ToolResult.Success(new DrawingDetailResult
                {
                    SourceViewName = sourceView.GetName2(),
                    DetailViewName = detailView.GetName2(),
                    Label = label,
                    RadiusMm = radiusMm,
                    ScaleNumerator = scaleNum,
                    ScaleDenominator = scaleDen,
                    TotalModelViewCount = afterCount
                });
            }
            catch (Exception ex)
            {
                return ToolResult.Error("CreateDetail failed: " + ex.Message);
            }
        }

        private static IView FindSourceView(IDrawingDoc drawing, string requestedName)
        {
            var sheetView = drawing.GetFirstView() as IView;
            var view = sheetView?.GetNextView() as IView;
            if (string.IsNullOrWhiteSpace(requestedName)) return view;
            while (view != null)
            {
                if (string.Equals(view.GetName2(), requestedName, StringComparison.OrdinalIgnoreCase))
                    return view;
                view = view.GetNextView() as IView;
            }
            return null;
        }

        private static void GetDetailPlacement(
            double[] outline, double sheetWidth, double sheetHeight,
            out double x, out double y)
        {
            double centerY = (outline[1] + outline[3]) / 2.0;
            double viewWidth = Math.Max(outline[2] - outline[0], 0.04);
            x = outline[2] + viewWidth + 0.07;
            y = centerY + 0.03;
            if (sheetWidth > 0) x = Math.Min(Math.Max(x, 0.04), sheetWidth - 0.04);
            if (sheetHeight > 0) y = Math.Min(Math.Max(y, 0.04), sheetHeight - 0.04);
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
                NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)) return parsed;
            if (double.TryParse(Convert.ToString(value, CultureInfo.CurrentCulture),
                NumberStyles.Float, CultureInfo.CurrentCulture, out parsed)) return parsed;
            return fallback;
        }
    }
}
