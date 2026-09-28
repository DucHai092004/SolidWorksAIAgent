using System;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SwMateAI.Core.Tools.Drawing
{
    public class ExportDxfTool : ExportDrawingToolBase
    {
        public ExportDxfTool(ISldWorks swApp) : base(swApp) { }
        public override string Name => "ExportDXF";
        public override string Description => "Exports the active SOLIDWORKS Drawing to DXF without interactive mapping dialogs.";

        public override ToolResult Execute(Dictionary<string, object> parameters)
        {
            bool oldDontShowMap = false;
            try
            {
                oldDontShowMap = SwApp.GetUserPreferenceToggle((int)swUserPreferenceToggle_e.swDXFDontShowMap);
                SwApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swDXFDontShowMap, true);
                return Save(ResolveOutput(parameters, ".dxf"), null, "DXF");
            }
            catch (Exception ex) { return ToolResult.Error("ExportDXF failed: " + ex.Message); }
            finally
            {
                try { SwApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swDXFDontShowMap, oldDontShowMap); }
                catch { }
            }
        }
    }
}
