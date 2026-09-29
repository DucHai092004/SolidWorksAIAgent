using System;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SwMateAI.Core.DrawingUnderstanding
{
    public sealed class DrawingReader
    {
        private readonly ISldWorks _swApp;

        public DrawingReader(ISldWorks swApp)
        {
            _swApp = swApp;
        }

        public DrawingUnderstandingResult Read()
        {
            var result = new DrawingUnderstandingResult();
            var model = _swApp?.ActiveDoc as IModelDoc2;
            var drawing = model as IDrawingDoc;
            if (model == null || drawing == null || model.GetType() != (int)swDocumentTypes_e.swDocDRAWING)
                return result;

            result.DocumentTitle = model.GetTitle() ?? string.Empty;
            var originalSheet = drawing.GetCurrentSheet() as ISheet;
            string originalSheetName = SafeSheetName(originalSheet);
            result.ActiveSheetName = originalSheetName;

            var sheetNames = drawing.GetSheetNames() as string[] ?? Array.Empty<string>();
            result.SheetCount = sheetNames.Length;

            try
            {
                foreach (string sheetName in sheetNames)
                {
                    result.Sheets.Add(new DrawingSheetInfo
                    {
                        Name = sheetName ?? string.Empty,
                        IsActive = string.Equals(sheetName, originalSheetName, StringComparison.OrdinalIgnoreCase)
                    });

                    if (!drawing.ActivateSheet(sheetName)) continue;
                    ReadActiveSheetViews(drawing, result);
                }
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(originalSheetName))
                {
                    try { drawing.ActivateSheet(originalSheetName); } catch { }
                }
            }

            return result;
        }

        private static void ReadActiveSheetViews(IDrawingDoc drawing, DrawingUnderstandingResult result)
        {
            var view = drawing.GetFirstView() as IView;
            bool isSheetView = true;
            while (view != null)
            {
                var info = new DrawingViewInfo
                {
                    Name = SafeViewName(view),
                    IsSheetView = isSheetView,
                    ReferencedDocument = isSheetView ? string.Empty : SafeReferencedModel(view),
                    ReferencedConfiguration = isSheetView ? string.Empty : SafeReferencedConfiguration(view),
                    DimensionCount = CountDimensions(view),
                    TableCount = CountTables(view),
                    NoteCount = CountNotes(view)
                };

                result.ViewCount++;
                if (!isSheetView) result.ModelViewCount++;
                result.DimensionCount += info.DimensionCount;
                result.TableCount += info.TableCount;
                result.NoteCount += info.NoteCount;
                result.Views.Add(info);

                isSheetView = false;
                view = view.GetNextView() as IView;
            }
        }

        private static int CountDimensions(IView view)
        {
            int count = 0;
            try
            {
                var dimension = view.GetFirstDisplayDimension5() as IDisplayDimension;
                while (dimension != null)
                {
                    count++;
                    dimension = dimension.GetNext5() as IDisplayDimension;
                }
            }
            catch { }
            return count;
        }

        private static int CountTables(IView view)
        {
            try
            {
                var raw = view.GetTableAnnotations();
                return raw is Array tables ? tables.Length : 0;
            }
            catch { return 0; }
        }

        private static int CountNotes(IView view)
        {
            try { return view.GetNoteCount(); }
            catch { return 0; }
        }

        private static string SafeSheetName(ISheet sheet)
        {
            try { return sheet?.GetName() ?? string.Empty; }
            catch { return string.Empty; }
        }

        private static string SafeViewName(IView view)
        {
            try { return view?.GetName2() ?? string.Empty; }
            catch { return string.Empty; }
        }

        private static string SafeReferencedModel(IView view)
        {
            try { return view?.GetReferencedModelName() ?? string.Empty; }
            catch { return string.Empty; }
        }

        private static string SafeReferencedConfiguration(IView view)
        {
            try { return view?.ReferencedConfiguration ?? string.Empty; }
            catch { return string.Empty; }
        }
    }
}
