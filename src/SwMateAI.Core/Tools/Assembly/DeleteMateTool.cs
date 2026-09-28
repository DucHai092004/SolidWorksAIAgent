using System;
using System.Collections.Generic;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SwMateAI.Core.Models.Assembly;
using SwMateAI.Core.Readers;

namespace SwMateAI.Core.Tools.Assembly
{
    public class DeleteMateTool : AssemblyActionToolBase, IUndoableSwTool
    {
        private MateSnapshot _snapshot;

        public DeleteMateTool(ISldWorks swApp) : base(swApp) { }
        public override string Name => "DeleteMate";
        public override string Description => "Deletes a supported Mate by name and keeps enough references to recreate it on Undo.";

        public override ToolResult Execute(Dictionary<string, object> parameters)
        {
            string name = Text(parameters, "MateName");
            if (string.IsNullOrWhiteSpace(name)) return ToolResult.Error("MateName is required.");

            var model = SwApp.ActiveDoc as IModelDoc2;
            var assembly = model as IAssemblyDoc;
            var feature = FindMateFeature(model, name);
            var mate = feature?.GetSpecificFeature2() as IMate2;
            if (model == null || assembly == null || feature == null || mate == null)
                return ToolResult.Error("Mate not found in active Assembly: " + name);

            if (!IsSafelyRestorable(mate.Type))
                return ToolResult.Error("DeleteMate safe Undo is not supported for this Mate type: " + mate.Type);
            var snapshot = CaptureSnapshot(feature, mate);
            if (snapshot.References.Count < 2)
                return ToolResult.Error("Mate references could not be captured safely; DeleteMate was cancelled.");

            model.ClearSelection2(true);
            if (!feature.Select2(false, 0))
                return ToolResult.Error("Could not select Mate for deletion.");
            if (!assembly.DeleteSelections(0))
                return ToolResult.Error("SOLIDWORKS could not delete the Mate.");

            model.EditRebuild3();
            var reader = new SolidWorksAssemblyReader(SwApp);
            if (reader.ReadMates().Any(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)))
                return ToolResult.Error("Mate still exists after DeleteMate.");

            _snapshot = snapshot;
            return ToolResult.Success(new AssemblyMateActionResult
            {
                Action = "DeleteMate",
                MateName = snapshot.Name,
                MateType = Enum.GetName(typeof(swMateType_e), snapshot.Type) ?? snapshot.Type.ToString(),
                DistanceMm = snapshot.DistanceMeters * 1000.0
            });
        }

        public ToolResult Undo()
        {
            if (_snapshot == null) return ToolResult.Error("There is no DeleteMate action to undo.");
            var model = SwApp.ActiveDoc as IModelDoc2;
            var assembly = model as IAssemblyDoc;
            if (model == null || assembly == null) return ToolResult.Error("Active document is not an Assembly.");
            model.ClearSelection2(true);
            for (int i = 0; i < _snapshot.References.Count; i++)
            {
                if (!SelectReference(model, _snapshot.References[i], i > 0))
                    return ToolResult.Error("Could not reselect Mate reference during Undo.");
            }

            var beforeNames = new HashSet<string>(new SolidWorksAssemblyReader(SwApp).ReadMates().Select(x => x.Name), StringComparer.OrdinalIgnoreCase);
            int errorStatus;
            var mate = assembly.AddMate5(
                _snapshot.Type,
                _snapshot.Alignment,
                _snapshot.Flipped,
                _snapshot.DistanceMeters,
                0, 0, 1, 1,
                0, 0, 0,
                false, false, 0,
                out errorStatus);
            if (mate == null || errorStatus != 0)
                return ToolResult.Error("SOLIDWORKS could not recreate the deleted Mate. Error status: " + errorStatus);

            model.EditRebuild3();
            var after = new SolidWorksAssemblyReader(SwApp).ReadMates();
            var restored = after.FirstOrDefault(x => !beforeNames.Contains(x.Name));
            if (restored == null)
                return ToolResult.Error("Mate was recreated but could not be identified after Undo.");

            var restoredFeature = FindMateFeature(model, restored.Name);
            if (restoredFeature != null)
            {
                try { restoredFeature.Name = _snapshot.Name; } catch { }
            }
            string originalName = _snapshot.Name;
            _snapshot = null;
            return ToolResult.Success("Undo DeleteMate: restored " + originalName);
        }
        private static MateSnapshot CaptureSnapshot(IFeature feature, IMate2 mate)
        {
            var snapshot = new MateSnapshot
            {
                Name = feature.Name ?? string.Empty,
                Type = mate.Type,
                Alignment = mate.Alignment,
                Flipped = mate.Flipped
            };

            if (snapshot.Type == (int)swMateType_e.swMateDISTANCE)
            {
                try
                {
                    var display = mate.DisplayDimension;
                    var dimension = display?.GetDimension2(0);
                    snapshot.DistanceMeters = dimension?.SystemValue ?? 0;
                }
                catch { return null; }
            }

            int count = mate.GetMateEntityCount();
            for (int i = 0; i < count; i++)
            {
                var reference = mate.MateEntity(i)?.Reference;
                if (!(reference is IEntity) && !(reference is IFeature)) return null;
                snapshot.References.Add(reference);
            }
            return snapshot;
        }
        private static bool SelectReference(IModelDoc2 model, object reference, bool append)
        {
            if (reference is IEntity entity)
            {
                var selection = model.SelectionManager as ISelectionMgr;
                var data = selection?.CreateSelectData();
                if (data != null) data.Mark = 1;
                return entity.Select4(append, data);
            }
            if (reference is IFeature feature)
                return feature.Select2(append, 1);
            return false;
        }

        private static bool IsSafelyRestorable(int type)
        {
            return type == (int)swMateType_e.swMateCOINCIDENT ||
                   type == (int)swMateType_e.swMateCONCENTRIC ||
                   type == (int)swMateType_e.swMatePARALLEL ||
                   type == (int)swMateType_e.swMatePERPENDICULAR ||
                   type == (int)swMateType_e.swMateTANGENT ||
                   type == (int)swMateType_e.swMateDISTANCE;
        }

        private static IFeature FindMateFeature(IModelDoc2 model, string name)
        {
            var feature = model?.FirstFeature() as IFeature;
            while (feature != null)
            {
                var found = FindRecursive(feature, name);
                if (found != null) return found;
                feature = feature.GetNextFeature() as IFeature;
            }
            return null;
        }

        private static IFeature FindRecursive(IFeature feature, string name)
        {
            if (feature == null) return null;
            if (string.Equals(feature.Name, name, StringComparison.OrdinalIgnoreCase)) return feature;
            var sub = feature.GetFirstSubFeature() as IFeature;
            while (sub != null)
            {
                var found = FindRecursive(sub, name);
                if (found != null) return found;
                sub = sub.GetNextSubFeature() as IFeature;
            }
            return null;
        }

        private sealed class MateSnapshot
        {
            public string Name { get; set; } = string.Empty;
            public int Type { get; set; }
            public int Alignment { get; set; }
            public bool Flipped { get; set; }
            public double DistanceMeters { get; set; }
            public List<object> References { get; } = new List<object>();
        }
    }
}
