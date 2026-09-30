using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SwMateAI.Core.BOM
{
    public sealed class HierarchicalBomBuilder
    {
        private readonly ISldWorks _swApp;

        public HierarchicalBomBuilder(ISldWorks swApp)
        {
            _swApp = swApp ?? throw new ArgumentNullException(nameof(swApp));
        }

        public BomResult Build(BomBuildOptions options)
        {
            options = options ?? new BomBuildOptions();
            if (options.Mode == BomMode.LegacyFlat)
                return new BomBuilder(_swApp).Build();

            var result = new BomResult { Mode = options.Mode };
            var model = _swApp.ActiveDoc as IModelDoc2;
            var assembly = model as IAssemblyDoc;
            if (model == null || assembly == null ||
                model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
                return result;

            var activeConfiguration = model.ConfigurationManager?.ActiveConfiguration as IConfiguration;
            var root = activeConfiguration?.GetRootComponent3(true) as IComponent2;
            if (root == null) return result;

            var roots = new List<BomHierarchyNode>();
            foreach (var child in Children(root))
            {
                BomHierarchyNode node = BuildNode(child, result);
                if (node != null) roots.Add(node);
            }

            var resolved = BomHierarchyResolver.Resolve(roots, options);
            result.TotalOccurrences = resolved.Count;

            var grouped = resolved.GroupBy(x =>
                (options.Mode == BomMode.Indented ? x.Level : 0) + "|" +
                (x.Node.IdentityKey ?? string.Empty));

            int itemNo = 1;
            foreach (var group in grouped)
            {
                var firstOccurrence = group.First();
                var first = firstOccurrence.Node;
                var item = new BomItem
                {
                    ItemNumber = itemNo++,
                    Level = options.Mode == BomMode.Indented ? firstOccurrence.Level : 0,
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

        private BomHierarchyNode BuildNode(IComponent2 component, BomResult result)
        {
            if (component == null) return null;

            int suppressionState = component.GetSuppression();
            if (BomSuppressionPolicy.ShouldSkip(suppressionState))
            {
                result.SuppressedSkipped++;
                return null;
            }

            bool excluded = false;
            try { excluded = component.ExcludeFromBOM; }
            catch { }
            if (excluded)
            {
                result.ExcludedSkipped++;
                return null;
            }

            string path = component.GetPathName() ?? string.Empty;
            string config = component.ReferencedConfiguration ?? string.Empty;
            var referencedModel = component.GetModelDoc2() as IModelDoc2;
            string title = CleanCadName(
                referencedModel?.GetTitle() ??
                Path.GetFileName(path) ??
                component.Name2 ?? string.Empty);
            string type = ResolveType(referencedModel, path);
            string identity = !string.IsNullOrWhiteSpace(path)
                ? path.ToUpperInvariant() + "|" + config.ToUpperInvariant()
                : "VIRTUAL::" + title.ToUpperInvariant() + "|" + config.ToUpperInvariant();

            string fallback = CleanCadName(!string.IsNullOrWhiteSpace(path)
                ? Path.GetFileName(path)
                : title);
            string partNumber = CleanCadName(referencedModel == null
                ? fallback
                : FirstNonEmpty(
                    ReadProperty(referencedModel, config, "Part Number"),
                    ReadProperty(referencedModel, config, "PartNumber"),
                    fallback));
            string description = referencedModel == null
                ? string.Empty
                : FirstNonEmpty(
                    ReadProperty(referencedModel, config, "Description"),
                    ReadProperty(referencedModel, config, "DESCRIPTION"));

            string material = string.Empty;
            if (referencedModel is IPartDoc part)
            {
                string database;
                material = part.GetMaterialPropertyName2(config, out database) ?? string.Empty;
            }

            var node = new BomHierarchyNode
            {
                IdentityKey = identity,
                ComponentName = component.Name2 ?? string.Empty,
                Path = path,
                Configuration = config,
                PartNumber = partNumber,
                Description = description,
                Material = material,
                ComponentType = type,
                IsVirtual = component.IsVirtual,
                IsLoaded = referencedModel != null,
                ChildDisplay = ResolveChildDisplay(referencedModel, config)
            };

            if (string.Equals(type, "Assembly", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var child in Children(component))
                {
                    BomHierarchyNode childNode = BuildNode(child, result);
                    if (childNode != null) node.Children.Add(childNode);
                }
            }

            return node;
        }

        private static IEnumerable<IComponent2> Children(IComponent2 parent)
        {
            object[] raw = null;
            try { raw = parent?.GetChildren() as object[]; }
            catch { }
            if (raw == null) yield break;

            foreach (object value in raw)
            {
                var child = value as IComponent2;
                if (child != null) yield return child;
            }
        }

        private static BomChildDisplay ResolveChildDisplay(IModelDoc2 model, string configurationName)
        {
            if (model == null || model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
                return BomChildDisplay.Show;

            try
            {
                var configuration = model.GetConfigurationByName(configurationName) as IConfiguration;
                if (configuration == null) return BomChildDisplay.Show;

                switch ((swChildComponentInBOMOption_e)configuration.ChildComponentDisplayInBOM)
                {
                    case swChildComponentInBOMOption_e.swChildComponent_Hide:
                        return BomChildDisplay.Hide;
                    case swChildComponentInBOMOption_e.swChildComponent_Promote:
                        return BomChildDisplay.Promote;
                    default:
                        return BomChildDisplay.Show;
                }
            }
            catch
            {
                return BomChildDisplay.Show;
            }
        }

        private static string ResolveType(IModelDoc2 model, string path)
        {
            int type = model?.GetType() ?? 0;
            if (type == (int)swDocumentTypes_e.swDocPART ||
                path.EndsWith(".SLDPRT", StringComparison.OrdinalIgnoreCase)) return "Part";
            if (type == (int)swDocumentTypes_e.swDocASSEMBLY ||
                path.EndsWith(".SLDASM", StringComparison.OrdinalIgnoreCase)) return "Assembly";
            return "Unknown";
        }

        private static string ReadProperty(IModelDoc2 model, string config, string name)
        {
            string value = ReadPropertyFrom(model, config, name);
            return string.IsNullOrWhiteSpace(value)
                ? ReadPropertyFrom(model, string.Empty, name)
                : value;
        }

        private static string ReadPropertyFrom(IModelDoc2 model, string config, string name)
        {
            var manager = model?.Extension?.get_CustomPropertyManager(config ?? string.Empty);
            if (manager == null) return string.Empty;
            string raw, resolved;
            bool wasResolved, linked;
            manager.Get6(name, false, out raw, out resolved, out wasResolved, out linked);
            return string.IsNullOrWhiteSpace(resolved) ? raw ?? string.Empty : resolved;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (string value in values)
                if (!string.IsNullOrWhiteSpace(value)) return value;
            return string.Empty;
        }

        private static string CleanCadName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            string name = Path.GetFileName(value.Trim());
            string[] extensions =
            {
                ".sldprt", ".sldasm", ".slddrw", ".step", ".stp",
                ".iges", ".igs", ".x_t", ".x_b", ".sat"
            };
            bool removed;
            do
            {
                removed = false;
                foreach (string ext in extensions)
                {
                    if (!name.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) continue;
                    name = name.Substring(0, name.Length - ext.Length);
                    removed = true;
                    break;
                }
            } while (removed && !string.IsNullOrWhiteSpace(name));
            return name;
        }
    }
}
