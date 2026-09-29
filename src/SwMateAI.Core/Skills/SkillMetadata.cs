using System.Collections.Generic;

namespace SwMateAI.Core.Skills
{
    public enum SkillRiskLevel
    {
        ReadOnly,
        Low,
        Medium,
        High
    }

    public enum SkillMaturity
    {
        Experimental,
        Pilot,
        Verified,
        Blocked
    }

    public class SkillMetadata
    {
        public string Name { get; set; } = string.Empty;
        public string ToolName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public SkillRiskLevel RiskLevel { get; set; } = SkillRiskLevel.Low;
        public SkillMaturity Maturity { get; set; } = SkillMaturity.Pilot;
        public bool HasLiveRegression { get; set; }
        public string VerifiedSolidWorksVersion { get; set; } = string.Empty;
        public string VerificationEvidence { get; set; } = string.Empty;
        public bool RequiresConfirmation { get; set; }
        public bool SupportsUndo { get; set; }
        public bool RequiresActiveDocument { get; set; }
        public bool RequiresPartDocument { get; set; }
        public bool RequiresAssemblyDocument { get; set; }
        public bool RequiresSelection { get; set; }
        public bool IsComposite { get; set; }
        public List<string> Aliases { get; } = new List<string>();
    }
}
