using System.Collections.Generic;
using SolidWorks.Interop.sldworks;

namespace SwMateAI.Core.Tools.ModelReader
{
    public class ReadBoundingBoxTool : ModelReaderToolBase
    {
        public override string Name => "ReadBoundingBox";
        public override string Description => "Reads the overall Part bounding box in millimeters.";
        public ReadBoundingBoxTool(ISldWorks swApp) : base(swApp, requirePart: true) { }

        public override ToolResult Execute(Dictionary<string, object> parameters)
        {
            return ToolResult.Success(Reader.ReadBoundingBox());
        }
    }
}
