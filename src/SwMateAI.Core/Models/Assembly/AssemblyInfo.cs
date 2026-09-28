namespace SwMateAI.Core.Models.Assembly
{
    /// <summary>Read-only summary of the active SOLIDWORKS assembly.</summary>
    public class AssemblyInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Configuration { get; set; } = string.Empty;
        public int TopLevelComponentCount { get; set; }
        public int TotalComponentCount { get; set; }
        public int MateCount { get; set; }
        public int SuppressedComponentCount { get; set; }
        public int LightweightComponentCount { get; set; }
        public bool HasUnloadedComponents { get; set; }
        public bool IsComponentTreeValid { get; set; }
    }
}
