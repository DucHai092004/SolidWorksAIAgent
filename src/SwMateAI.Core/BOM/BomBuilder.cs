using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SwMateAI.Core.BOM
{
    public class BomBuilder
    {
        private readonly ISldWorks _swApp;
        public BomBuilder(ISldWorks swApp) { _swApp = swApp; }

        public BomResult Build()
        {
            var result = new BomResult();
            var model = _swApp.ActiveDoc as IModelDoc2;
            var assembly = model as IAssemblyDoc;
            if (model == null || assembly == null || model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
                return result;

            var raw = assembly.GetComponents(false) as object[];
            if (raw == null) return result;
            var occurrences = new List<ComponentRecord>();

            foreach (var obj in raw)
            {
                var component = obj as IComponent2;
                if (component == null) continue;
                if (component.IsSuppressed()) { result.SuppressedSkipped++; continue; }
                occurrences.Add(ToRecord(component));
            }
            result.TotalOccurrences = occurrences.Count;

            int itemNo = 1;
            foreach (var group in occurrences.GroupBy(x => x.IdentityKey).OrderBy(x => x.Key))
            {
                var first = group.First();
                var item = new BomItem
                {
                    ItemNumber = itemNo++,
                    PartNumber = first.PartNumber,
                    Description = first.Description,
                    Quantity = group.Count(),
                    Material = first.Material,
                    ComponentType = first.ComponentType,
                    Configuration = first.Configuration,
                    SourcePath = first.Path,
                    RepresentativeComponentName = first.ComponentName,
                    IsVirtual = first.IsVirtual,
                    IsLoaded = first.IsLoaded
                };
                if (!item.IsLoaded) result.UnloadedCount++;
                result.Items.Add(item);
            }
            return result;
        }

        private static ComponentRecord ToRecord(IComponent2 component)
        {
            string path = component.GetPathName() ?? string.Empty;
            string config = component.ReferencedConfiguration ?? string.Empty;
            var model = component.GetModelDoc2() as IModelDoc2;
            string title = model?.GetTitle() ?? Path.GetFileNameWithoutExtension(path) ?? component.Name2 ?? string.Empty;
            string type = ResolveType(model, path);
            bool isVirtual = component.IsVirtual;
            string identity = !string.IsNullOrWhiteSpace(path)
                ? path.ToUpperInvariant() + "|" + config.ToUpperInvariant()
                : "VIRTUAL::" + title.ToUpperInvariant() + "|" + config.ToUpperInvariant();

            string fallback = !string.IsNullOrWhiteSpace(path) ? Path.GetFileNameWithoutExtension(path) : title;
            string partNumber = model == null ? fallback : FirstNonEmpty(ReadProperty(model, config, "Part Number"), ReadProperty(model, config, "PartNumber"), fallback);
            string description = model == null ? string.Empty : FirstNonEmpty(ReadProperty(model, config, "Description"), ReadProperty(model, config, "DESCRIPTION"));
            string material = string.Empty;
            if (model is IPartDoc part)
            {
                string database;
                material = part.GetMaterialPropertyName2(config, out database) ?? string.Empty;
            }

            return new ComponentRecord
            {
                IdentityKey = identity,
                ComponentName = component.Name2 ?? string.Empty,
                Path = path,
                Configuration = config,
                PartNumber = partNumber,
                Description = description,
                Material = material,
                ComponentType = type,
                IsVirtual = isVirtual,
                IsLoaded = model != null
            };
        }

        private static string ResolveType(IModelDoc2 model, string path)
        {
            int type = model?.GetType() ?? 0;
            if (type == (int)swDocumentTypes_e.swDocPART || path.EndsWith(".SLDPRT", StringComparison.OrdinalIgnoreCase)) return "Part";
            if (type == (int)swDocumentTypes_e.swDocASSEMBLY || path.EndsWith(".SLDASM", StringComparison.OrdinalIgnoreCase)) return "Assembly";
            return "Unknown";
        }

        private static string ReadProperty(IModelDoc2 model, string config, string name)
        {
            string value = ReadPropertyFrom(model, config, name);
            return string.IsNullOrWhiteSpace(value) ? ReadPropertyFrom(model, string.Empty, name) : value;
        }

        private static string ReadPropertyFrom(IModelDoc2 model, string config, string name)
        {
            var manager = model?.Extension?.get_CustomPropertyManager(config ?? string.Empty);
            if (manager == null) return string.Empty;
            string raw, resolved; bool wasResolved, linked;
            manager.Get6(name, false, out raw, out resolved, out wasResolved, out linked);
            return string.IsNullOrWhiteSpace(resolved) ? raw ?? string.Empty : resolved;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (var value in values) if (!string.IsNullOrWhiteSpace(value)) return value;
            return string.Empty;
        }

        private sealed class ComponentRecord
        {
            public string IdentityKey { get; set; } = string.Empty;
            public string ComponentName { get; set; } = string.Empty;
            public string Path { get; set; } = string.Empty;
            public string Configuration { get; set; } = string.Empty;
            public string PartNumber { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string Material { get; set; } = string.Empty;
            public string ComponentType { get; set; } = string.Empty;
            public bool IsVirtual { get; set; }
            public bool IsLoaded { get; set; }
        }
    }
}
