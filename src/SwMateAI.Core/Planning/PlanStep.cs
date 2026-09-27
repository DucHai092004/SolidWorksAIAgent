using System.Collections.Generic;

namespace SwMateAI.Core.Planning
{
    public enum PlanStepStatus { Pending, Running, Completed, Failed, Skipped }

    public class PlanStep
    {
        public int Index { get; set; }
        public string SkillName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Dictionary<string, object> Parameters { get; } = new Dictionary<string, object>();
        public PlanStepStatus Status { get; set; } = PlanStepStatus.Pending;
        public string Error { get; set; } = string.Empty;
    }
}
