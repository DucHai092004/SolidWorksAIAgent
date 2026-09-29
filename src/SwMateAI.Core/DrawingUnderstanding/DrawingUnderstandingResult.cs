using System.Collections.Generic;

namespace SwMateAI.Core.DrawingUnderstanding
{
    public class DrawingUnderstandingResult
    {
        public string DrawingTitle { get; set; } = string.Empty;
        public int SheetCount { get; set; }
        public int ModelViewCount { get; set; }
        public int DimensionCount { get; set; }
        public int NoteCount { get; set; }
        public int TableCount { get; set; }
        public List<DrawingSheetInfo> Sheets { get; } = new List<DrawingSheetInfo>();
    }

    public class DrawingSheetInfo
    {
        public string Name { get; set; } = string.Empty;
        public double WidthMm { get; set; }
        public double HeightMm { get; set; }
        public List<DrawingViewInfo> Views { get; } = new List<DrawingViewInfo>();
        public List<DrawingNoteInfo> Notes { get; } = new List<DrawingNoteInfo>();
        public List<DrawingTableInfo> Tables { get; } = new List<DrawingTableInfo>();
    }

    public class DrawingViewInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Orientation { get; set; } = string.Empty;
        public string ReferencedModel { get; set; } = string.Empty;
        public string Configuration { get; set; } = string.Empty;
        public double Scale { get; set; }
        public List<DrawingDimensionInfo> Dimensions { get; } = new List<DrawingDimensionInfo>();
    }

    public class DrawingDimensionInfo
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayText { get; set; } = string.Empty;
        public bool IsReference { get; set; }
    }

    public class DrawingNoteInfo
    {
        public string Text { get; set; } = string.Empty;
        public string ViewName { get; set; } = string.Empty;
    }

    public class DrawingTableInfo
    {
        public string Title { get; set; } = string.Empty;
        public int Type { get; set; }
        public int Rows { get; set; }
        public int Columns { get; set; }
        public string ViewName { get; set; } = string.Empty;
        public List<List<string>> Cells { get; } = new List<List<string>>();
    }
}
