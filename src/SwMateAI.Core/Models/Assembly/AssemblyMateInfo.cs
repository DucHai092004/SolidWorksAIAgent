using System.Collections.Generic;

namespace SwMateAI.Core.Models.Assembly
{
    /// <summary>One mate feature and the components referenced by that mate.</summary>
    public class AssemblyMateInfo
    {
        public string Name { get; set; } = string.Empty;
        public int TypeId { get; set; }
        public string TypeName { get; set; } = string.Empty;
        public int AlignmentId { get; set; }
        public string AlignmentName { get; set; } = string.Empty;
        public int EntityCount { get; set; }
        public List<string> Components { get; } = new List<string>();
    }
}
