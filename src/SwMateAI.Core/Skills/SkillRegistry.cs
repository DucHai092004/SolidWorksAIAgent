using System;
using System.Collections.Generic;
using System.Linq;

namespace SwMateAI.Core.Skills
{
    public class SkillRegistry
    {
        private readonly Dictionary<string, ISkill> _skills = new Dictionary<string, ISkill>(StringComparer.OrdinalIgnoreCase);
        public IEnumerable<string> Names => _skills.Keys.OrderBy(x => x);

        public void Register(ISkill skill)
        {
            if (skill == null) throw new ArgumentNullException(nameof(skill));
            _skills[skill.Name] = skill;
        }

        public bool TryGet(string name, out ISkill skill) => _skills.TryGetValue(name ?? string.Empty, out skill);

        public IReadOnlyList<SkillMetadata> GetMetadata()
        {
            return _skills.Values.Select(x => new SkillMetadata
            {
                Name = x.Name,
                Description = x.Description,
                RequiresConfirmation = x.RequiresConfirmation,
                SupportsUndo = false
            }).ToList().AsReadOnly();
        }
    }
}
