using System;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SwMateAI.Core.Drawing;

namespace SwMateAI.Core.Tools.Drawing
{
    /// <summary>Imports dimensions marked for drawing from the model into a drawing view.</summary>
    public class InsertDimensionsTool : SwToolBase
    {
        public InsertDimensionsTool(ISldWorks swApp) : base(swApp) { }
        public override string Name => "InsertDimensions";
        public override string Description => "Imports model dimensions marked for drawing into a selected or first model view.";

        public override bool CanExecute(out string reason)
        {
            reason = null;
            var model = SwApp.ActiveDoc as IModelDoc2;
            if (model == null || model.GetType() != (int)swDocumentTypes_e.swDocDRAWING)
            { reason = "Open a Drawing before inserting dimensions."; return false; }
            return true;
        }

        public override ToolResult Execute(Dictionary<string, object> parameters)
        {
            try
            {
                var model = SwApp.ActiveDoc as IModelDoc2;
                var drawing = model as IDrawingDoc;
                if (drawing == null) return ToolResult.Error("The active document is not a Drawing.");

                string requested = Text(parameters, "ViewName");
                var view = FindView(drawing, requested);
                if (view == null) return ToolResult.Error("No model drawing view was found.");

                model.ClearSelection2(true);
                bool selected = model.Extension.SelectByID2(
                    view.GetName2(), "DRAWINGVIEW", 0, 0, 0, false, 0, null, 0);
                if (!selected) return ToolResult.Error("Could not select drawing view '" + view.GetName2() + "'.");

                int before = CountDimensions(drawing);
                object raw = drawing.InsertModelAnnotations3(
                    (int)swImportModelItemsSource_e.swImportModelItemsFromEntireModel,
                    (int)swInsertAnnotation_e.swInsertDimensionsMarkedForDrawing,
                    true, false, false, false);
                model.ClearSelection2(true);
                model.EditRebuild3();

                int after = CountDimensions(drawing);
                int count = Math.Max(0, after - before);
                if (count == 0 && raw is Array returned) count = returned.Length;

                return ToolResult.Success(new DrawingAnnotationResult
                {
                    ViewName = view.GetName2(),
                    InsertedCount = count
                });
            }
            catch (Exception ex) { return ToolResult.Error("InsertDimensions failed: " + ex.Message); }
        }

        private static int CountDimensions(IDrawingDoc drawing)
        {
            int count = 0;
            var view = drawing?.GetFirstView() as IView;
            while (view != null)
            {
                var dimension = view.GetFirstDisplayDimension5() as IDisplayDimension;
                while (dimension != null)
                {
                    count++;
                    dimension = dimension.GetNext5() as IDisplayDimension;
                }
                view = view.GetNextView() as IView;
            }
            return count;
        }

        private static IView FindView(IDrawingDoc drawing, string requested)
        {
            var view = (drawing.GetFirstView() as IView)?.GetNextView() as IView;
            if (string.IsNullOrWhiteSpace(requested)) return view;
            while (view != null)
            {
                if (string.Equals(view.GetName2(), requested, StringComparison.OrdinalIgnoreCase)) return view;
                view = view.GetNextView() as IView;
            }
            return null;
        }

        private static string Text(Dictionary<string, object> p, string key) =>
            p != null && p.TryGetValue(key, out var v) ? Convert.ToString(v) ?? string.Empty : string.Empty;
    }
}
