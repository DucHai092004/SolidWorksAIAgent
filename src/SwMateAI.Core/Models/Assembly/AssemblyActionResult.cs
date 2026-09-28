namespace SwMateAI.Core.Models.Assembly
{
    public class AssemblyActionResult
    {
        public string Action { get; set; } = string.Empty;
        public string ComponentName { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public double Xmm { get; set; }
        public double Ymm { get; set; }
        public double Zmm { get; set; }

        public override string ToString() =>
            $"{Action}: {ComponentName} @ X={Xmm:0.###}, Y={Ymm:0.###}, Z={Zmm:0.###} mm";
    }
}
