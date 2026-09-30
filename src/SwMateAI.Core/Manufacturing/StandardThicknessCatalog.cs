using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace SwMateAI.Core.Manufacturing
{
    /// <summary>Company/supplier controlled thickness table. No universal thickness standard is assumed.</summary>
    public class StandardThicknessCatalog
    {
        private readonly Dictionary<string, List<double>> _values =
            new Dictionary<string, List<double>>(StringComparer.OrdinalIgnoreCase);

        public static StandardThicknessCatalog Load(string path)
        {
            var catalog = new StandardThicknessCatalog();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return catalog;
            foreach (string line in File.ReadAllLines(path))
            {
                string text = line?.Trim();
                if (string.IsNullOrWhiteSpace(text) || text.StartsWith("#") ||
                    text.StartsWith("Material", StringComparison.OrdinalIgnoreCase)) continue;
                string[] fields = text.Split(',');
                if (fields.Length < 2) continue;

                string material = NormalizeMaterial(fields[0]);
                if (!double.TryParse(fields[1].Trim().Replace(',', '.'),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out double thickness) || thickness <= 0) continue;
                if (!catalog._values.TryGetValue(material, out var list))
                    catalog._values[material] = list = new List<double>();
                list.Add(thickness);
            }
            foreach (var list in catalog._values.Values) list.Sort();
            return catalog;
        }

        public bool TryGetNext(string material, double minimum, out double thickness)
        {
            thickness = 0;
            string key = NormalizeMaterial(material);
            if (!_values.TryGetValue(key, out var values)) return false;
            double found = values.FirstOrDefault(x => x + 1e-9 >= minimum);
            if (found <= 0) return false;
            thickness = found;
            return true;
        }

        private static string NormalizeMaterial(string value) =>
            (value ?? string.Empty).Trim().ToUpperInvariant();
    }
}
