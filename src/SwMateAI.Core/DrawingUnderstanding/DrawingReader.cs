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
                int dimensionCount = ReadDimensions(view, result);
                var info = new DrawingViewInfo
                {
                    Name = SafeViewName(view),
                    IsSheetView = isSheetView,
                    ReferencedDocument = isSheetView ? string.Empty : SafeReferencedModel(view),
                    ReferencedConfiguration = isSheetView ? string.Empty : SafeReferencedConfiguration(view),
                    DimensionCount = dimensionCount,
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

        private static int ReadDimensions(IView view, DrawingUnderstandingResult result)
        {
            int count = 0;
            try
            {
                var display = view.GetFirstDisplayDimension5() as IDisplayDimension;
                while (display != null)
                {
                    count++;
                    result.Dimensions.Add(ReadDimension(view, display));
                    display = display.GetNext5() as IDisplayDimension;
                }
            }
            catch { }
            return count;
        }

        private static DrawingDimensionInfo ReadDimension(IView view, IDisplayDimension display)
        {
            var info = new DrawingDimensionInfo
            {
                ViewName = SafeViewName(view),
                Type = SafeDimensionType(display),
                UnitKind = SafeDimensionUnitKind(display)
            };

            try
            {
                var dimension = display?.GetDimension2(0) as IDimension;
                if (dimension != null)
                {
                    try { info.Name = dimension.FullName ?? string.Empty; } catch { }
                    info.ValueSystem = SafeDimensionValue(dimension);
                    ReadTolerance(dimension, info);
                }
            }
            catch { }

            try
            {
                var annotation = display?.GetAnnotation() as IAnnotation;
                var position = annotation?.GetPosition();
                if (position is Array values && values.Length >= 2)
                {
                    info.XSystem = Convert.ToDouble(values.GetValue(0));
                    info.YSystem = Convert.ToDouble(values.GetValue(1));
                }
            }
            catch { }

            return info;
        }

        private static double SafeDimensionValue(IDimension dimension)
        {
            try
            {
                var raw = dimension.GetValue3((int)swInConfigurationOpts_e.swThisConfiguration, null);
                if (raw is Array values && values.Length > 0)
                    return Convert.ToDouble(values.GetValue(0));
                if (raw != null) return Convert.ToDouble(raw);
            }
            catch { }
            return 0d;
        }

        private static void ReadTolerance(IDimension dimension, DrawingDimensionInfo info)
        {
            try
            {
                var tolerance = dimension?.Tolerance as IDimensionTolerance;
                if (tolerance == null) return;
                info.ToleranceType = ((swTolType_e)tolerance.Type).ToString();
                double minValue = 0d;
                double maxValue = 0d;
                tolerance.GetMinValue2(out minValue);
                tolerance.GetMaxValue2(out maxValue);
                info.ToleranceMinSystem = minValue;
                info.ToleranceMaxSystem = maxValue;
            }
            catch { }
        }

        private static string SafeDimensionType(IDisplayDimension display)
        {
            try { return ((swDimensionType_e)display.Type2).ToString(); }
            catch { return string.Empty; }
        }

        private static string SafeDimensionUnitKind(IDisplayDimension display)
        {
            try
            {
                return display.Type2 == (int)swDimensionType_e.swAngularDimension
                    ? "AngleRad"
                    : "LengthM";
            }
            catch { return string.Empty; }
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
