using System;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;

namespace SwMateAI.Core.Manufacturing
{
    /// <summary>
    /// P2 stock-table builder. It preserves the existing one-row-per-Part manufacturing
    /// breakdown, but expands SOLIDWORKS Sheet Metal / Weldment cut-list items only for
    /// the Stock Excel workflow.
    /// </summary>
    public class StockExcelBreakdownBuilder
    {
        private readonly ISldWorks _swApp;
        private readonly StockCalculationOptions _options;
        private readonly StockWeightCalculator _weightCalculator = new StockWeightCalculator();

        public StockExcelBreakdownBuilder(ISldWorks swApp, StockCalculationOptions options = null)
        {
            _swApp = swApp ?? throw new ArgumentNullException(nameof(swApp));
            _options = options ?? new StockCalculationOptions();
        }

        public BreakdownResult Build()
        {
            BreakdownResult source = new ManufacturingBreakdownBuilder(_swApp, _options).Build();
            var result = CopySummary(source);
            var expander = new CutListStockExpander(_swApp);

            foreach (BreakdownItem baseItem in source.Items)
            {
                List<BreakdownItem> cutListRows = expander.Expand(baseItem);
                if (cutListRows.Count == 0)
                {
                    result.Items.Add(baseItem);
                    continue;
                }

                foreach (BreakdownItem row in cutListRows)
                {
                    // Sheet Metal has an exact flat blank volume. Weldment profile volume
                    // is profile-dependent and intentionally remains zero unless a future
                    // profile-area reader supplies it.
                    if (string.Equals(row.ManufacturingForm, "Sheet Metal", StringComparison.OrdinalIgnoreCase))
                        _weightCalculator.Calculate(row);
                    else if (string.Equals(row.ManufacturingForm, "Weldment", StringComparison.OrdinalIgnoreCase))
                    {
                        row.StockVolumeMm3 = 0;
                        row.StockWeightKg = 0;
                    }

                    result.Items.Add(row);
                }
            }

            return result;
        }

        private static BreakdownResult CopySummary(BreakdownResult source)
        {
            if (source == null) return new BreakdownResult();
            return new BreakdownResult
            {
                TotalPartOccurrences = source.TotalPartOccurrences,
                SuppressedSkipped = source.SuppressedSkipped,
                UnloadedPartCount = source.UnloadedPartCount,
                AllowancePerSideMm = source.AllowancePerSideMm,
                MaterialFilesScanned = source.MaterialFilesScanned,
                MaterialRecordsFound = source.MaterialRecordsFound,
                StockMaterialsFromDocuments = source.StockMaterialsFromDocuments,
                StockMaterialsFromCad = source.StockMaterialsFromCad,
                StockMaterialsNeedReview = source.StockMaterialsNeedReview,
                MaterialImportErrors = source.MaterialImportErrors
            };
        }
    }
}
