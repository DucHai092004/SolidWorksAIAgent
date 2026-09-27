using System;
using SwMateAI.Core.Common;
using SwMateAI.Core.Planning;
using SwMateAI.Core.Skills;

namespace SwMateAI.Core.Agent
{
    public class AgentOrchestrator
    {
        private readonly SolidWorksContextReader _contextReader;
        private readonly SkillRegistry _registry;
        private readonly IAgentLogger _logger;

        public AgentState State { get; } = new AgentState();
        public AgentContext LastContext { get; private set; }

        public AgentOrchestrator(SolidWorksContextReader contextReader, SkillRegistry registry, IAgentLogger logger)
        {
            _contextReader = contextReader ?? throw new ArgumentNullException(nameof(contextReader));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public AgentContext Observe()
        {
            State.Transition(AgentStage.Observing);
            LastContext = _contextReader.Read();
            _logger.Info($"Observed SOLIDWORKS: {LastContext.DocumentType} '{LastContext.DocumentName}', selection={LastContext.SelectedObjectCount}.");
            State.Transition(AgentStage.Idle);
            return LastContext;
        }

        public ExecutionResult ExecutePlan(TaskPlan plan, bool confirmed = false)
        {
            if (plan == null || !plan.IsValid) return Fail(plan, null, "Task plan is empty or invalid.", 0);

            State.ActivePlanId = plan.Id;
            State.Transition(AgentStage.Observing);
            LastContext = _contextReader.Read();
            State.Transition(AgentStage.Validating);
            _logger.Info($"Validating plan '{plan.Goal}' ({plan.Steps.Count} step(s)).");

            foreach (var step in plan.Steps)
            {
                if (!_registry.TryGet(step.SkillName, out var skill))
                    return Fail(plan, step, $"Skill '{step.SkillName}' is not registered.", 0);
                if (skill.RequiresConfirmation && !confirmed)
                    return Fail(plan, step, $"Skill '{skill.Name}' requires confirmation.", 0);
            }

            int completed = 0;
            foreach (var step in plan.Steps)
            {
                _registry.TryGet(step.SkillName, out var skill);

                // Re-observe before every step because earlier steps may create or change
                // the document/selection required by later skills.
                State.Transition(AgentStage.Observing);
                LastContext = _contextReader.Read();
                State.Transition(AgentStage.Validating);
                if (!skill.CanExecute(LastContext, out var reason))
                    return Fail(plan, step, $"Skill '{skill.Name}' cannot execute: {reason}", completed);

                State.Transition(AgentStage.Executing);
                step.Status = PlanStepStatus.Running;
                _logger.Info($"Executing step {step.Index}: {step.SkillName}.");

                var result = skill.Execute(step.Parameters);
                if (!result.IsSuccess) return Fail(plan, step, result.Error, completed);

                State.Transition(AgentStage.Checking);
                if (!skill.Validate(out var validationError))
                    return Fail(plan, step, $"Validation failed: {validationError}", completed);

                step.Status = PlanStepStatus.Completed;
                completed++;

                // Refresh context after a successful step so the next step sees the
                // actual SolidWorks state produced by this skill.
                LastContext = _contextReader.Read();
            }

            State.Transition(AgentStage.Completed);
            _logger.Info($"Plan completed successfully: {completed}/{plan.Steps.Count} step(s).");
            return new ExecutionResult { IsSuccess = true, Plan = plan, CompletedSteps = completed };
        }

        private ExecutionResult Fail(TaskPlan plan, PlanStep step, string error, int completed)
        {
            if (step != null) { step.Status = PlanStepStatus.Failed; step.Error = error ?? string.Empty; }
            State.Fail(error);
            _logger.Error(error ?? "Unknown agent error.");
            return new ExecutionResult { IsSuccess = false, Plan = plan, FailedStep = step, CompletedSteps = completed, Error = error ?? string.Empty };
        }
    }
}
