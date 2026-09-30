using System;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SwMateAI.Core.Drawing;

namespace SwMateAI.Core.Tools.Drawing
{
    /// <summary>
    /// Phase 6A.1: creates a new Drawing from the user's default Drawing template.
    /// The source Part/Assembly identity is preserved for later view insertion skills.
    /// </summary>
    public class CreateDrawingTool : SwToolBase
    {
        private readonly DrawingSessionContext _session;

        public CreateDrawingTool(ISldWorks swApp, DrawingSessionContext session) : base(swApp)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public override string Name => "CreateDrawing";
        public override string Description =>
            "Creates a new SOLIDWORKS Drawing for the active Part or Assembly using the configured default Drawing template.";

        public override bool CanExecute(out string reason)
        {
            reason = null;
            var source = SwApp.ActiveDoc as IModelDoc2;
            if (source == null)
            {
                reason = "Open a Part or Assembly before creating a Drawing.";
                return false;
            }

            int type = source.GetType();
            if (type != (int)swDocumentTypes_e.swDocPART && type != (int)swDocumentTypes_e.swDocASSEMBLY)
            {
                reason = "The active document must be a Part or Assembly.";
                return false;
            }

            string template = SwApp.GetUserPreferenceStringValue(
                (int)swUserPreferenceStringValue_e.swDefaultTemplateDrawing);
            if (string.IsNullOrWhiteSpace(template))
            {
                reason = "No default Drawing template is configured in SOLIDWORKS.";
                return false;
            }
            return true;
        }

        public override ToolResult Execute(Dictionary<string, object> parameters)
        {
            try
            {
                var source = SwApp.ActiveDoc as IModelDoc2;
                if (source == null) return ToolResult.Error("No active source model.");

                string template = SwApp.GetUserPreferenceStringValue(
                    (int)swUserPreferenceStringValue_e.swDefaultTemplateDrawing);
                if (string.IsNullOrWhiteSpace(template))
                    return ToolResult.Error("No default Drawing template is configured in SOLIDWORKS.");

                _session.SourceModelTitle = source.GetTitle() ?? string.Empty;
                _session.SourceModelPath = source.GetPathName() ?? string.Empty;
                _session.DrawingTemplatePath = template;

                var drawingModel = SwApp.NewDocument(template, 0, 0, 0) as IModelDoc2;
                var drawing = drawingModel as IDrawingDoc;
                if (drawingModel == null || drawing == null)
                    return ToolResult.Error("SOLIDWORKS did not create a valid Drawing document.");

                if (drawing.GetCurrentSheet() == null)
                    return ToolResult.Error("Drawing was created but no active sheet exists.");

                _session.DrawingTitle = drawingModel.GetTitle() ?? string.Empty;
                return ToolResult.Success(_session);
            }
            catch (Exception ex)
            {
                return ToolResult.Error("CreateDrawing failed: " + ex.Message);
            }
        }
    }
}
