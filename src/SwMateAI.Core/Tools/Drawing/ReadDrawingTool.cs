using System;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SwMateAI.Core.DrawingUnderstanding;

namespace SwMateAI.Core.Tools.Drawing
{
    /// <summary>Phase 7: structured SLDDRW understanding without image/OCR inference.</summary>
    public class ReadDrawingTool : SwToolBase
    {
        public ReadDrawingTool(ISldWorks swApp) : base(swApp) { }
        public override string Name => "ReadDrawing";
        public override string Description =>
            "Reads Drawing sheets, model views, dimensions, notes and tables directly from the SOLIDWORKS Drawing API.";

        public override bool CanExecute(out string reason)
        {
            reason = null;
            var model = SwApp.ActiveDoc as IModelDoc2;
            if (model == null || model.GetType() != (int)swDocumentTypes_e.swDocDRAWING)
            { reason = "Open a Drawing before reading it."; return false; }
            return true;
        }

        public override ToolResult Execute(Dictionary<string, object> parameters)
        {
            try { return ToolResult.Success(new DrawingReader().Read(SwApp.ActiveDoc as IModelDoc2)); }
            catch (Exception ex) { return ToolResult.Error("ReadDrawing failed: " + ex.Message); }
        }
    }
}
