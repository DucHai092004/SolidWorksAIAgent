namespace SwMateAI.Core.Models.Assembly
{
    /// <summary>One component occurrence in the active assembly.</summary>
    public class AssemblyComponentInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string ReferencedConfiguration { get; set; } = string.Empty;
        public string ParentName { get; set; } = string.Empty;
        public int Depth { get; set; }
        public int SuppressionState { get; set; }
        public string SuppressionStateName { get; set; } = string.Empty;
        public bool IsSuppressed { get; set; }
        public bool IsVirtual { get; set; }
        public bool IsLoaded { get; set; }
    }
}
