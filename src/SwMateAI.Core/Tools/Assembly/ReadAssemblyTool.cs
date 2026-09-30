using System.Collections.Generic;
using SolidWorks.Interop.sldworks;

namespace SwMateAI.Core.Tools.Assembly
{
    public class ReadAssemblyTool : AssemblyReaderToolBase
    {
        public override string Name => "ReadAssembly";
        public override string Description => "Reads a summary of the active Assembly.";

        public ReadAssemblyTool(ISldWorks swApp) : base(swApp) { }

        public override ToolResult Execute(Dictionary<string, object> parameters)
        {
            return ToolResult.Success(Reader.ReadAssembly());
        }
    }
}
