namespace SwMateAI.Core.Models.Assembly
{
    public class AssemblyMateActionResult
    {
        public string Action { get; set; } = string.Empty;
        public string MateName { get; set; } = string.Empty;
        public string MateType { get; set; } = string.Empty;
        public double DistanceMm { get; set; }

        public override string ToString() =>
            string.IsNullOrWhiteSpace(MateName)
                ? $"{Action}: {MateType}"
                : $"{Action}: {MateName} [{MateType}]";
    }
}
