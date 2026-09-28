using System;
using System.Collections.Generic;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SwMateAI.Core.Models.Assembly;

namespace SwMateAI.Core.Readers
{
    /// <summary>
    /// Read-only access to the active SOLIDWORKS Assembly.
    /// This reader never changes mates, component positions or suppression states.
    /// </summary>
    public class SolidWorksAssemblyReader
    {
        private readonly ISldWorks _swApp;

        public SolidWorksAssemblyReader(ISldWorks swApp)
        {
            _swApp = swApp ?? throw new ArgumentNullException(nameof(swApp));
        }

        private IModelDoc2 ActiveModel() => _swApp.ActiveDoc as IModelDoc2;

        private IAssemblyDoc ActiveAssembly()
        {
            var model = ActiveModel();
            if (model == null || model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
                return null;
            return model as IAssemblyDoc;
        }

        public AssemblyInfo ReadAssembly()
        {
            var model = ActiveModel();
            var assembly = ActiveAssembly();
            if (model == null || assembly == null) return null;

            var components = ReadComponents();
            var mates = ReadMates();
            return new AssemblyInfo
            {
                Name = model.GetTitle() ?? string.Empty,
                Configuration = model.ConfigurationManager.ActiveConfiguration?.Name ?? string.Empty,
                TopLevelComponentCount = assembly.GetComponentCount(true),
                TotalComponentCount = assembly.GetComponentCount(false),
                MateCount = mates.Count,
                SuppressedComponentCount = components.Count(x => x.IsSuppressed),
                LightweightComponentCount = assembly.GetLightWeightComponentCount(),
                HasUnloadedComponents = assembly.HasUnloadedComponents(),
                IsComponentTreeValid = assembly.IsComponentTreeValid()
            };
        }

        public List<AssemblyComponentInfo> ReadComponents()
        {
            var assembly = ActiveAssembly();
            var result = new List<AssemblyComponentInfo>();
            if (assembly == null) return result;

            var raw = assembly.GetComponents(false) as object[];
            if (raw == null) return result;

            foreach (var item in raw)
            {
                var component = item as IComponent2;
                if (component == null) continue;

                int suppression = component.GetSuppression2();
                var parent = component.GetParent();
                result.Add(new AssemblyComponentInfo
                {
                    Name = component.Name2 ?? string.Empty,
                    Path = component.GetPathName() ?? string.Empty,
                    ReferencedConfiguration = component.ReferencedConfiguration ?? string.Empty,
                    ParentName = parent?.Name2 ?? string.Empty,
                    Depth = GetDepth(component),
                    SuppressionState = suppression,
                    SuppressionStateName = Enum.GetName(typeof(swComponentSuppressionState_e), suppression) ?? suppression.ToString(),
                    IsSuppressed = component.IsSuppressed(),
                    IsVirtual = component.IsVirtual,
                    IsLoaded = component.GetModelDoc2() != null
                });
            }
            return result;
        }

        private static int GetDepth(IComponent2 component)
        {
            int depth = 0;
            var parent = component?.GetParent();
            while (parent != null && depth < 64)
            {
                depth++;
                parent = parent.GetParent();
            }
            return depth;
        }

        public List<AssemblyMateInfo> ReadMates()
        {
            var model = ActiveModel();
            var result = new List<AssemblyMateInfo>();
            if (model == null || ActiveAssembly() == null) return result;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var feature = model.FirstFeature() as IFeature;
            while (feature != null)
            {
                ReadMateFeatureRecursive(feature, result, seen);
                feature = feature.GetNextFeature() as IFeature;
            }
            return result;
        }

        private static void ReadMateFeatureRecursive(
            IFeature feature,
            List<AssemblyMateInfo> result,
            HashSet<string> seen)
        {
            if (feature == null) return;
            try
            {
                var mate = feature.GetSpecificFeature2() as IMate2;
                if (mate != null && seen.Add(feature.Name ?? string.Empty))
                    result.Add(ToMateInfo(feature, mate));
            }
            catch { }

            var sub = feature.GetFirstSubFeature() as IFeature;
            while (sub != null)
            {
                ReadMateFeatureRecursive(sub, result, seen);
                sub = sub.GetNextSubFeature() as IFeature;
            }
        }

        private static AssemblyMateInfo ToMateInfo(IFeature feature, IMate2 mate)
        {
            var info = new AssemblyMateInfo
            {
                Name = feature.Name ?? string.Empty,
                TypeId = mate.Type,
                TypeName = Enum.GetName(typeof(swMateType_e), mate.Type) ?? mate.Type.ToString(),
                AlignmentId = mate.Alignment,
                AlignmentName = mate.Alignment.ToString(),
                EntityCount = mate.GetMateEntityCount()
            };

            for (int i = 0; i < info.EntityCount; i++)
            {
                try
                {
                    var entity = mate.MateEntity(i);
                    string componentName = entity?.ReferenceComponent?.Name2 ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(componentName) &&
                        !info.Components.Contains(componentName, StringComparer.OrdinalIgnoreCase))
                        info.Components.Add(componentName);
                }
                catch { }
            }
            return info;
        }
    }
}
