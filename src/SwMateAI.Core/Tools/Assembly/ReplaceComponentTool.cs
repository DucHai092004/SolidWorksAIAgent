using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SwMateAI.Core.Models.Assembly;

namespace SwMateAI.Core.Tools.Assembly
{
    public class ReplaceComponentTool : AssemblyActionToolBase, IUndoableSwTool
    {
        private string _oldPath = string.Empty;
        private string _oldConfig = string.Empty;
        private string _replacementPath = string.Empty;
        private string _resultComponentName = string.Empty;

        public ReplaceComponentTool(ISldWorks swApp) : base(swApp) { }
        public override string Name => "ReplaceComponent";
        public override string Description => "Replaces one selected Assembly component with another Part/Assembly file.";

        public override ToolResult Execute(Dictionary<string, object> parameters)
        {
            string name = Text(parameters, "ComponentName");
            string newPath = Text(parameters, "NewPath");
            if (string.IsNullOrWhiteSpace(name)) return ToolResult.Error("ComponentName is required.");
            if (string.IsNullOrWhiteSpace(newPath) || !File.Exists(newPath)) return ToolResult.Error("Replacement file does not exist: " + newPath);

            var model = SwApp.ActiveDoc as IModelDoc2;
            var assembly = model as IAssemblyDoc;
            IComponent2 component = assembly?.GetComponentByName(name);
            if (model == null || assembly == null || component == null)
                return ToolResult.Error("Component not found in active Assembly: " + name);
            _oldPath = component.GetPathName() ?? string.Empty;
            _oldConfig = component.ReferencedConfiguration ?? string.Empty;
            _replacementPath = newPath;

            model.ClearSelection2(true);
            if (!component.Select4(false, null, false))
                return ToolResult.Error("Could not select component for replacement.");

            bool ok = assembly.ReplaceComponents2(
                newPath,
                string.Empty,
                false,
                (int)swReplaceComponentsConfiguration_e.swReplaceComponentsConfiguration_MatchName,
                true);
            if (!ok) return ToolResult.Error("SOLIDWORKS could not replace the selected component.");

            model.EditRebuild3();
            var components = assembly.GetComponents(false) as object[];
            var replacement = components?.OfType<IComponent2>()
                .FirstOrDefault(x => string.Equals(x.GetPathName(), newPath, StringComparison.OrdinalIgnoreCase));
            if (replacement == null)
                return ToolResult.Error("Replacement completed but the new component could not be identified.");

            _resultComponentName = replacement.Name2 ?? name;
            return ToolResult.Success(new AssemblyActionResult
            {
                Action = "ReplaceComponent",
                ComponentName = _resultComponentName,
                Path = newPath
            });
        }
        public ToolResult Undo()
        {
            if (string.IsNullOrWhiteSpace(_oldPath) || string.IsNullOrWhiteSpace(_resultComponentName))
                return ToolResult.Error("There is no ReplaceComponent action to undo.");

            var model = SwApp.ActiveDoc as IModelDoc2;
            var assembly = model as IAssemblyDoc;
            IComponent2 component = assembly?.GetComponentByName(_resultComponentName);
            if (component == null && assembly != null)
            {
                var all = assembly.GetComponents(false) as object[];
                component = all?.OfType<IComponent2>()
                    .FirstOrDefault(x => string.Equals(x.GetPathName(), _replacementPath, StringComparison.OrdinalIgnoreCase));
            }
            if (model == null || assembly == null || component == null)
                return ToolResult.Error("Replacement component could not be found for Undo.");

            model.ClearSelection2(true);
            if (!component.Select4(false, null, false))
                return ToolResult.Error("Could not select replacement component for Undo.");
            bool ok = assembly.ReplaceComponents2(
                _oldPath,
                _oldConfig,
                false,
                (int)swReplaceComponentsConfiguration_e.swReplaceComponentsConfiguration_MatchName,
                true);
            if (!ok) return ToolResult.Error("SOLIDWORKS could not restore the original component.");

            model.EditRebuild3();
            string restored = _oldPath;
            _oldPath = _oldConfig = _replacementPath = _resultComponentName = string.Empty;
            return ToolResult.Success("Undo ReplaceComponent: restored " + restored);
        }
    }
}
