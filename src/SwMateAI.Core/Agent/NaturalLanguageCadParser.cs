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
        public double HoleX { get; set; }
        public double HoleY { get; set; }
    }

    public static class NaturalLanguageCadParser
    {
        public static bool TryParse(string input, out NaturalLanguageCadCommand command, out string error)
        {
            command = null; error = null;
            if (string.IsNullOrWhiteSpace(input)) { error = "Command is empty."; return false; }

            string s = input.Trim().ToLowerInvariant()
                .Replace("×", "x").Replace("φ", "phi").Replace("ø", "phi");

            double w, h, t;
            var triple = Regex.Match(s, @"(?<w>\d+(?:[\.,]\d+)?)\s*(?:mm\s*)?x\s*(?<h>\d+(?:[\.,]\d+)?)\s*(?:mm\s*)?x\s*(?<t>\d+(?:[\.,]\d+)?)");
            if (triple.Success)
            {
                w = Number(triple.Groups["w"].Value);
                h = Number(triple.Groups["h"].Value);
                t = Number(triple.Groups["t"].Value);
            }
            else
            {
                var wh = Regex.Match(s, @"(?<w>\d+(?:[\.,]\d+)?)\s*(?:mm\s*)?x\s*(?<h>\d+(?:[\.,]\d+)?)");
                var thick = Regex.Match(s, @"(?:dày|day|thickness|thick\.?|t)\s*[:=]?\s*(?<t>\d+(?:[\.,]\d+)?)");
                if (!wh.Success || !thick.Success)
                {
                    error = "Could not find plate dimensions. Try '120 x 80 x 15 mm' or '120 x 80 mm, dày 15 mm'.";
                    return false;
                }
                w = Number(wh.Groups["w"].Value); h = Number(wh.Groups["h"].Value); t = Number(thick.Groups["t"].Value);
            }

            if (w <= 0 || h <= 0 || t <= 0) { error = "Plate dimensions must be greater than zero."; return false; }

            var hole = Regex.Match(s, @"(?:phi|diameter|dia|đường\s*kính|duong\s*kinh)\s*[:=]?\s*(?<d>\d+(?:[\.,]\d+)?)");
            if (!hole.Success)
            {
                command = new NaturalLanguageCadCommand { Intent = "CreatePlate", Width = w, Height = h, Thickness = t };
                return true;
            }

            double d = Number(hole.Groups["d"].Value);
            if (d <= 0 || d >= Math.Min(w, h)) { error = "Hole diameter is invalid for this plate."; return false; }

            double x = 0, y = 0;
            var mx = Regex.Match(s, @"(?:\bx|tọa\s*độ\s*x|toa\s*do\s*x)\s*[:=]?\s*(?<v>-?\d+(?:[\.,]\d+)?)");
            var my = Regex.Match(s, @"(?:\by|tọa\s*độ\s*y|toa\s*do\s*y)\s*[:=]?\s*(?<v>-?\d+(?:[\.,]\d+)?)");
            if (mx.Success) x = Number(mx.Groups["v"].Value);
            if (my.Success) y = Number(my.Groups["v"].Value);

            double margin = d / 2.0;
            if (Math.Abs(x) + margin > w / 2.0 || Math.Abs(y) + margin > h / 2.0)
            {
                error = "The requested hole position would place the hole outside the plate.";
                return false;
            }

            command = new NaturalLanguageCadCommand
            {
                Intent = "CreatePlateWithHole", Width = w, Height = h, Thickness = t,
                HoleDiameter = d, HoleDepth = t, HoleX = x, HoleY = y
            };
            return true;
        }

        private static double Number(string value) =>
            double.Parse(value.Replace(',', '.'), CultureInfo.InvariantCulture);
    }
}