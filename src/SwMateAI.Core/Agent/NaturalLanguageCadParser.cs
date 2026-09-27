using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace SwMateAI.Core.Agent
{
    public class NaturalLanguageCadCommand
    {
        public string Intent { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double Thickness { get; set; }
        public double HoleDiameter { get; set; }
        public double HoleDepth { get; set; }
    }

    /// <summary>
    /// Phase 3 deterministic parser. Converts simple Vietnamese/English
    /// plate-with-hole commands into structured CAD parameters without an external AI API.
    /// </summary>
    public static class NaturalLanguageCadParser
    {
        public static bool TryParse(string input, out NaturalLanguageCadCommand command, out string error)
        {
            command = null;
            error = null;
            if (string.IsNullOrWhiteSpace(input)) { error = "Command is empty."; return false; }

            string s = input.Trim().ToLowerInvariant()
                .Replace("×", "x")
                .Replace("φ", "phi")
                .Replace("ø", "phi");

            var dims = Regex.Match(s, @"(?<w>\d+(?:[\.,]\d+)?)\s*(?:mm\s*)?x\s*(?<h>\d+(?:[\.,]\d+)?)\s*(?:mm\s*)?x\s*(?<t>\d+(?:[\.,]\d+)?)");
            if (!dims.Success)
            {
                error = "Could not find plate dimensions. Use a format such as 120 x 80 x 15 mm.";
                return false;
            }

            var hole = Regex.Match(s, @"(?:phi|diameter|dia|đường\s*kính|duong\s*kinh)\s*[:=]?\s*(?<d>\d+(?:[\.,]\d+)?)");
            if (!hole.Success)
            {
                error = "Could not find the hole diameter. Example: lỗ phi 12 or hole diameter 12.";
                return false;
            }

            double w = Number(dims.Groups["w"].Value);
            double h = Number(dims.Groups["h"].Value);
            double t = Number(dims.Groups["t"].Value);
            double d = Number(hole.Groups["d"].Value);
            if (w <= 0 || h <= 0 || t <= 0 || d <= 0) { error = "All dimensions must be greater than zero."; return false; }
            if (d >= Math.Min(w, h)) { error = "Hole diameter must be smaller than plate width and height."; return false; }

            command = new NaturalLanguageCadCommand
            {
                Intent = "CreatePlateWithHole",
                Width = w, Height = h, Thickness = t,
                HoleDiameter = d, HoleDepth = t
            };
            return true;
        }

        private static double Number(string value)
        {
            return double.Parse(value.Replace(',', '.'), CultureInfo.InvariantCulture);
        }
    }
}