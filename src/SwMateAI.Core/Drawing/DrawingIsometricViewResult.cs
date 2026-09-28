namespace SwMateAI.Core.Drawing
{
    public class DrawingIsometricViewResult
    {
        public string ModelPath { get; set; } = string.Empty;
        public string ViewName { get; set; } = string.Empty;
        public double Xmm { get; set; }
        public double Ymm { get; set; }
        public int TotalModelViewCount { get; set; }

        public override string ToString()
        {
            return "Isometric view inserted: " + ViewName +
                   " | X=" + Xmm.ToString("0.###") + " mm" +
                   " | Y=" + Ymm.ToString("0.###") + " mm";
        }
    }
}
