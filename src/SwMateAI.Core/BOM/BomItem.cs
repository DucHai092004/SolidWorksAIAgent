namespace SwMateAI.Core.BOM
{
    public class BomItem
    {
        public int ItemNumber { get; set; }
        public string PartNumber { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string Material { get; set; } = string.Empty;
        public string ComponentType { get; set; } = string.Empty;
        public string Configuration { get; set; } = string.Empty;
        public string SourcePath { get; set; } = string.Empty;
        public bool IsVirtual { get; set; }
        public bool IsLoaded { get; set; }
    }
}
