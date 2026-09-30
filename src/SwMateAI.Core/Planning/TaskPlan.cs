using System;
using System.Collections.Generic;

namespace SwMateAI.Core.Planning
{
    public class TaskPlan
    {
        public string Id { get; } = Guid.NewGuid().ToString("N");
        public string Goal { get; set; } = string.Empty;
        public List<PlanStep> Steps { get; } = new List<PlanStep>();
        public bool IsValid => Steps.Count > 0;
    }
}
