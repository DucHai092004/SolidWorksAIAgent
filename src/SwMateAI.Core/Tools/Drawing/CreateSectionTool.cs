using System;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SwMateAI.Core.Drawing;

namespace SwMateAI.Core.Tools.Drawing
{
    /// <summary>
    /// Phase 6 Drawing Automation: creates a centered section view
    /// from an existing model view on the active Drawing sheet.
    /// </summary>
    public class CreateSectionTool : SwToolBase
    {
        public CreateSectionTool(ISldWorks swApp) : base(swApp) { }

        public override string Name => "CreateSection";
        public override string Description =>
            "Creates a centered section view from a source drawing view.";

        public override bool CanExecute(out string reason)
        {
            reason = null;
            var model = SwApp.ActiveDoc as IModelDoc2;
            if (model == null || model.GetType() != (int)swDocumentTypes_e.swDocDRAWING)
            {
                reason = "Open a Drawing before creating a section view.";
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
                string direction = GetString(parameters, "Direction");
                if (string.IsNullOrWhiteSpace(direction)) direction = "Vertical";

                var sourceView = FindSourceView(drawing, requestedView);
                if (sourceView == null)
                    return ToolResult.Error("No suitable source model view was found on the active sheet.");

                var outline = sourceView.GetOutline() as double[];
                if (outline == null || outline.Length < 4)
                    return ToolResult.Error("Could not read the source view outline.");

                if (!drawing.ActivateView(sourceView.GetName2()))
                    return ToolResult.Error("Could not activate source view '" + sourceView.GetName2() + "'.");

                model.ClearSelection2(true);
                double scale = Math.Max(sourceView.ScaleDecimal, 1e-6);
                bool horizontal = IsHorizontal(direction);
                var line = CreateCenteredSectionLine(model, outline, scale, horizontal);
                if (line == null)
                    return ToolResult.Error("Could not create the section line in the source view.");
                var sheet = drawing.GetCurrentSheet() as ISheet;
                double sheetWidth = 0, sheetHeight = 0;
                sheet?.GetSize(ref sheetWidth, ref sheetHeight);
                GetSectionPlacement(outline, sheetWidth, sheetHeight, horizontal,
                    out double sectionX, out double sectionY);

                object excludedComponents = null;
                int beforeCount = CountModelViews(drawing);
                var sectionView = drawing.CreateSectionViewAt5(
                    sectionX,
                    sectionY,
                    0.0,
                    label,
                    (int)swCreateSectionViewAtOptions_e.swCreateSectionView_ScaleWithModel,
                    excludedComponents,
                    0.0);

                if (sectionView == null || sectionView.GetSection() == null)
                    return ToolResult.Error("SOLIDWORKS did not create a valid section view.");

                model.EditRebuild3();
                int afterCount = CountModelViews(drawing);
                if (afterCount <= beforeCount)
                    return ToolResult.Error("Drawing model view count did not increase.");

                return ToolResult.Success(new DrawingSectionResult
                {
                    SourceViewName = sourceView.GetName2(),
                    SectionViewName = sectionView.GetName2(),
                    Label = label,
                    Direction = horizontal ? "Horizontal" : "Vertical",
                    TotalModelViewCount = afterCount
                });
            }
            catch (Exception ex)
            {
                return ToolResult.Error("CreateSection failed: " + ex.Message);
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

        private static ISketchSegment CreateCenteredSectionLine(
            IModelDoc2 model, double[] outline, double scale, bool horizontal)
        {
            double width = Math.Max(outline[2] - outline[0], 0.01);
            double height = Math.Max(outline[3] - outline[1], 0.01);
            if (horizontal)
            {
                double half = Math.Max(width / scale * 0.65, 0.04);
                return model.SketchManager.CreateLine(-half, 0, 0, half, 0, 0);
            }

            double verticalHalf = Math.Max(height / scale * 0.65, 0.04);
            return model.SketchManager.CreateLine(0, -verticalHalf, 0, 0, verticalHalf, 0);
        }
        private static void GetSectionPlacement(
            double[] outline, double sheetWidth, double sheetHeight, bool horizontal,
            out double x, out double y)
        {
            double centerX = (outline[0] + outline[2]) / 2.0;
            double centerY = (outline[1] + outline[3]) / 2.0;
            double viewWidth = Math.Max(outline[2] - outline[0], 0.04);
            double viewHeight = Math.Max(outline[3] - outline[1], 0.04);

            if (horizontal)
            {
                x = centerX;
                y = Math.Max(0.03, centerY - viewHeight - 0.04);
            }
            else
            {
                x = centerX + viewWidth + 0.06;
                y = centerY;
            }

            if (sheetWidth > 0) x = Math.Min(Math.Max(x, 0.03), sheetWidth - 0.03);
            if (sheetHeight > 0) y = Math.Min(Math.Max(y, 0.03), sheetHeight - 0.03);
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
        private static bool IsHorizontal(string value)
        {
            string v = (value ?? string.Empty).Trim().ToLowerInvariant();
            return v == "horizontal" || v == "ngang" || v == "h";
        }

        private static string GetString(Dictionary<string, object> parameters, string key)
        {
            if (parameters == null || !parameters.TryGetValue(key, out var value) || value == null)
                return string.Empty;
            return Convert.ToString(value) ?? string.Empty;
        }
    }
}
