namespace SwMateAI.Core.Models.Understanding
{
    public class FeatureInfo
    {
        public string Name { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public int ErrorCode { get; set; }
        public bool HasWarning { get; set; }
        public bool IsSketch { get; set; }
    }
}
