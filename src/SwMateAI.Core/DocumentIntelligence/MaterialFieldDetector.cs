using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SwMateAI.Core.DocumentIntelligence
{
    internal static class MaterialFieldDetector
    {
        private static readonly string[] CodeHeaders =
        {
            "ma chi tiet", "ma ct", "ma sp", "part number", "part no",
            "part code", "drawing no", "drawing number", "code"
        };
        private static readonly string[] MaterialHeaders =
        {
            "vat lieu phoi", "vl phoi", "phoi", "stock material", "raw material",
            "material grade", "mac vat lieu", "material"
        };

        public static bool TryFindColumns(IReadOnlyList<string> cells, out int codeIndex, out int materialIndex)
        {
            codeIndex = materialIndex = -1;
            for (int i = 0; i < cells.Count; i++)
            {
                string h = NormalizeHeader(cells[i]);
                if (codeIndex < 0 && Matches(h, CodeHeaders)) codeIndex = i;
                if (materialIndex < 0 && Matches(h, MaterialHeaders)) materialIndex = i;
            }
            return codeIndex >= 0 && materialIndex >= 0 && codeIndex != materialIndex;
        }

        public static string NormalizeHeader(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            string formD = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(formD.Length);
            foreach (char c in formD)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
                sb.Append(c == 'đ' ? 'd' : c);
            }
            return string.Join(" ", sb.ToString().Normalize(NormalizationForm.FormC)
                .Replace("_", " ").Replace("-", " ").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
        }

        private static bool Matches(string value, IEnumerable<string> accepted)
        {
            foreach (string candidate in accepted)
                if (value.Equals(candidate, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
