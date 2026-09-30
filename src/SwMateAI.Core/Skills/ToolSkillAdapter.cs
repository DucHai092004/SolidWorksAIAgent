using System.Collections.Generic;
using SwMateAI.Core.Agent;
using SwMateAI.Core.Tools;

namespace SwMateAI.Core.Skills
{
    public class ToolSkillAdapter : ISkill
    {
        private readonly ISwTool _tool;
        private readonly SkillMetadata _metadata;
        private ToolResult _lastResult;

        public ToolSkillAdapter(ISwTool tool, SkillMetadata metadata)
        {
            _tool = tool;
            _metadata = metadata;
        }
        public string Name => _metadata.Name;
        public string Description => _metadata.Description;
        public SkillMetadata Metadata => _metadata;
        public bool RequiresConfirmation => _metadata.RequiresConfirmation;

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

        public SkillResult Undo()
        {
            if (!Metadata.SupportsUndo || !(_tool is IUndoableSwTool undoable))
                return SkillResult.Failure($"Undo is not implemented for skill '{Name}' yet.");
            var result = undoable.Undo();
            return result.IsSuccess ? SkillResult.Success(result.Data) : SkillResult.Failure(result.ErrorMessage);
        }
    }
}
