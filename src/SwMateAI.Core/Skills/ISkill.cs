using System.Collections.Generic;
using SwMateAI.Core.Agent;

namespace SwMateAI.Core.Skills
{
    public interface ISkill
    {
        string Name { get; }
        string Description { get; }
        SkillMetadata Metadata { get; }
        bool RequiresConfirmation { get; }
        bool CanExecute(AgentContext context, out string reason);
        SkillResult Execute(Dictionary<string, object> input);
        bool Validate(out string reason);
        SkillResult Undo();
    }
}
