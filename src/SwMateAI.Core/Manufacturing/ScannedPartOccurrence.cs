namespace SwMateAI.Core.Manufacturing
{
    public class ScannedPartOccurrence
    {
        public string IdentityKey { get; set; } = string.Empty;
        public string ComponentName { get; set; } = string.Empty;
        public string SourcePath { get; set; } = string.Empty;
        public string ReferencedConfiguration { get; set; } = string.Empty;
        public string ModelTitle { get; set; } = string.Empty;
        public bool IsVirtual { get; set; }
        public bool IsLoaded { get; set; }
    }
}
