using System.Collections.Generic;

namespace SwMateAI.Core.Models.Assembly
{
    /// <summary>One physical interference reported by SOLIDWORKS.</summary>
    public class AssemblyInterferenceInfo
    {
        public int Index { get; set; }
        public double VolumeMm3 { get; set; }
        public bool IsPossibleInterference { get; set; }
        public bool IsFastener { get; set; }
        public List<string> Components { get; } = new List<string>();
    }

    public class AssemblyInterferenceResult
    {
        public int Count { get; set; }
        public List<AssemblyInterferenceInfo> Items { get; } = new List<AssemblyInterferenceInfo>();
    }
}
