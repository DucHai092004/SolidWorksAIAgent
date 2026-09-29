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
        public List<DrawingNoteInfo> Notes { get; } = new List<DrawingNoteInfo>();
        public List<DrawingTableInfo> Tables { get; } = new List<DrawingTableInfo>();
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
        public string UnitKind { get; set; } = string.Empty;
        public double ValueSystem { get; set; }
        public double XSystem { get; set; }
        public double YSystem { get; set; }
        public string ToleranceType { get; set; } = string.Empty;
        public double ToleranceMinSystem { get; set; }
        public double ToleranceMaxSystem { get; set; }
    }

    public class DrawingNoteInfo
    {
        public string ViewName { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
    }

    public class DrawingTableInfo
    {
        public string ViewName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public int RowCount { get; set; }
        public int ColumnCount { get; set; }
        public List<List<string>> Rows { get; } = new List<List<string>>();
    }
}
