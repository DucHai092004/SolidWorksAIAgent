namespace SwMateAI.UI.ViewModels
{
    /// <summary>
    /// UI-only representation of an Agent plan step.
    /// Keeps localization concerns out of SwMateAI.Core.
    /// </summary>
    public sealed class PlanStepUiItem
    {
        public int Index { get; set; }
        public string SkillName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool IsVerified { get; set; }
    }
}
