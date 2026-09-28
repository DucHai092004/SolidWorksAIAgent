namespace SwMateAI.Core.Agent
{
    public class ModelSnapshot
    {
        public bool HasDocument { get; set; }
        public string DocumentTitle { get; set; } = string.Empty;
        public int DocumentType { get; set; }
        public int FeatureCount { get; set; }
        public int SolidBodyCount { get; set; }
        public int AssemblyComponentCount { get; set; }
        public int AssemblyMateCount { get; set; }
        public int AssemblyBomCount { get; set; }
    }
}
