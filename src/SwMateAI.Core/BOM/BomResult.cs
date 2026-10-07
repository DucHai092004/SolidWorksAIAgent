using System.Collections.Generic;

namespace SwMateAI.Core.BOM
{
    public class BomResult
    {
        public List<BomItem> Items { get; } = new List<BomItem>();
        public BomMode Mode { get; set; } = BomMode.LegacyFlat;
        public int TotalOccurrences { get; set; }
        public int SuppressedSkipped { get; set; }
        public int HiddenSkipped { get; set; }
        public int MissingSkipped { get; set; }
        public int ExcludedSkipped { get; set; }
        public int UnloadedCount { get; set; }
        public int CapturedImageCount { get; set; }
        public List<string> Warnings { get; } = new List<string>();
        public string ExcelPath { get; set; } = string.Empty;
        public string CsvPath { get; set; } = string.Empty;
    }
}
