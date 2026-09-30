using System;
using System.Collections.Generic;

namespace SwMateAI.Core.BOM
{
    public static class BomHierarchyResolver
    {
        public static IReadOnlyList<BomResolvedOccurrence> Resolve(
            IEnumerable<BomHierarchyNode> roots,
            BomBuildOptions options)
        {
            var result = new List<BomResolvedOccurrence>();
            if (roots == null) return result;
            options = options ?? new BomBuildOptions();

            foreach (var root in roots)
                ResolveNode(root, options, 0, result);

            return result;
        }

        private static void ResolveNode(
            BomHierarchyNode node,
            BomBuildOptions options,
            int level,
            List<BomResolvedOccurrence> result)
        {
            if (node == null || node.IsSuppressed || node.ExcludeFromBom) return;

            bool isAssembly = string.Equals(node.ComponentType, "Assembly", StringComparison.OrdinalIgnoreCase);
            bool isPart = string.Equals(node.ComponentType, "Part", StringComparison.OrdinalIgnoreCase);
            BomChildDisplay display = options.RespectChildDisplay
                ? node.ChildDisplay
                : BomChildDisplay.Show;

            switch (options.Mode)
            {
                case BomMode.TopLevel:
                    ResolveTopLevel(node, isAssembly, level, display, options, result);
                    break;

                case BomMode.PartsOnly:
                    ResolvePartsOnly(node, isAssembly, isPart, display, options, result);
                    break;

                case BomMode.Indented:
                    ResolveIndented(node, isAssembly, level, display, options, result);
                    break;

                default:
                    Add(node, level, result);
                    foreach (var child in node.Children)
                        ResolveNode(child, options, level, result);
                    break;
            }
        }

        private static void ResolveTopLevel(
            BomHierarchyNode node,
            bool isAssembly,
            int level,
            BomChildDisplay display,
            BomBuildOptions options,
            List<BomResolvedOccurrence> result)
        {
            if (!isAssembly)
            {
                Add(node, 0, result);
                return;
            }

            if (display == BomChildDisplay.Promote)
            {
                foreach (var child in node.Children)
                    ResolveNode(child, options, 0, result);
                return;
            }

            // Top-level BOM intentionally does not expand Show/Hide children.
            Add(node, 0, result);
        }

        private static void ResolvePartsOnly(
            BomHierarchyNode node,
            bool isAssembly,
            bool isPart,
            BomChildDisplay display,
            BomBuildOptions options,
            List<BomResolvedOccurrence> result)
        {
            if (isPart)
            {
                Add(node, 0, result);
                return;
            }

            if (!isAssembly) return;
            if (display == BomChildDisplay.Hide) return;

            // Show and Promote both expose descendant parts in a Parts-only BOM.
            foreach (var child in node.Children)
                ResolveNode(child, options, 0, result);
        }

        private static void ResolveIndented(
            BomHierarchyNode node,
            bool isAssembly,
            int level,
            BomChildDisplay display,
            BomBuildOptions options,
            List<BomResolvedOccurrence> result)
        {
            if (!isAssembly)
            {
                Add(node, level, result);
                return;
            }

            if (display == BomChildDisplay.Promote)
            {
                foreach (var child in node.Children)
                    ResolveNode(child, options, level, result);
                return;
            }

            Add(node, level, result);
            if (display == BomChildDisplay.Hide) return;

            foreach (var child in node.Children)
                ResolveNode(child, options, level + 1, result);
        }

        private static void Add(BomHierarchyNode node, int level, List<BomResolvedOccurrence> result)
        {
            result.Add(new BomResolvedOccurrence
            {
                Node = node,
                Level = level
            });
        }
    }
}
