namespace SwMateAI.Core.Manufacturing
{
    public class StockCalculationOptions
    {
        /// <summary>Machining allowance added on each side of the finished bounding box.</summary>
        public double AllowancePerSideMm { get; set; } = 3.0;

        /// <summary>Round-bar diameter tolerance used only for stock sizing.</summary>
        public double RoundDiameterAllowancePerSideMm { get; set; } = 3.0;
    }
}
