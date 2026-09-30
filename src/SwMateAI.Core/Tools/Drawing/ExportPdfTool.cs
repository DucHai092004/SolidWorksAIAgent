using System;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SwMateAI.Core.Tools.Drawing
{
    public class ExportPdfTool : ExportDrawingToolBase
    {
        public ExportPdfTool(ISldWorks swApp) : base(swApp) { }
        public override string Name => "ExportPDF";
        public override string Description => "Exports all sheets of the active Drawing to PDF.";

        public override ToolResult Execute(Dictionary<string, object> parameters)
        {
            try
            {
                var data = SwApp.GetExportFileData((int)swExportDataFileType_e.swExportPdfData) as IExportPdfData;
                if (data == null) return ToolResult.Error("SOLIDWORKS did not provide PDF export data.");
                data.ViewPdfAfterSaving = false;
                data.SetSheets((int)swExportDataSheetsToExport_e.swExportData_ExportAllSheets, null);
                return Save(ResolveOutput(parameters, ".pdf"), data, "PDF");
            }
            catch (Exception ex) { return ToolResult.Error("ExportPDF failed: " + ex.Message); }
        }
    }
}
