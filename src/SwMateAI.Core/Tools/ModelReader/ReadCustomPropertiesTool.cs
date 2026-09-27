using System.Collections.Generic;
using SolidWorks.Interop.sldworks;

namespace SwMateAI.Core.Tools.ModelReader
{
    public class ReadCustomPropertiesTool : ModelReaderToolBase
    {
        public override string Name => "ReadCustomProperties";
        public override string Description => "Reads custom properties from the active document.";
        public ReadCustomPropertiesTool(ISldWorks swApp) : base(swApp, requirePart: false) { }

        public override ToolResult Execute(Dictionary<string, object> parameters)
        {
            return ToolResult.Success(Reader.ReadCustomProperties());
        }
    }
}
