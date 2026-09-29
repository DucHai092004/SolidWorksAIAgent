using System.Collections.Generic;

namespace SwMateAI.Core.DrawingUnderstanding
{
    public class DrawingUnderstandingResult
    {
        public string DocumentTitle { get; set; } = string.Empty;
        public string ActiveSheetName { get; set; } = string.Empty;
        public int SheetCount { get; set; }
        public int ViewCount { get; set; }
        public int ModelViewCount { get; set; }
        public int DimensionCount { get; set; }
        public int TableCount { get; set; }
        public int NoteCount { get; set; }
        public List<DrawingSheetInfo> Sheets { get; } = new List<DrawingSheetInfo>();
        public List<DrawingViewInfo> Views { get; } = new List<DrawingViewInfo>();
        public List<DrawingDimensionInfo> Dimensions { get; } = new List<DrawingDimensionInfo>();
    }

    public class DrawingSheetInfo
    {
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    public class DrawingViewInfo
    {
        public string Name { get; set; } = string.Empty;
        public string ReferencedDocument { get; set; } = string.Empty;
        public string ReferencedConfiguration { get; set; } = string.Empty;
        public bool IsSheetView { get; set; }
        public int DimensionCount { get; set; }
        public int TableCount { get; set; }
        public int NoteCount { get; set; }
    }

    public class DrawingDimensionInfo
    {
        public string Name { get; set; } = string.Empty;
        public string ViewName { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public double Value { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public string ToleranceType { get; set; } = string.Empty;
        public double ToleranceMin { get; set; }
        public double ToleranceMax { get; set; }
    }
}
