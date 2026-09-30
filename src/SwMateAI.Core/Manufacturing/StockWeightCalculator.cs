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

            // CAD density is valid for stock weight only when the external stock material
            // does not override the CAD material. Otherwise leave weight blank/zero until an
            // approved stock-material density catalog is configured.
            if (!string.IsNullOrWhiteSpace(item.StockMaterial) &&
                !string.IsNullOrWhiteSpace(item.Material) &&
                !string.Equals(item.StockMaterial.Trim(), item.Material.Trim(), System.StringComparison.OrdinalIgnoreCase))
            {
                item.StockWeightKg = 0;
                return;
            }

            double volumeM3 = item.StockVolumeMm3 * 1e-9;
            item.StockWeightKg = volumeM3 * item.DensityKgM3;
        }
    }
}
