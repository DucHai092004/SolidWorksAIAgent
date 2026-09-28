using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SwMateAI.Core.Manufacturing;

namespace SwMateAI.Core.Tools.Manufacturing
{
    public class ExportManufacturingBreakdownTool : SwToolBase
    {
        public ExportManufacturingBreakdownTool(ISldWorks swApp) : base(swApp) { }
        public override string Name => "ExportManufacturingBreakdown";
        public override string Description => "Builds the manufacturing breakdown, captures Part images and exports an Excel workbook.";

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
            var model = SwApp.ActiveDoc as IModelDoc2;
            double allowance = ReadDouble(parameters, "AllowancePerSideMm", 3.0);
            string output = Text(parameters, "OutputPath");
            if (string.IsNullOrWhiteSpace(output)) output = DefaultOutputPath(model);
            string imageFolder = Text(parameters, "ImageFolder");
            var options = new StockCalculationOptions { AllowancePerSideMm = allowance, RoundDiameterAllowancePerSideMm = allowance };
            return ToolResult.Success(new ManufacturingBreakdownExporter(SwApp).Export(output, imageFolder, options));
        }

        private static string Text(Dictionary<string, object> input, string key)
        {
            return input != null && input.TryGetValue(key, out var value) ? Convert.ToString(value)?.Trim() ?? string.Empty : string.Empty;
        }

        private static double ReadDouble(Dictionary<string, object> input, string key, double fallback)
        {
            string s = Text(input, key).Replace(',', '.');
            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : fallback;
        }

        private static string DefaultOutputPath(IModelDoc2 model)
        {
            string assemblyPath = model?.GetPathName() ?? string.Empty;
            string root = !string.IsNullOrWhiteSpace(assemblyPath)
                ? Path.GetDirectoryName(assemblyPath)
                : System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
            string name = !string.IsNullOrWhiteSpace(assemblyPath)
                ? Path.GetFileNameWithoutExtension(assemblyPath)
                : Path.GetFileNameWithoutExtension(model?.GetTitle() ?? "Assembly");
            string folder = Path.Combine(root ?? string.Empty, "SW-MATE_AI_Output");
            return Path.Combine(folder, name + "_Breakdown.xlsx");
        }
    }
}
