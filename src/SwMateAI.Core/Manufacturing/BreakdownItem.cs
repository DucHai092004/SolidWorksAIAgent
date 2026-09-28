namespace SwMateAI.Core.Manufacturing
{
    /// <summary>One unique manufactured-part row in the V1 breakdown table.</summary>
    public class BreakdownItem
    {
        public string ImagePath { get; set; } = string.Empty;
        public string PartNumber { get; set; } = string.Empty;
        public string PartName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string Material { get; set; } = string.Empty;
        public double FinishedXmm { get; set; }
        public double FinishedYmm { get; set; }
        public double FinishedZmm { get; set; }
        public string StockType { get; set; } = string.Empty;
        public string StockSize { get; set; } = string.Empty;
        public double StockWeightKg { get; set; }
        public string ManufacturingTechnology { get; set; } = string.Empty;
        public string Supplier { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string SourcePath { get; set; } = string.Empty;
        public string Configuration { get; set; } = string.Empty;
        public bool IsLoaded { get; set; }
        public string FinishedSize => $"{FinishedXmm:0.###} x {FinishedYmm:0.###} x {FinishedZmm:0.###} mm";
    }
}
