using System;
using System.Collections.Generic;
using System.IO;

namespace SwMateAI.Core.DocumentIntelligence
{
    public class ProjectDocumentDiscovery
    {
        private static readonly HashSet<string> Excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".git", ".vs", "bin", "obj", "SW-MATE_AI_Output", "PartImages",
            "codestack-master", "node_modules", "packages"
        };

        public IReadOnlyList<string> Discover(string root)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return result;
            string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            Walk(fullRoot, fullRoot, result);
            return result;
        }

        private static void Walk(string root, string current, List<string> result)
        {
            foreach (string file in SafeFiles(current))
            {
                string ext = Path.GetExtension(file);
                if (ext.Equals(".xlsx", StringComparison.OrdinalIgnoreCase) ||
                    ext.Equals(".csv", StringComparison.OrdinalIgnoreCase) ||
                    ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
                    result.Add(file);
            }

            foreach (string child in SafeDirectories(current))
            {
                var info = new DirectoryInfo(child);
                if (Excluded.Contains(info.Name) || (info.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                string full = Path.GetFullPath(child).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) Walk(root, full, result);
            }
        }

        private static IEnumerable<string> SafeFiles(string path)
        {
            try { return Directory.GetFiles(path); }
            catch { return Array.Empty<string>(); }
        }

        private static IEnumerable<string> SafeDirectories(string path)
        {
            try { return Directory.GetDirectories(path); }
            catch { return Array.Empty<string>(); }
        }
    }
}
