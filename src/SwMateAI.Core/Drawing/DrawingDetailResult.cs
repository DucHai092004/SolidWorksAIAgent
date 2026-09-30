namespace SwMateAI.Core.Drawing
{
    public class DrawingDetailResult
    {
        public string SourceViewName { get; set; } = string.Empty;
        public string DetailViewName { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public double RadiusMm { get; set; }
        public double ScaleNumerator { get; set; }
        public double ScaleDenominator { get; set; }
        public int TotalModelViewCount { get; set; }

        public override string ToString()
        {
            return "Detail view created: " + DetailViewName +
                   " | radius " + RadiusMm.ToString("0.###") + " mm" +
                   " | scale " + ScaleNumerator.ToString("0.###") + ":" +
                   ScaleDenominator.ToString("0.###");
        }
    }
}
