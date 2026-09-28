using System;
using System.Collections.Generic;
using System.Globalization;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SwMateAI.Core.Manufacturing;

namespace SwMateAI.Core.Tools.Manufacturing
{
    public class BuildManufacturingBreakdownTool : SwToolBase
    {
        public BuildManufacturingBreakdownTool(ISldWorks swApp) : base(swApp) { }
        public override string Name => "BuildManufacturingBreakdown";
        public override string Description => "Scans the active Assembly, groups unique Parts and calculates preliminary stock data.";

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
            double allowance = ReadDouble(parameters, "AllowancePerSideMm", 3.0);
            var options = new StockCalculationOptions { AllowancePerSideMm = allowance, RoundDiameterAllowancePerSideMm = allowance };
            return ToolResult.Success(new ManufacturingBreakdownBuilder(SwApp, options).Build());
        }

        private static double ReadDouble(Dictionary<string, object> input, string key, double fallback)
        {
            if (input == null || !input.TryGetValue(key, out var raw) || raw == null) return fallback;
            if (raw is double d) return d;
            string s = Convert.ToString(raw)?.Replace(',', '.') ?? string.Empty;
            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : fallback;
        }
    }
}
