using System;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SwMateAI.Core.Manufacturing;

namespace SwMateAI.Core.Tools.Manufacturing
{
    public class ApplyStockMaterialsResult
    {
        public int Applied { get; set; }
        public int NeedsReview { get; set; }
        public int Skipped { get; set; }
        public int SourceFilesScanned { get; set; }
    }

    public class ApplyStockMaterialsTool : SwToolBase
    {
        public ApplyStockMaterialsTool(ISldWorks swApp) : base(swApp) { }
        public override string Name => "ApplyStockMaterials";
        public override string Description =>
            "Reads related Excel/CSV/PDF documents and writes Stock Material custom properties to matched loaded Parts.";

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
            var options = new StockCalculationOptions { DocumentRoot = Text(parameters, "DocumentRoot") };
            var breakdown = new ManufacturingBreakdownBuilder(SwApp, options).Build();
            var result = new ApplyStockMaterialsResult
            {
                NeedsReview = breakdown.StockMaterialsNeedReview,
                SourceFilesScanned = breakdown.MaterialFilesScanned
            };

            var assembly = SwApp.ActiveDoc as IAssemblyDoc;
            foreach (var item in breakdown.Items)
            {
                if (item.StockMaterialNeedsReview || string.IsNullOrWhiteSpace(item.StockMaterial))
                { result.Skipped++; continue; }
                var component = assembly?.GetComponentByName(item.RepresentativeComponentName);
                var model = component?.GetModelDoc2() as IModelDoc2;
                if (model == null) { result.Skipped++; continue; }

                var manager = model.Extension?.get_CustomPropertyManager(item.Configuration ?? string.Empty);
                if (manager == null) { result.Skipped++; continue; }

                manager.Add3("Stock Material",
                    (int)swCustomInfoType_e.swCustomInfoText,
                    item.StockMaterial,
                    (int)swCustomPropertyAddOption_e.swCustomPropertyReplaceValue);
                manager.Add3("Stock Material Source",
                    (int)swCustomInfoType_e.swCustomInfoText,
                    item.StockMaterialSource,
                    (int)swCustomPropertyAddOption_e.swCustomPropertyReplaceValue);
                model.SetSaveFlag();
                result.Applied++;
            }
            return ToolResult.Success(result);
        }

        private static string Text(Dictionary<string, object> input, string key) =>
            input != null && input.TryGetValue(key, out var value)
                ? Convert.ToString(value)?.Trim() ?? string.Empty
                : string.Empty;
    }
}
