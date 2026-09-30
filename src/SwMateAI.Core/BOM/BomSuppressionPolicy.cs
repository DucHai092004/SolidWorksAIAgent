using SolidWorks.Interop.swconst;

namespace SwMateAI.Core.BOM
{
    public static class BomSuppressionPolicy
    {
        public static bool ShouldSkip(int suppressionState)
        {
            return suppressionState ==
                   (int)swComponentSuppressionState_e.swComponentSuppressed;
        }
    }
}
