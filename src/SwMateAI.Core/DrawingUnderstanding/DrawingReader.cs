using System;
using System.Collections.Generic;
using System.IO;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SwMateAI.Core.DrawingUnderstanding
{
    public class DrawingReader
    {
        public DrawingUnderstandingResult Read(IModelDoc2 model)
        {
            var drawing = model as IDrawingDoc;
            if (drawing == null) throw new InvalidOperationException("The active document is not a Drawing.");

            var result = new DrawingUnderstandingResult
            {
                DrawingTitle = model.GetTitle() ?? string.Empty,
                SheetCount = drawing.GetSheetCount()
            };

            string originalSheet = (drawing.GetCurrentSheet() as ISheet)?.GetName() ?? string.Empty;
            var sheetNames = drawing.GetSheetNames() as object[];
            if (sheetNames == null) return result;

            foreach (object rawName in sheetNames)
            {
                string name = Convert.ToString(rawName) ?? string.Empty;
                if (name.Length == 0 || !drawing.ActivateSheet(name)) continue;
                var sheet = drawing.GetCurrentSheet() as ISheet;
                var info = new DrawingSheetInfo { Name = name };
                if (sheet != null)
                {
                    double width = 0, height = 0;
                    sheet.GetSize(ref width, ref height);
                    info.WidthMm = width * 1000.0;
                    info.HeightMm = height * 1000.0;
                }
                ReadSheet(drawing, info, result);
                result.Sheets.Add(info);
            }

            if (originalSheet.Length > 0) drawing.ActivateSheet(originalSheet);
            return result;
        }

        private static void ReadSheet(IDrawingDoc drawing, DrawingSheetInfo sheet, DrawingUnderstandingResult result)
        {
            var view = drawing.GetFirstView() as IView;
            while (view != null)
            {
                string viewName = view.GetName2() ?? string.Empty;
                ReadNotes(view, viewName, sheet, result);
                ReadTables(view, viewName, sheet, result);

                // The first view is the sheet itself; only subsequent views reference models.
                string referenced = view.GetReferencedModelName() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(referenced))
                {
                    var viewInfo = new DrawingViewInfo
                    {
                        Name = viewName,
                        Orientation = view.GetOrientationName() ?? string.Empty,
                        ReferencedModel = Path.GetFileName(referenced),
                        Configuration = view.ReferencedConfiguration ?? string.Empty,
                        Scale = view.ScaleDecimal
                    };
                    ReadDimensions(view, viewInfo, result);
                    sheet.Views.Add(viewInfo);
                    result.ModelViewCount++;
                }
                view = view.GetNextView() as IView;
            }
        }

        private static void ReadDimensions(IView view, DrawingViewInfo target, DrawingUnderstandingResult result)
        {
            var dimension = view.GetFirstDisplayDimension5() as IDisplayDimension;
            while (dimension != null)
            {
                target.Dimensions.Add(new DrawingDimensionInfo
                {
                    Name = dimension.GetNameForSelection() ?? string.Empty,
                    DisplayText = dimension.GetText((int)swDimensionTextParts_e.swDimensionTextAll) ?? string.Empty,
                    IsReference = dimension.IsReferenceDim()
                });
                result.DimensionCount++;
                dimension = dimension.GetNext5() as IDisplayDimension;
            }
        }

        private static void ReadNotes(IView view, string viewName, DrawingSheetInfo sheet, DrawingUnderstandingResult result)
        {
            foreach (object item in Items(view.GetNotes()))
            {
                var note = item as INote;
                if (note == null) continue;
                string text = note.GetText() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(text)) continue;
                sheet.Notes.Add(new DrawingNoteInfo { Text = text, ViewName = viewName });
                result.NoteCount++;
            }
        }

        private static void ReadTables(IView view, string viewName, DrawingSheetInfo sheet, DrawingUnderstandingResult result)
        {
            foreach (object item in Items(view.GetTableAnnotations()))
            {
                var table = item as ITableAnnotation;
                if (table == null) continue;
                var info = new DrawingTableInfo
                {
                    Title = table.Title ?? string.Empty,
                    Type = table.Type,
                    Rows = table.RowCount,
                    Columns = table.ColumnCount,
                    ViewName = viewName
                };
                for (int r = 0; r < table.RowCount; r++)
                {
                    var row = new List<string>();
                    for (int c = 0; c < table.ColumnCount; c++)
                        row.Add(table.get_DisplayedText(r, c) ?? string.Empty);
                    info.Cells.Add(row);
                }
                sheet.Tables.Add(info);
                result.TableCount++;
            }
        }

        private static IEnumerable<object> Items(object raw)
        {
            if (!(raw is Array array)) yield break;
            foreach (object item in array) yield return item;
        }
    }
}
