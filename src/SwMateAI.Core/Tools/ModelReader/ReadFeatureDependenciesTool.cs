using System.Collections.Generic;
using SolidWorks.Interop.sldworks;

namespace SwMateAI.Core.Tools.ModelReader
{
    /// <summary>
    /// Read-only Phase 2 skill that exposes feature parent/child relationships.
    /// This is the foundation for later design-intent and impact analysis.
    /// </summary>
    public class ReadFeatureDependenciesTool : ModelReaderToolBase
    {
        public override string Name => "ReadFeatureDependencies";
        public override string Description => "Reads parent/child dependencies between features in the active model.";

        public ReadFeatureDependenciesTool(ISldWorks swApp)
            : base(swApp, requirePart: false) { }

        public override ToolResult Execute(Dictionary<string, object> parameters)
        {
            return ToolResult.Success(Reader.ReadFeatureDependencies());
        }
    }
}
