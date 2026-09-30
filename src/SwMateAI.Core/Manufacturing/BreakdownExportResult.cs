namespace SwMateAI.Core.Manufacturing
{
    public class BreakdownExportResult
    {
        public BreakdownResult Breakdown { get; set; }
        public string ExcelPath { get; set; } = string.Empty;
        public string ImageFolder { get; set; } = string.Empty;
        public int CapturedImageCount { get; set; }
    }
}
