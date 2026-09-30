using System;
using System.Collections.Generic;

namespace SwMateAI.Core.Agent
{
    public class AgentContext
    {
        public bool IsConnected { get; set; }
        public string SolidWorksVersion { get; set; } = string.Empty;
        public bool HasActiveDocument { get; set; }
        public string DocumentType { get; set; } = "None";
        public string DocumentName { get; set; } = string.Empty;
        public string DocumentPath { get; set; } = string.Empty;
        public string ActiveConfiguration { get; set; } = string.Empty;
        public int SelectedObjectCount { get; set; }
        public List<int> SelectedObjectTypes { get; } = new List<int>();
        public DateTime ObservedAt { get; set; } = DateTime.Now;
    }
}
