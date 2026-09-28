namespace SwMateAI.Core.Drawing
{
    public class DrawingSectionResult
    {
        public string SourceViewName { get; set; } = string.Empty;
        public string SectionViewName { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Direction { get; set; } = string.Empty;
        public int TotalModelViewCount { get; set; }

        public override string ToString()
        {
            return "Section view created: " + SectionViewName +
                   " | source " + SourceViewName +
                   " | " + Direction + " cut";
        }
    }
}
