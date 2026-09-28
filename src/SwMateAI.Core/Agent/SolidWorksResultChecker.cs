using System;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SwMateAI.Core.Agent
{
    public class SolidWorksResultChecker
    {
        private readonly ISldWorks _swApp;

        public SolidWorksResultChecker(ISldWorks swApp)
        {
            _swApp = swApp ?? throw new ArgumentNullException(nameof(swApp));
        }

        public ModelSnapshot Capture()
        {
            var snapshot = new ModelSnapshot();
            var model = _swApp.ActiveDoc as IModelDoc2;
            if (model == null) return snapshot;

            snapshot.HasDocument = true;
            snapshot.DocumentTitle = model.GetTitle() ?? string.Empty;
            snapshot.DocumentType = model.GetType();
            snapshot.FeatureCount = model.GetFeatureCount();
            if (model.GetType() == (int)swDocumentTypes_e.swDocASSEMBLY)
            {
                var assembly = model as IAssemblyDoc;
                snapshot.AssemblyComponentCount = assembly?.GetComponentCount(false) ?? 0;
            }
            if (model.GetType() == (int)swDocumentTypes_e.swDocPART)
            {
                var part = model as IPartDoc;
                var bodies = part?.GetBodies2((int)swBodyType_e.swSolidBody, true) as object[];
                snapshot.SolidBodyCount = bodies?.Length ?? 0;
            }

            return snapshot;
        }

        public bool Validate(string skillName, ModelSnapshot before, out string reason)
        {
            reason = null;
            var model = _swApp.ActiveDoc as IModelDoc2;
            if (model == null)
            {
                reason = "No active SOLIDWORKS document exists after execution.";
                return false;
            }

            if (skillName == "CreatePart")
                return RequirePart(model, out reason);

            if (skillName == "CreateSketch")
            {
                if (!RequirePart(model, out reason)) return false;
                if (model.SketchManager.ActiveSketch == null)
                {
                    reason = "Expected an active sketch after CreateSketch.";
                    return false;
                }
                return true;
            }

            if (skillName == "CreateRectangle" || skillName == "CreateCircle")
            {
                if (!RequirePart(model, out reason)) return false;
                if (model.SketchManager.ActiveSketch == null)
                {
                    reason = $"Expected an active sketch after {skillName}.";
                    return false;
                }
                return true;
            }

            if (skillName == "InsertComponent" || skillName == "MoveComponent" || skillName == "AddMate" || skillName == "ReplaceComponent")
            {
                if (!RequireAssembly(model, out reason)) return false;
                if (!model.EditRebuild3()) { reason = "SOLIDWORKS rebuild failed after Assembly action."; return false; }
                var assembly = model as IAssemblyDoc;
                if (assembly == null || !assembly.IsComponentTreeValid()) { reason = "Assembly component tree is invalid after action."; return false; }
                var after = Capture();
                if (skillName == "InsertComponent" && before != null && after.AssemblyComponentCount <= before.AssemblyComponentCount)
                { reason = "Assembly component count did not increase after InsertComponent."; return false; }
                return true;
            }

            if (RequiresFeatureValidation(skillName))
            {
                if (!RequirePart(model, out reason)) return false;
                if (!model.EditRebuild3())
                {
                    reason = "SOLIDWORKS rebuild failed after skill execution.";
                    return false;
                }

                if (!CheckFeatureTree(model, out reason)) return false;
                var after = Capture();
                if (after.SolidBodyCount <= 0)
                {
                    reason = "No solid body exists after the CAD operation.";
                    return false;
                }

                if (before != null && before.HasDocument &&
                    string.Equals(before.DocumentTitle, after.DocumentTitle, StringComparison.OrdinalIgnoreCase) &&
                    RequiresFeatureGrowth(skillName) && after.FeatureCount <= before.FeatureCount)
                {
                    reason = $"Feature count did not increase after {skillName}.";
                    return false;
                }
            }

            return true;
        }

        private static bool RequireAssembly(IModelDoc2 model, out string reason)
        {
            reason = null;
            if (model.GetType() == (int)swDocumentTypes_e.swDocASSEMBLY) return true;
            reason = "The active document is not an Assembly after execution.";
            return false;
        }

        private static bool RequirePart(IModelDoc2 model, out string reason)
        {
            reason = null;
            if (model.GetType() == (int)swDocumentTypes_e.swDocPART) return true;
            reason = "The active document is not a Part after execution.";
            return false;
        }

        private static bool RequiresFeatureValidation(string skillName)
        {
            return skillName == "Extrude" ||
                   skillName == "CreateExtrude" ||
                   skillName == "CutExtrude" ||
                   skillName == "CreateExtrudeCut" ||
                   skillName == "CreatePlate" ||
                   skillName == "CreatePlateWithHole" ||
                   skillName == "FilletPlateCorners" ||
                   skillName == "CreateFillet" ||
                   skillName == "ChamferPlateCorners" ||
                   skillName == "CreateChamfer" ||
                   skillName == "AddDimension" ||
                   skillName == "ModifyDimension";
        }

        private static bool RequiresFeatureGrowth(string skillName)
        {
            return skillName == "Extrude" ||
                   skillName == "CreateExtrude" ||
                   skillName == "CutExtrude" ||
                   skillName == "CreateExtrudeCut" ||
                   skillName == "FilletPlateCorners" ||
                   skillName == "CreateFillet" ||
                   skillName == "ChamferPlateCorners" ||
                   skillName == "CreateChamfer";
        }

        private static bool CheckFeatureTree(IModelDoc2 model, out string reason)
        {
            reason = null;
            var feature = model.FirstFeature() as IFeature;
            while (feature != null)
            {
                bool warning = false;
                int errorCode = feature.GetErrorCode2(out warning);
                if (errorCode != 0)
                {
                    reason = $"Feature '{feature.Name}' ({feature.GetTypeName2()}) has rebuild error code {errorCode}.";
                    return false;
                }
                feature = feature.GetNextFeature() as IFeature;
            }
            return true;
        }
    }
}
