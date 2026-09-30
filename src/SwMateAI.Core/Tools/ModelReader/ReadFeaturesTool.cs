using System.Collections.Generic;
using SolidWorks.Interop.sldworks;

namespace SwMateAI.Core.Tools.ModelReader
{
    public class ReadFeaturesTool : ModelReaderToolBase
    {
        public override string Name => "ReadFeatures";
        public override string Description => "Reads non-sketch features from the active document.";
        public ReadFeaturesTool(ISldWorks swApp) : base(swApp, requirePart: false) { }

        public override ToolResult Execute(Dictionary<string, object> parameters)
        {
            return ToolResult.Success(Reader.ReadFeatures());
        }
    }
}
