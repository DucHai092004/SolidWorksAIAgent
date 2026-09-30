using System;

namespace SwMateAI.Core.Manufacturing
{
    public class StockCalculator
    {
        public void Calculate(BreakdownItem item, StockCalculationOptions options)
        {
            if (item == null) return;
            options = options ?? new StockCalculationOptions();

            if (item.StockType == "Round Bar" &&
                StockClassifier.TryGetRoundBarDimensions(item, out var diameter, out var length))
            {
                double stockD = diameter + options.RoundDiameterTotalAllowanceMm;
                double roundStockL = length + options.RoundLengthTotalAllowanceMm;
                item.StockSize = $"Ø{stockD:0.###} x {roundStockL:0.###} mm";
                item.StockVolumeMm3 = Math.PI * stockD * stockD * 0.25 * roundStockL;
                item.StockSizeRule = $"Round: Ø +{options.RoundDiameterTotalAllowanceMm:0.###} mm; L +{options.RoundLengthTotalAllowanceMm:0.###} mm";
                item.StockThicknessBasis = "Not applicable to round stock";
                return;
            }

            if (item.FinishedXmm <= 0 || item.FinishedYmm <= 0 || item.FinishedZmm <= 0)
            {
                item.StockSize = string.Empty;
                item.StockVolumeMm3 = 0;
                item.StockSizeRule = "Missing finished dimensions";
                return;
            }

            double[] d = { item.FinishedXmm, item.FinishedYmm, item.FinishedZmm };
            Array.Sort(d);
            double finishedH = d[0], finishedW = d[1], finishedL = d[2];

            double stockL = finishedL + options.PrismaticLengthTotalAllowanceMm;
            double stockW = finishedW + options.PrismaticWidthTotalAllowanceMm;
            double minimumH = finishedH + options.MinimumThicknessAllowanceMm;
            double stockH = minimumH;
            item.StockThicknessBasis = $"Fallback: H +{options.MinimumThicknessAllowanceMm:0.###} mm";

            if (options.UseStandardThicknessTable)
            {
                var catalog = StandardThicknessCatalog.Load(options.StandardThicknessCatalogPath);
                string material = !string.IsNullOrWhiteSpace(item.StockMaterial) ? item.StockMaterial : item.Material;
                if (catalog.TryGetNext(material, minimumH, out double standardH))
                {
                    stockH = standardH;
                    item.StockThicknessBasis = $"Approved thickness table: next >= {minimumH:0.###} mm";
                }
            }

            item.StockSize = $"{stockL:0.###} x {stockW:0.###} x {stockH:0.###} mm";
            item.StockVolumeMm3 = stockL * stockW * stockH;
            item.StockSizeRule =
                $"Prismatic: L +{options.PrismaticLengthTotalAllowanceMm:0.###}; " +
                $"W +{options.PrismaticWidthTotalAllowanceMm:0.###}; H >= +{options.MinimumThicknessAllowanceMm:0.###} mm";
        }
    }
}
