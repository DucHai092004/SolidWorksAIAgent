using System;

namespace SwMateAI.Core.Manufacturing
{
    public class StockClassifier
    {
        public string Classify(BreakdownItem item)
        {
            if (item == null || item.FinishedXmm <= 0 || item.FinishedYmm <= 0 || item.FinishedZmm <= 0)
            {
                if (item != null) item.StockClassificationBasis = "Missing finished bounding-box dimensions";
                return "Unclassified";
            }

            if (item.HasCylindricalFace && TryGetRoundBarDimensions(item, out var boxDiameter, out _))
            {
                double tolerance = Math.Max(0.5, boxDiameter * 0.05);
                if (item.LargestCylinderDiameterMm > 0 &&
                    Math.Abs(item.LargestCylinderDiameterMm - boxDiameter) <= tolerance)
                {
                    item.StockClassificationBasis = "Outer CAD cylinder diameter matches two transverse bounding-box axes";
                    return "Round Bar";
                }
            }

            double[] d = { item.FinishedXmm, item.FinishedYmm, item.FinishedZmm };
            Array.Sort(d);
            double small = d[0], middle = d[1], large = d[2];

            if (small <= middle * 0.25)
            {
                item.StockClassificationBasis = "Bounding-box rule: thin axis <= 25% of next axis";
                return "Plate";
            }
            if (large >= middle * 3.0 && Math.Abs(middle - small) / middle <= 0.15)
            {
                item.StockClassificationBasis = "Bounding-box rule: long axis + near-square cross-section";
                return "Square Bar";
            }
            if (large >= middle * 3.0)
            {
                item.StockClassificationBasis = "Bounding-box rule: long axis";
                return "Rectangular Bar";
            }

            item.StockClassificationBasis = "Bounding-box rule: general prismatic stock";
            return "Block";
        }

        public static bool TryGetRoundBarDimensions(BreakdownItem item, out double diameter, out double length)
        {
            diameter = length = 0;
            if (item == null) return false;
            double x = item.FinishedXmm, y = item.FinishedYmm, z = item.FinishedZmm;
            if (NearlyEqual(x, y)) { diameter = (x + y) / 2.0; length = z; return true; }
            if (NearlyEqual(x, z)) { diameter = (x + z) / 2.0; length = y; return true; }
            if (NearlyEqual(y, z)) { diameter = (y + z) / 2.0; length = x; return true; }
            return false;
        }

        private static bool NearlyEqual(double a, double b)
        {
            double max = Math.Max(Math.Abs(a), Math.Abs(b));
            return max > 0 && Math.Abs(a - b) / max <= 0.08;
        }
    }
}
