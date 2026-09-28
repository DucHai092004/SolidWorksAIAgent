using System.Collections.Generic;

namespace SwMateAI.Core.BOM
{
    public class BomResult
    {
        public List<BomItem> Items { get; } = new List<BomItem>();
        public int TotalOccurrences { get; set; }
        public int SuppressedSkipped { get; set; }
        public int UnloadedCount { get; set; }
        public string ExcelPath { get; set; } = string.Empty;
        public string CsvPath { get; set; } = string.Empty;
    }
}
