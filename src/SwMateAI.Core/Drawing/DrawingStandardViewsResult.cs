using System.Collections.Generic;

namespace SwMateAI.Core.Drawing
{
    public class DrawingStandardViewsResult
    {
        public string ModelPath { get; set; } = string.Empty;
        public string Projection { get; set; } = string.Empty;
        public int AddedViewCount { get; set; }
        public int TotalModelViewCount { get; set; }
        public List<string> ViewNames { get; } = new List<string>();

        public override string ToString()
        {
            return "Standard views inserted: " + AddedViewCount +
                   " | projection " + Projection +
                   " | total model views " + TotalModelViewCount;
        }
    }
}
