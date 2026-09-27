using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace SwMateAI.Core.Agent
{
    public class CadHoleSpec
    {
        public double Diameter { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
    }

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
        public List<CadHoleSpec> Holes { get; } = new List<CadHoleSpec>();
        public double FilletRadius { get; set; }
        public double ChamferDistance { get; set; }
    }

    public static class NaturalLanguageCadParser
    {
        public static bool TryParse(string input, out NaturalLanguageCadCommand command, out string error)
        {
            command = null; error = null;
            if (string.IsNullOrWhiteSpace(input)) { error = "Command is empty."; return false; }

            string s = input.Trim().ToLowerInvariant()
                .Replace("×", "x").Replace("φ", "phi").Replace("ø", "phi");

            if (!TryPlateDimensions(s, out double w, out double h, out double t))
            {
                error = "Could not find plate dimensions. Try '120 x 80 x 15 mm' or '120 x 80 mm, dày 15 mm'.";
                return false;
            }
            if (w <= 0 || h <= 0 || t <= 0) { error = "Plate dimensions must be greater than zero."; return false; }

            var parsed = new NaturalLanguageCadCommand { Width = w, Height = h, Thickness = t, HoleDepth = t };

            var holeMatches = Regex.Matches(s, @"(?:lỗ\s*|lo\s*|hole\s*)?(?:phi|diameter|dia|đường\s*kính|duong\s*kinh)\s*[:=]?\s*(?<d>\d+(?:[\.,]\d+)?)");
            for (int i = 0; i < holeMatches.Count; i++)
            {
                var match = holeMatches[i];
                double d = Number(match.Groups["d"].Value);
                int segmentStart = match.Index + match.Length;
                int segmentEnd = i + 1 < holeMatches.Count ? holeMatches[i + 1].Index : s.Length;
                string segment = s.Substring(segmentStart, segmentEnd - segmentStart);
                double x = Coordinate(segment, "x");
                double y = Coordinate(segment, "y");

                if (d <= 0 || d >= Math.Min(w, h)) { error = "Hole diameter is invalid for this plate."; return false; }
                double margin = d / 2.0;
                if (Math.Abs(x) + margin > w / 2.0 || Math.Abs(y) + margin > h / 2.0)
                {
                    error = $"Hole Ø{d:0.###} at X={x:0.###}, Y={y:0.###} would be outside the plate.";
                    return false;
                }
                parsed.Holes.Add(new CadHoleSpec { Diameter = d, X = x, Y = y });
            }

            var fillet = Regex.Match(s, @"(?:bo\s*(?:tròn\s*)?(?:4\s*)?(?:góc|goc)|fillet)\s*(?:r\s*[:=]?\s*)?(?<r>\d+(?:[\.,]\d+)?)");
            var chamfer = Regex.Match(s, @"(?:vát|vat|chamfer)\s*(?:4\s*)?(?:góc|goc|mép|mep)?\s*(?<c>\d+(?:[\.,]\d+)?)");
            if (fillet.Success) parsed.FilletRadius = Number(fillet.Groups["r"].Value);
            if (chamfer.Success) parsed.ChamferDistance = Number(chamfer.Groups["c"].Value);

            if (parsed.FilletRadius > 0 && parsed.ChamferDistance > 0)
            {
                error = "Use either fillet or chamfer in one command, not both.";
                return false;
            }
            if (parsed.FilletRadius >= Math.Min(w, h) / 2.0 || parsed.ChamferDistance >= Math.Min(w, h) / 2.0)
            {
                error = "Corner treatment is too large for this plate.";
                return false;
            }

            parsed.Intent = parsed.Holes.Count > 0 ? "CreatePlateWithHole" : "CreatePlate";
            if (parsed.Holes.Count > 0)
            {
                parsed.HoleDiameter = parsed.Holes[0].Diameter;
                parsed.HoleX = parsed.Holes[0].X;
                parsed.HoleY = parsed.Holes[0].Y;
            }

            command = parsed;
            return true;
        }

        private static bool TryPlateDimensions(string s, out double w, out double h, out double t)
        {
            w = h = t = 0;
            var triple = Regex.Match(s, @"(?<w>\d+(?:[\.,]\d+)?)\s*(?:mm\s*)?x\s*(?<h>\d+(?:[\.,]\d+)?)\s*(?:mm\s*)?x\s*(?<t>\d+(?:[\.,]\d+)?)");
            if (triple.Success)
            {
                w = Number(triple.Groups["w"].Value);
                h = Number(triple.Groups["h"].Value);
                t = Number(triple.Groups["t"].Value);
                return true;
            }

            var wh = Regex.Match(s, @"(?<w>\d+(?:[\.,]\d+)?)\s*(?:mm\s*)?x\s*(?<h>\d+(?:[\.,]\d+)?)");
            var thick = Regex.Match(s, @"(?:dày|day|thickness|thick\.?|t)\s*[:=]?\s*(?<t>\d+(?:[\.,]\d+)?)");
            if (!wh.Success || !thick.Success) return false;

            w = Number(wh.Groups["w"].Value);
            h = Number(wh.Groups["h"].Value);
            t = Number(thick.Groups["t"].Value);
            return true;
        }

        private static double Coordinate(string segment, string axis)
        {
            var m = Regex.Match(segment, $@"(?:tọa\s*độ\s*|toa\s*do\s*)?\b{axis}\s*[:=]\s*(?<v>-?\d+(?:[\.,]\d+)?)");
            return m.Success ? Number(m.Groups["v"].Value) : 0;
        }

        private static double Number(string value) =>
            double.Parse(value.Replace(',', '.'), CultureInfo.InvariantCulture);
    }
}
