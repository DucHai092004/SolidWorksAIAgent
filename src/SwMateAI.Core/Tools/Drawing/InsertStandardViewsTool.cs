using System;
using System.Collections.Generic;
using System.IO;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SwMateAI.Core.Drawing;

namespace SwMateAI.Core.Tools.Drawing
{
    /// <summary>
    /// Phase 6 Drawing Automation: inserts the standard orthographic views
    /// for the source Part/Assembly into the active Drawing.
    /// </summary>
    public class InsertStandardViewsTool : SwToolBase
    {
        private readonly DrawingSessionContext _session;

        public InsertStandardViewsTool(ISldWorks swApp, DrawingSessionContext session) : base(swApp)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public override string Name => "InsertStandardViews";
        public override string Description =>
            "Inserts standard orthographic drawing views using first-angle or third-angle projection.";
        public override bool CanExecute(out string reason)
        {
            reason = null;
            var model = SwApp.ActiveDoc as IModelDoc2;
            if (model == null)
            {
                reason = "Open a Drawing before inserting standard views.";
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
                if (string.IsNullOrWhiteSpace(modelPath))
                    modelPath = _session.SourceModelPath;
                if (string.IsNullOrWhiteSpace(modelPath))
                    return ToolResult.Error(
                        "The source model has no file path. Save the Part/Assembly first, or provide ModelPath.");

                if (!File.Exists(modelPath))
                    return ToolResult.Error("Source model file was not found: " + modelPath);

                string projection = GetString(parameters, "Projection");
                if (string.IsNullOrWhiteSpace(projection)) projection = "Third";
                bool firstAngle = IsFirstAngle(projection);

                int beforeCount = CountModelViews(drawing, null);
                bool created = firstAngle
                    ? drawing.Create1stAngleViews2(modelPath)
                    : drawing.Create3rdAngleViews2(modelPath);

                var names = new List<string>();
                int afterCount = CountModelViews(drawing, names);
                int added = afterCount - beforeCount;

                if (!created)
                    return ToolResult.Error("SOLIDWORKS reported that standard view creation failed.");

                if (added < 3)
                    return ToolResult.Error(
                        "Expected at least 3 standard model views, but only " + added + " were added.");

                var result = new DrawingStandardViewsResult
                {
                    ModelPath = modelPath,
                    Projection = firstAngle ? "First angle" : "Third angle",
                    AddedViewCount = added,
                    TotalModelViewCount = afterCount
                };
                foreach (var name in names) result.ViewNames.Add(name);
                return ToolResult.Success(result);
            }
            catch (Exception ex)
            {
                return ToolResult.Error("InsertStandardViews failed: " + ex.Message);
            }
        }

        private static int CountModelViews(IDrawingDoc drawing, List<string> names)
        {
            int count = 0;
            var sheetView = drawing.GetFirstView() as IView;
            var view = sheetView?.GetNextView() as IView;
            while (view != null)
            {
                count++;
                if (names != null)
                {
                    string name = view.GetName2();
                    string orientation = view.GetOrientationName();
                    names.Add(string.IsNullOrWhiteSpace(orientation)
                        ? name
                        : name + " [" + orientation + "]");
                }
                view = view.GetNextView() as IView;
            }
            return count;
        }

        private static bool IsFirstAngle(string value)
        {
            string v = (value ?? string.Empty).Trim().ToLowerInvariant();
            return v == "first" || v == "first angle" ||
                   v == "góc thứ nhất" || v == "goc thu nhat" || v == "1";
        }

        private static string GetString(Dictionary<string, object> parameters, string key)
        {
            if (parameters == null || !parameters.TryGetValue(key, out var value) || value == null)
                return string.Empty;
            return Convert.ToString(value) ?? string.Empty;
        }
    }
}
