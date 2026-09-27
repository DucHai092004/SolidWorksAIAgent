using System.Collections.Generic;
using SolidWorks.Interop.sldworks;

namespace SwMateAI.Core.Tools.ModelReader
{
    public class ReadDimensionsTool : ModelReaderToolBase
    {
        public override string Name => "ReadDimensions";
        public override string Description => "Reads driving dimensions from the active document.";
        public ReadDimensionsTool(ISldWorks swApp) : base(swApp, requirePart: false) { }

        public override ToolResult Execute(Dictionary<string, object> parameters)
        {
            return ToolResult.Success(Reader.ReadDimensions());
        }
    }
}
