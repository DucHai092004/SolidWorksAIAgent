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
        public int Failed { get; set; }
        public int NeedsReview { get; set; }
        public int Skipped { get; set; }
        public int SourceFilesScanned { get; set; }
    }

    public class ApplyStockMaterialsTool : SwToolBase
    {
        public ApplyStockMaterialsTool(ISldWorks swApp) : base(swApp) { }
        public override string Name => "ApplyStockMaterials";
        public override string Description =>
            "Reads related Excel/CSV/PDF documents and writes verified Stock Material and StockSize custom properties to matched loaded Parts.";

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
            if (breakdown.MaterialImportErrors > 0)
                return ToolResult.Error($"{breakdown.MaterialImportErrors} material document issue(s) require review; no stock properties were written.");
            var result = new ApplyStockMaterialsResult
            {
                NeedsReview = breakdown.StockMaterialsNeedReview,
                SourceFilesScanned = breakdown.MaterialFilesScanned
            };

            var assembly = SwApp.ActiveDoc as IAssemblyDoc;
            foreach (var item in breakdown.Items)
            {
                if (item.StockMaterialNeedsReview || item.StockGeometryNeedsReview
                    || string.IsNullOrWhiteSpace(item.StockMaterial)
                    || string.IsNullOrWhiteSpace(item.StockSize))
                { result.Skipped++; continue; }
                var component = assembly?.GetComponentByName(item.RepresentativeComponentName);
                var model = component?.GetModelDoc2() as IModelDoc2;
                if (model == null) { result.Skipped++; continue; }

                var manager = model.Extension?.get_CustomPropertyManager(item.Configuration ?? string.Empty);
                if (manager == null) { result.Skipped++; continue; }

                PropertyValue materialBefore = default(PropertyValue);
                PropertyValue sizeBefore = default(PropertyValue);
                PropertyValue sourceBefore = default(PropertyValue);
                bool snapshotReady = false;
                try
                {
                    materialBefore = ReadProperty(manager, "Stock Material");
                    sizeBefore = ReadProperty(manager, "StockSize");
                    sourceBefore = ReadProperty(manager, "Stock Material Source");
                    snapshotReady = true;
                    WriteAndVerify(manager, "Stock Material", item.StockMaterial);
                    WriteAndVerify(manager, "StockSize", item.StockSize);
                    WriteAndVerify(manager, "Stock Material Source", item.StockMaterialSource ?? string.Empty);
                    model.SetSaveFlag();
                    result.Applied++;
                }
                catch (Exception)
                {
                    result.Failed++;
                    if (snapshotReady)
                    {
                        // Attempt all restores even when one COM call fails.
                        TryRestore(manager, "Stock Material", materialBefore);
                        TryRestore(manager, "StockSize", sizeBefore);
                        TryRestore(manager, "Stock Material Source", sourceBefore);
                    }
                }
            }
            if (result.Failed > 0)
                return ToolResult.Error($"Failed to verify stock properties for {result.Failed} Part(s); {result.Applied} Part(s) applied.");
            return ToolResult.Success(result);
        }

        private struct PropertyValue
        {
            public bool Exists;
            public string Raw;
        }

        private static PropertyValue ReadProperty(ICustomPropertyManager manager, string name)
        {
            string raw, resolved;
            bool wasResolved, linked;
            int status = manager.Get6(name, false, out raw, out resolved, out wasResolved, out linked);
            return new PropertyValue { Exists = status != 0, Raw = raw ?? string.Empty };
        }

        private static void WriteAndVerify(ICustomPropertyManager manager, string name, string value)
        {
            manager.Add3(name, (int)swCustomInfoType_e.swCustomInfoText, value,
                (int)swCustomPropertyAddOption_e.swCustomPropertyReplaceValue);
            var actual = ReadProperty(manager, name);
            if (!actual.Exists || !string.Equals(actual.Raw, value, StringComparison.Ordinal))
                throw new InvalidOperationException($"Custom property {name} did not retain its value.");
        }

        private static void TryRestore(ICustomPropertyManager manager, string name, PropertyValue previous)
        {
            try
            {
                if (previous.Exists)
                    manager.Add3(name, (int)swCustomInfoType_e.swCustomInfoText, previous.Raw,
                        (int)swCustomPropertyAddOption_e.swCustomPropertyReplaceValue);
                else
                    manager.Delete2(name);
            }
            catch (Exception) { /* The overall tool result remains a failure. */ }
        }

        private static string Text(Dictionary<string, object> input, string key) =>
            input != null && input.TryGetValue(key, out var value)
                ? Convert.ToString(value)?.Trim() ?? string.Empty
                : string.Empty;
    }
}
