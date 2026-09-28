using System.Collections.Generic;
using SolidWorks.Interop.sldworks;

namespace SwMateAI.Core.Tools.Assembly
{
    public class CheckInterferenceTool : AssemblyReaderToolBase
    {
        public override string Name => "CheckInterference";
        public override string Description => "Calculates physical interferences in the active Assembly.";

        public CheckInterferenceTool(ISldWorks swApp) : base(swApp) { }

        public override ToolResult Execute(Dictionary<string, object> parameters)
        {
            return ToolResult.Success(Reader.CheckInterference());
        }
    }
}
