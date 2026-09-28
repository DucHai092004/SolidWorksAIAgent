using System;

namespace SwMateAI.Core.Manufacturing
{
    public class StockCalculator
    {
        public void Calculate(BreakdownItem item, StockCalculationOptions options)
        {
            if (item == null) return;
            options = options ?? new StockCalculationOptions();

            if (item.StockType == "Round Bar" && StockClassifier.TryGetRoundBarDimensions(item, out var diameter, out var length))
            {
                double stockD = diameter + 2.0 * options.RoundDiameterAllowancePerSideMm;
                double stockL = length + 2.0 * options.AllowancePerSideMm;
                item.StockSize = $"Ø{stockD:0.###} x {stockL:0.###} mm";
                item.StockVolumeMm3 = Math.PI * stockD * stockD * 0.25 * stockL;
                return;
            }

            if (item.FinishedXmm <= 0 || item.FinishedYmm <= 0 || item.FinishedZmm <= 0)
            {
                item.StockSize = string.Empty;
                item.StockVolumeMm3 = 0;
                return;
            }

            double ax = item.FinishedXmm + 2.0 * options.AllowancePerSideMm;
            double ay = item.FinishedYmm + 2.0 * options.AllowancePerSideMm;
            double az = item.FinishedZmm + 2.0 * options.AllowancePerSideMm;
            item.StockSize = $"{ax:0.###} x {ay:0.###} x {az:0.###} mm";
            item.StockVolumeMm3 = ax * ay * az;
        }
    }
}
