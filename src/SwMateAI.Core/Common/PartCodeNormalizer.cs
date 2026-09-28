using System;
using System.IO;

namespace SwMateAI.Core.Common
{
    /// <summary>Normalizes CAD/document part codes without destroying dots inside the real code.</summary>
    public static class PartCodeNormalizer
    {
        private static readonly string[] CadExtensions =
        {
            ".sldprt", ".sldasm", ".slddrw", ".step", ".stp",
            ".iges", ".igs", ".x_t", ".x_b", ".sat"
        };

        public static string CleanDisplay(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            string name = Path.GetFileName(value.Trim().Trim('"', '\''));
            bool removed;
            do
            {
                removed = false;
                foreach (string ext in CadExtensions)
                {
                    if (!name.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) continue;
                    name = name.Substring(0, name.Length - ext.Length).Trim();
                    removed = true;
                    break;
                }
            } while (removed && name.Length > 0);
            return name;
        }

        public static string Normalize(string value)
        {
            string cleaned = CleanDisplay(value);
            if (cleaned.Length == 0) return string.Empty;
            cleaned = cleaned.Replace(" ", string.Empty)
                             .Replace("–", "-")
                             .Replace("—", "-");
            return cleaned.ToUpperInvariant();
        }
    }
}
