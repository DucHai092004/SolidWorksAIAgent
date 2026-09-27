using System;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SwMateAI.Core.Tools.CAD
{
    /// <summary>
    /// Phase 2B CAD Tool: Creates a new sketch on the first standard
    /// reference plane of the active SOLIDWORKS Part document.
    /// </summary>
    public class CreateSketchTool : SwToolBase
    {
        public override string Name => "CreateSketch";

        public override string Description =>
            "Creates a new sketch on the first standard reference plane of the active Part.";

        public CreateSketchTool(ISldWorks swApp) : base(swApp)
        {
        }

        public override bool CanExecute(out string reason)
        {
            reason = null;

            try
            {
                var model = SwApp.ActiveDoc as IModelDoc2;
                if (model == null)
                {
                    reason = "No active SOLIDWORKS document.";
                    return false;
                }

                if (model.GetType() != (int)swDocumentTypes_e.swDocPART)
                {
                    reason = "The active document must be a Part.";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                reason = $"Unable to validate the active document: {ex.Message}";
                return false;
            }
        }

        public override ToolResult Execute(Dictionary<string, object> parameters)
        {
            try
            {
                var model = SwApp.ActiveDoc as IModelDoc2;
                if (model == null)
                    return ToolResult.Error("No active SOLIDWORKS document.");

                IFeature plane = model.FirstFeature() as IFeature;
                while (plane != null && plane.GetTypeName2() != "RefPlane")
                    plane = plane.GetNextFeature() as IFeature;

                if (plane == null)
                    return ToolResult.Error("No reference plane was found in the active Part.");

                model.ClearSelection2(true);

                bool selected = plane.Select2(false, 0);
                if (!selected)
                    return ToolResult.Error("SOLIDWORKS could not select the reference plane.");

                model.SketchManager.InsertSketch(true);

                var activeSketch = model.SketchManager.ActiveSketch;
                if (activeSketch == null)
                    return ToolResult.Error("SOLIDWORKS did not enter sketch edit mode.");

                return ToolResult.Success(
                    $"Sketch created successfully on plane: {plane.Name}");
            }
            catch (Exception ex)
            {
                return ToolResult.Error($"CreateSketch failed: {ex.Message}");
            }
        }
    }
}
