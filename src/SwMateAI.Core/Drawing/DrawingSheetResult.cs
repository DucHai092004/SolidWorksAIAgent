namespace SwMateAI.Core.Drawing
{
    public class DrawingSheetResult
    {
        public string SheetName { get; set; } = string.Empty;
        public string PaperSize { get; set; } = string.Empty;
        public double ScaleNumerator { get; set; }
        public double ScaleDenominator { get; set; }
        public int TotalSheetCount { get; set; }

        public override string ToString()
        {
            return "Sheet created: " + SheetName +
                   " | " + PaperSize +
                   " | scale " + ScaleNumerator.ToString("0.###") +
                   ":" + ScaleDenominator.ToString("0.###") +
                   " | total sheets " + TotalSheetCount;
        }
    }
}
