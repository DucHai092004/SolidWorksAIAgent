using System.Collections.Generic;

namespace SwMateAI.Core.DocumentIntelligence
{
    public class MaterialImportResult
    {
        public List<MaterialSourceRecord> Records { get; } = new List<MaterialSourceRecord>();
        public List<string> Errors { get; } = new List<string>();
        public int FilesScanned { get; set; }
        public int ExcelCsvFiles { get; set; }
        public int PdfFiles { get; set; }
    }
}
