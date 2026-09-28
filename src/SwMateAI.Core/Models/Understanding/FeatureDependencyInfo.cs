using System.Collections.Generic;

namespace SwMateAI.Core.Models.Understanding
{
    /// <summary>
    /// Describes one node in the SOLIDWORKS feature dependency graph.
    /// Parents are features this node depends on; children depend on this node.
    /// Keeping both directions makes later design-intent analysis easier.
    /// </summary>
    public class FeatureDependencyInfo
    {
        public string Name { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public bool IsSketch { get; set; }
        public List<string> Parents { get; } = new List<string>();
        public List<string> Children { get; } = new List<string>();
    }
}
