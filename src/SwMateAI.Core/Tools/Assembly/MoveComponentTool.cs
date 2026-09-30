using System;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;
using SwMateAI.Core.Models.Assembly;

namespace SwMateAI.Core.Tools.Assembly
{
    public class MoveComponentTool : AssemblyActionToolBase, IUndoableSwTool
    {
        private string _lastComponentName = string.Empty;
        private double[] _previousTransform;

        public MoveComponentTool(ISldWorks swApp) : base(swApp) { }
        public override string Name => "MoveComponent";
        public override string Description => "Moves an Assembly component to an absolute XYZ position while preserving orientation.";

        public override ToolResult Execute(Dictionary<string, object> parameters)
        {
            string name = Text(parameters, "ComponentName");
            if (string.IsNullOrWhiteSpace(name)) return ToolResult.Error("ComponentName is required.");
            var assembly = SwApp.ActiveDoc as IAssemblyDoc;
            var model = SwApp.ActiveDoc as IModelDoc2;
            var component = assembly?.GetComponentByName(name);
            if (assembly == null || model == null || component == null)
                return ToolResult.Error("Component not found in active Assembly: " + name);

            var current = component.Transform2;
            var data = current?.ArrayData as double[];
            if (data == null || data.Length < 13) return ToolResult.Error("Could not read component transform.");
            _previousTransform = (double[])data.Clone();
            _lastComponentName = component.Name2 ?? name;
            double x = Mm(parameters, "X"), y = Mm(parameters, "Y"), z = Mm(parameters, "Z");
            data[9] = x / 1000.0;
            data[10] = y / 1000.0;
            data[11] = z / 1000.0;

            var math = SwApp.GetMathUtility() as IMathUtility;
            var target = math?.CreateTransform(data) as MathTransform;
            if (target == null) return ToolResult.Error("Could not create target transform.");
            if (!component.SetTransformAndSolve2(target))
                return ToolResult.Error("SOLIDWORKS could not move the component.");

            model.EditRebuild3();
            var actual = component.Transform2?.ArrayData as double[];
            if (actual == null || Math.Abs(actual[9] - data[9]) > 1e-6 || Math.Abs(actual[10] - data[10]) > 1e-6 || Math.Abs(actual[11] - data[11]) > 1e-6)
                return ToolResult.Error("Component transform did not reach the requested XYZ position.");

            return ToolResult.Success(new AssemblyActionResult
            {
                Action = "MoveComponent",
                ComponentName = _lastComponentName,
                Path = component.GetPathName() ?? string.Empty,
                Xmm = x,
                Ymm = y,
                Zmm = z
            });
        }

        public ToolResult Undo()
        {
            if (string.IsNullOrWhiteSpace(_lastComponentName) || _previousTransform == null)
                return ToolResult.Error("There is no MoveComponent action to undo.");
            var assembly = SwApp.ActiveDoc as IAssemblyDoc;
            var model = SwApp.ActiveDoc as IModelDoc2;
            var component = assembly?.GetComponentByName(_lastComponentName);
            if (assembly == null || model == null || component == null)
                return ToolResult.Error("Moved component could not be found for Undo.");

            var math = SwApp.GetMathUtility() as IMathUtility;
            var transform = math?.CreateTransform(_previousTransform) as MathTransform;
            if (transform == null || !component.SetTransformAndSolve2(transform))
                return ToolResult.Error("SOLIDWORKS could not restore the previous component transform.");

            model.EditRebuild3();
            string restored = _lastComponentName;
            _lastComponentName = string.Empty;
            _previousTransform = null;
            return ToolResult.Success("Undo MoveComponent: restored " + restored);
        }
    }
}
