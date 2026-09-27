using System.Collections.Generic;
using SolidWorks.Interop.sldworks;

namespace SwMateAI.Core.Tools.ModelReader
{
    public class ReadSketchesTool : ModelReaderToolBase
    {
        public override string Name => "ReadSketches";
        public override string Description => "Reads sketches from the active document.";
        public ReadSketchesTool(ISldWorks swApp) : base(swApp, requirePart: false) { }

        public override ToolResult Execute(Dictionary<string, object> parameters)
        {
            return ToolResult.Success(Reader.ReadSketches());
        }
    }
}
