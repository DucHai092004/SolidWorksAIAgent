using System.Collections.Generic;
using SwMateAI.Core.Agent;
using SwMateAI.Core.Tools;

namespace SwMateAI.Core.Skills
{
    public class ToolSkillAdapter : ISkill
    {
        private readonly ISwTool _tool;
        private ToolResult _lastResult;

        public ToolSkillAdapter(ISwTool tool) { _tool = tool; }
        public string Name => _tool.Name;
        public string Description => _tool.Description;
        public bool RequiresConfirmation => false;

        public bool CanExecute(AgentContext context, out string reason)
        {
            if (_tool is SwToolBase baseTool) return baseTool.CanExecute(out reason);
            reason = null;
            return true;
        }

        public SkillResult Execute(Dictionary<string, object> input)
        {
            _lastResult = _tool.Execute(input ?? new Dictionary<string, object>());
            return _lastResult.IsSuccess ? SkillResult.Success(_lastResult.Data) : SkillResult.Failure(_lastResult.ErrorMessage);
        }

        public bool Validate(out string reason)
        {
            if (_lastResult != null && _lastResult.IsSuccess) { reason = null; return true; }
            reason = _lastResult?.ErrorMessage ?? "Skill has not executed successfully.";
            return false;
        }

        public SkillResult Undo() => SkillResult.Failure($"Undo is not implemented for skill '{Name}' yet.");
    }
}
