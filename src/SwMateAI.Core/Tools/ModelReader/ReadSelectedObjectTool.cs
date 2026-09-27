using System.Collections.Generic;
using SolidWorks.Interop.sldworks;

namespace SwMateAI.Core.Tools.ModelReader
{
    public class ReadSelectedObjectTool : ModelReaderToolBase
    {
        public override string Name => "ReadSelectedObject";
        public override string Description => "Reads the objects currently selected in SOLIDWORKS.";
        public ReadSelectedObjectTool(ISldWorks swApp) : base(swApp, requirePart: false) { }

        public override ToolResult Execute(Dictionary<string, object> parameters)
        {
            return ToolResult.Success(Reader.ReadSelectedObjects());
        }
    }
}
