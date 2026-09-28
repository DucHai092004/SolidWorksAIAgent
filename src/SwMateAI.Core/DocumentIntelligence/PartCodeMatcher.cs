using System;
using System.Collections.Generic;
using System.Linq;
using SwMateAI.Core.Common;

namespace SwMateAI.Core.DocumentIntelligence
{
    public class PartCodeMatch
    {
        public MaterialSourceRecord Record { get; set; }
        public double Confidence { get; set; }
        public string Method { get; set; } = string.Empty;
        public bool AutoAccept { get; set; }
    }

    public class PartCodeMatcher
    {
        public PartCodeMatch Find(string partCode, IEnumerable<MaterialSourceRecord> records)
        {
            string target = PartCodeNormalizer.Normalize(partCode);
            if (target.Length == 0 || records == null) return null;
            var list = records.Where(x => !string.IsNullOrWhiteSpace(x.NormalizedPartCode)).ToList();
            var exact = list.Where(x => x.NormalizedPartCode == target).ToList();
            if (exact.Count > 0)
                return new PartCodeMatch { Record = exact[0], Confidence = 1.0, Method = "EXACT", AutoAccept = true };

            var ranked = list.Select(x => new { Record = x, Score = Similarity(target, x.NormalizedPartCode) })
                .OrderByDescending(x => x.Score).Take(2).ToList();

            if (ranked.Count == 0 || ranked[0].Score < 0.85) return null;
            double gap = ranked.Count > 1 ? ranked[0].Score - ranked[1].Score : 1.0;
            bool safeDigits = DigitSignature(target) == DigitSignature(ranked[0].Record.NormalizedPartCode);
            bool auto = ranked[0].Score >= 0.95 && gap >= 0.03 && safeDigits;
            return new PartCodeMatch
            {
                Record = ranked[0].Record,
                Confidence = ranked[0].Score,
                Method = auto ? "FUZZY_SAFE" : "FUZZY_REVIEW",
                AutoAccept = auto
            };
        }

        private static double Similarity(string a, string b)
        {
            int distance = Levenshtein(a ?? string.Empty, b ?? string.Empty);
            int max = Math.Max(a?.Length ?? 0, b?.Length ?? 0);
            return max == 0 ? 1.0 : 1.0 - (double)distance / max;
        }

        private static string DigitSignature(string value) =>
            new string((value ?? string.Empty).Where(char.IsDigit).ToArray());

        private static int Levenshtein(string a, string b)
        {
            int[] previous = new int[b.Length + 1];
            int[] current = new int[b.Length + 1];
            for (int j = 0; j <= b.Length; j++) previous[j] = j;
            for (int i = 1; i <= a.Length; i++)
            {
                current[0] = i;
                for (int j = 1; j <= b.Length; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
                }
                var temp = previous; previous = current; current = temp;
            }
            return previous[b.Length];
        }
    }
}
