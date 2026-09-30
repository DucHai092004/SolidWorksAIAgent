namespace SwMateAI.Core.Models.Understanding
{
    public class BoundingBoxInfo
    {
        public double Xmm { get; set; }
        public double Ymm { get; set; }
        public double Zmm { get; set; }
        public override string ToString() => $"{Xmm:0.###} x {Ymm:0.###} x {Zmm:0.###} mm";
    }
}
