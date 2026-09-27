using System.Collections.Generic;

namespace SwMateAI.Core.Planning
{
    public class Intent
    {
        public string Name { get; set; } = string.Empty;
        public string RawText { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public Dictionary<string, object> Parameters { get; } = new Dictionary<string, object>();
    }
}
