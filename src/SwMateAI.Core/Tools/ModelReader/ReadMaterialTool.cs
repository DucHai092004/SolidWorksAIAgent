using System.Collections.Generic;
using SolidWorks.Interop.sldworks;

namespace SwMateAI.Core.Tools.ModelReader
{
    public class ReadMaterialTool : ModelReaderToolBase
    {
        public override string Name => "ReadMaterial";
        public override string Description => "Reads the current material from the active Part.";
        public ReadMaterialTool(ISldWorks swApp) : base(swApp, requirePart: true) { }

        public override ToolResult Execute(Dictionary<string, object> parameters)
        {
            return ToolResult.Success(Reader.ReadMaterial());
        }
    }
}
