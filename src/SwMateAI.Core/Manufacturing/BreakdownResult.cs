using System.Collections.Generic;

namespace SwMateAI.Core.Manufacturing
{
    public class BreakdownResult
    {
        public List<BreakdownItem> Items { get; } = new List<BreakdownItem>();
        public int TotalPartOccurrences { get; set; }
        public int UniquePartCount => Items.Count;
        public int SuppressedSkipped { get; set; }
        public int UnloadedPartCount { get; set; }
        public double AllowancePerSideMm { get; set; }
    }
}
