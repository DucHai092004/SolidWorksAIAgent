using System;
using System.Collections.Generic;
using System.IO;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SwMateAI.Core.Models.Assembly;

namespace SwMateAI.Core.Tools.Assembly
{
    public class InsertComponentTool : AssemblyActionToolBase, IUndoableSwTool
    {
        private string _lastInsertedName = string.Empty;

        public InsertComponentTool(ISldWorks swApp) : base(swApp) { }
        public override string Name => "InsertComponent";
        public override string Description => "Inserts a Part or Assembly component at an XYZ position in the active Assembly.";

        public override ToolResult Execute(Dictionary<string, object> parameters)
        {
            string path = Text(parameters, "Path");
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return ToolResult.Error("Component file does not exist: " + path);
            string ext = Path.GetExtension(path);
            if (!ext.Equals(".sldprt", StringComparison.OrdinalIgnoreCase) && !ext.Equals(".sldasm", StringComparison.OrdinalIgnoreCase))
                return ToolResult.Error("InsertComponent supports .SLDPRT and .SLDASM files only.");

            double x = Mm(parameters, "X"), y = Mm(parameters, "Y"), z = Mm(parameters, "Z");
            var assembly = SwApp.ActiveDoc as IAssemblyDoc;
            if (assembly == null) return ToolResult.Error("Active document is not an Assembly.");
            var component = assembly.AddComponent5(
                path,
                (int)swAddComponentConfigOptions_e.swAddComponentConfigOptions_CurrentSelectedConfig,
                string.Empty,
                false,
                string.Empty,
                x / 1000.0,
                y / 1000.0,
                z / 1000.0);
            if (component == null) return ToolResult.Error("SOLIDWORKS did not return the inserted component.");

            _lastInsertedName = component.Name2 ?? string.Empty;
            (SwApp.ActiveDoc as IModelDoc2)?.EditRebuild3();
            return ToolResult.Success(new AssemblyActionResult
            {
                Action = "InsertComponent",
                ComponentName = _lastInsertedName,
                Path = path,
                Xmm = x,
                Ymm = y,
                Zmm = z
            });
        }

        public ToolResult Undo()
        {
            if (string.IsNullOrWhiteSpace(_lastInsertedName))
                return ToolResult.Error("There is no InsertComponent action to undo.");
            var assembly = SwApp.ActiveDoc as IAssemblyDoc;
            var model = SwApp.ActiveDoc as IModelDoc2;
            var component = assembly?.GetComponentByName(_lastInsertedName);
            if (assembly == null || model == null || component == null)
                return ToolResult.Error("The inserted component could not be found for Undo.");

            model.ClearSelection2(true);
            if (!component.Select4(false, null, false))
                return ToolResult.Error("Could not select the inserted component for Undo.");
            if (!assembly.DeleteSelections(0))
                return ToolResult.Error("SOLIDWORKS could not delete the inserted component during Undo.");

            model.EditRebuild3();
            string deleted = _lastInsertedName;
            _lastInsertedName = string.Empty;
            return ToolResult.Success("Undo InsertComponent: removed " + deleted);
        }
    }
}
