using System.Collections.Generic;
using SolidWorks.Interop.sldworks;

namespace SwMateAI.Core.Tools.Assembly
{
    public class ReadMatesTool : AssemblyReaderToolBase
    {
        public override string Name => "ReadMates";
        public override string Description => "Reads mate features and participating components in the active Assembly.";

        public ReadMatesTool(ISldWorks swApp) : base(swApp) { }

        public override ToolResult Execute(Dictionary<string, object> parameters)
        {
            return ToolResult.Success(Reader.ReadMates());
        }
    }
}
