using SwMateAI.Core.Planning;

namespace SwMateAI.Core.Agent
{
    public class ExecutionResult
    {
        public bool IsSuccess { get; set; }
        public TaskPlan Plan { get; set; }
        public int CompletedSteps { get; set; }
        public PlanStep FailedStep { get; set; }
        public string Error { get; set; } = string.Empty;
    }
}
