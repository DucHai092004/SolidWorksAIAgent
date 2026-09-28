namespace SwMateAI.Core.Manufacturing
{
    /// <summary>Business stock rules. Values are TOTAL allowances, not per-side allowances.</summary>
    public class StockCalculationOptions
    {
        public double RoundDiameterTotalAllowanceMm { get; set; } = 2.0;
        public double RoundLengthTotalAllowanceMm { get; set; } = 4.0;
        public double PrismaticLengthTotalAllowanceMm { get; set; } = 10.0;
        public double PrismaticWidthTotalAllowanceMm { get; set; } = 10.0;
        public double MinimumThicknessAllowanceMm { get; set; } = 2.0;
        public bool UseStandardThicknessTable { get; set; } = true;
        public string StandardThicknessCatalogPath { get; set; } = string.Empty;
        public string DocumentRoot { get; set; } = string.Empty;

        // Kept for backward compatibility with older planner/tool parameters.
        public double AllowancePerSideMm { get; set; } = 3.0;
        public double RoundDiameterAllowancePerSideMm { get; set; } = 3.0;
    }
}
