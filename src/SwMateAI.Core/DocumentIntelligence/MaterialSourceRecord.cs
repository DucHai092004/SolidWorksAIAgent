namespace SwMateAI.Core.DocumentIntelligence
{
    /// <summary>Evidence extracted from one Excel/CSV/PDF location. No SolidWorks COM dependency.</summary>
    public class MaterialSourceRecord
    {
        public string RawPartCode { get; set; } = string.Empty;
        public string NormalizedPartCode { get; set; } = string.Empty;
        public string StockMaterial { get; set; } = string.Empty;
        public string SourceFile { get; set; } = string.Empty;
        public string SheetName { get; set; } = string.Empty;
        public int? RowNumber { get; set; }
        public int? PageNumber { get; set; }
        public string ExtractionMethod { get; set; } = string.Empty;
        public double ExtractionConfidence { get; set; } = 1.0;
    }
}
