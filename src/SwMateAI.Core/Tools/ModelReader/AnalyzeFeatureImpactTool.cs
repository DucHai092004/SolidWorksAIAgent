using System;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;

namespace SwMateAI.Core.Tools.ModelReader
{
    /// <summary>
    /// Read-only impact-analysis skill. It never edits the model; it only walks
    /// descendants in the feature dependency graph for a requested feature.
    /// </summary>
    public class AnalyzeFeatureImpactTool : ModelReaderToolBase
    {
        public override string Name => "AnalyzeFeatureImpact";
        public override string Description => "Finds downstream features that may be affected by changing a target feature.";

        public AnalyzeFeatureImpactTool(ISldWorks swApp)
            : base(swApp, requirePart: false) { }

        public override ToolResult Execute(Dictionary<string, object> parameters)
        {
            if (parameters == null || !parameters.TryGetValue("FeatureName", out var raw))
                return ToolResult.Error("FeatureName is required.");

            string featureName = Convert.ToString(raw)?.Trim();
            if (string.IsNullOrWhiteSpace(featureName))
                return ToolResult.Error("FeatureName is required.");

            var impact = Reader.AnalyzeFeatureImpact(featureName);
            return impact == null
                ? ToolResult.Error($"Feature '{featureName}' was not found in the active model.")
                : ToolResult.Success(impact);
        }
    }
}
