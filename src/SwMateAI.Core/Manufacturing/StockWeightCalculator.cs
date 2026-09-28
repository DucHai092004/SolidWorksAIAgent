namespace SwMateAI.Core.Manufacturing
{
    public class StockWeightCalculator
    {
        public void Calculate(BreakdownItem item)
        {
            if (item == null || item.StockVolumeMm3 <= 0 || item.DensityKgM3 <= 0)
            {
                if (item != null) item.StockWeightKg = 0;
                return;
            }

            double volumeM3 = item.StockVolumeMm3 * 1e-9;
            item.StockWeightKg = volumeM3 * item.DensityKgM3;
        }
    }
}
