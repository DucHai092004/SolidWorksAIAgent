using System.Collections.Generic;
using SolidWorks.Interop.sldworks;

namespace SwMateAI.Core.Tools.Assembly
{
    public class ReadComponentsTool : AssemblyReaderToolBase
    {
        public override string Name => "ReadComponents";
        public override string Description => "Reads all component occurrences in the active Assembly.";

        public ReadComponentsTool(ISldWorks swApp) : base(swApp) { }

        public override ToolResult Execute(Dictionary<string, object> parameters)
        {
            return ToolResult.Success(Reader.ReadComponents());
        }
    }
}
