using System;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SwMateAI.Core.Manufacturing;

namespace SwMateAI.Core.Tools.Manufacturing
{
    public class BuildManufacturingBreakdownTool : SwToolBase
    {
        public BuildManufacturingBreakdownTool(ISldWorks swApp) : base(swApp) { }
        public override string Name => "BuildManufacturingBreakdown";
        public override string Description =>
            "Scans the Assembly, reads stock material from related Excel/CSV/PDF files and calculates stock sizes.";

        public override bool CanExecute(out string reason)
        {
            reason = null;
            var model = SwApp.ActiveDoc as IModelDoc2;
            if (model == null) { reason = "No active SOLIDWORKS document."; return false; }
            if (model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
            { reason = "The active document must be an Assembly."; return false; }
            return true;
        }

        public override ToolResult Execute(Dictionary<string, object> parameters)
        {

            var options = new StockCalculationOptions
            {
                DocumentRoot = Text(parameters, "DocumentRoot"),
                StandardThicknessCatalogPath = Text(parameters, "ThicknessCatalogPath")
            };
            return ToolResult.Success(new ManufacturingBreakdownBuilder(SwApp, options).Build());
        }

        private static string Text(Dictionary<string, object> input, string key) =>
            input != null && input.TryGetValue(key, out var value)
                ? Convert.ToString(value)?.Trim() ?? string.Empty
                : string.Empty;
    }
}
