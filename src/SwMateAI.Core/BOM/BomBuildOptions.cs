using System.Collections.Generic;

namespace SwMateAI.Core.BOM
{
    public enum BomMode
    {
        LegacyFlat,
        TopLevel,
        PartsOnly,
        Indented
    }

    public enum BomChildDisplay
    {
        Show,
        Hide,
        Promote
    }

    public class BomBuildOptions
    {
        public BomMode Mode { get; set; } = BomMode.LegacyFlat;
        public bool RespectChildDisplay { get; set; } = true;
    }

    public class BomHierarchyNode
    {
        public string IdentityKey { get; set; } = string.Empty;
        public string ComponentName { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Configuration { get; set; } = string.Empty;
        public string PartNumber { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Material { get; set; } = string.Empty;
        public string ComponentType { get; set; } = string.Empty;
        public bool IsVirtual { get; set; }
        public bool IsLoaded { get; set; }
        public bool IsSuppressed { get; set; }
        public bool ExcludeFromBom { get; set; }
        public BomChildDisplay ChildDisplay { get; set; } = BomChildDisplay.Show;
        public List<BomHierarchyNode> Children { get; } = new List<BomHierarchyNode>();
    }

    public class BomResolvedOccurrence
    {
        public BomHierarchyNode Node { get; set; }
        public int Level { get; set; }
    }
}
