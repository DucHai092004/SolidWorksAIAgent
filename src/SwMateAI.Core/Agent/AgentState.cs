using System;

namespace SwMateAI.Core.Agent
{
    public enum AgentStage
    {
        Idle, Observing, Understanding, Planning, Validating,
        Executing, Checking, Completed, Failed
    }

    public class AgentState
    {
        public AgentStage Stage { get; private set; } = AgentStage.Idle;
        public string CurrentRequest { get; set; } = string.Empty;
        public string ActivePlanId { get; set; } = string.Empty;
        public string LastError { get; private set; } = string.Empty;
        public DateTime UpdatedAt { get; private set; } = DateTime.Now;

        public void Transition(AgentStage stage)
        {
            Stage = stage;
            UpdatedAt = DateTime.Now;
            if (stage != AgentStage.Failed) LastError = string.Empty;
        }

        public void Fail(string error)
        {
            LastError = error ?? string.Empty;
            Stage = AgentStage.Failed;
            UpdatedAt = DateTime.Now;
        }
    }
}
