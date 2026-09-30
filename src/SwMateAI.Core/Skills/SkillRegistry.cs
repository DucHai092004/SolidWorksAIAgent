using System;
using System.Collections.Generic;
using System.Linq;

namespace SwMateAI.Core.Skills
{
    public class SkillRegistry
    {
        private readonly Dictionary<string, ISkill> _skills =
            new Dictionary<string, ISkill>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _aliases =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public IEnumerable<string> Names => _skills.Keys.OrderBy(x => x);

        public void Register(ISkill skill, IEnumerable<string> aliases = null)
        {
            if (skill == null) throw new ArgumentNullException(nameof(skill));
            _skills[skill.Name] = skill;

            if (aliases == null) return;
            foreach (var alias in aliases.Where(x => !string.IsNullOrWhiteSpace(x)))
                _aliases[alias] = skill.Name;
        }

        public bool TryGet(string name, out ISkill skill)
        {
            skill = null;
            if (string.IsNullOrWhiteSpace(name)) return false;
            if (_skills.TryGetValue(name, out skill)) return true;
            return _aliases.TryGetValue(name, out var canonical) &&
                   _skills.TryGetValue(canonical, out skill);
        }

        public string ResolveCanonicalName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;
            if (_skills.ContainsKey(name)) return name;
            return _aliases.TryGetValue(name, out var canonical) ? canonical : name;
        }

        public IReadOnlyList<SkillMetadata> GetMetadata()
        {
            return _skills.Values
                .Select(x => x.Metadata)
                .OrderBy(x => x.Category)
                .ThenBy(x => x.Name)
                .ToList()
                .AsReadOnly();
        }
    }
}
