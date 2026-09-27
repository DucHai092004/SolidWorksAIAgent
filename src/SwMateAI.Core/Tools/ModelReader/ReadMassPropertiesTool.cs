using System.Collections.Generic;
using SolidWorks.Interop.sldworks;

namespace SwMateAI.Core.Tools.ModelReader
{
    public class ReadMassPropertiesTool : ModelReaderToolBase
    {
        public override string Name => "ReadMassProperties";
        public override string Description => "Reads mass, volume, surface area and density from the active Part.";
        public ReadMassPropertiesTool(ISldWorks swApp) : base(swApp, requirePart: true) { }

        public override ToolResult Execute(Dictionary<string, object> parameters)
        {
            return ToolResult.Success(Reader.ReadMassProperties());
        }
    }
}
