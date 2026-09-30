using System.Collections.Generic;
using SolidWorks.Interop.sldworks;

namespace SwMateAI.Core.Tools.ModelReader
{
    public class ReadFeatureTreeTool : ModelReaderToolBase
    {
        public override string Name => "ReadFeatureTree";
        public override string Description => "Reads the active document Feature Tree.";
        public ReadFeatureTreeTool(ISldWorks swApp) : base(swApp, requirePart: false) { }

        public override ToolResult Execute(Dictionary<string, object> parameters)
        {
            return ToolResult.Success(Reader.ReadFeatureTree());
        }
    }
}
