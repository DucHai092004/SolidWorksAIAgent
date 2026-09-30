using System.Collections.Generic;

namespace SwMateAI.Core.Models.Understanding
{
    /// <summary>
    /// Read-only impact report derived from the SOLIDWORKS feature dependency graph.
    /// DirectChildren are immediate dependents; AffectedFeatures contains all descendants.
    /// </summary>
    public class FeatureImpactInfo
    {
        public string TargetFeature { get; set; } = string.Empty;
        public string TargetTypeName { get; set; } = string.Empty;
        public List<string> DirectChildren { get; } = new List<string>();
        public List<string> AffectedFeatures { get; } = new List<string>();
    }
}
