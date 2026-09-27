namespace SwMateAI.Core.Skills
{
    public class SkillMetadata
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool RequiresConfirmation { get; set; }
        public bool SupportsUndo { get; set; }
    }
}
