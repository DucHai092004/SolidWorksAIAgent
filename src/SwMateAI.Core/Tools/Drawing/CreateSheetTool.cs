using System;
using System.Collections.Generic;
using System.Globalization;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SwMateAI.Core.Drawing;

namespace SwMateAI.Core.Tools.Drawing
{
    /// <summary>
    /// Phase 6A.3: adds a new sheet to the active SOLIDWORKS Drawing.
    /// Supports ISO paper sizes A4-A0 and a configurable drawing scale.
    /// </summary>
    public class CreateSheetTool : SwToolBase
    {
        public CreateSheetTool(ISldWorks swApp) : base(swApp) { }

        public override string Name => "CreateSheet";
        public override string Description =>
            "Creates and activates a new sheet in the active Drawing with paper size and scale settings.";

        public override bool CanExecute(out string reason)
        {
            reason = null;
            var model = SwApp.ActiveDoc as IModelDoc2;
            if (model == null)
            {
                reason = "Open a Drawing before creating a sheet.";
                return false;
            }
            if (model.GetType() != (int)swDocumentTypes_e.swDocDRAWING)
            {
                reason = "The active document must be a Drawing.";
                return false;
            }

            return true;
        }

        public override ToolResult Execute(Dictionary<string, object> parameters)
        {
            try
            {
                var model = SwApp.ActiveDoc as IModelDoc2;
                var drawing = model as IDrawingDoc;
                if (drawing == null)
                    return ToolResult.Error("The active document is not a Drawing.");

                int beforeCount = drawing.GetSheetCount();
                string requestedName = GetString(parameters, "Name");
                string sheetName = string.IsNullOrWhiteSpace(requestedName)
                    ? BuildDefaultName(drawing, beforeCount + 1)
                    : requestedName.Trim();

                if (SheetExists(drawing, sheetName))
                    return ToolResult.Error("A sheet named '" + sheetName + "' already exists.");

                string paperSize = GetString(parameters, "PaperSize");
                if (string.IsNullOrWhiteSpace(paperSize)) paperSize = "A3";
                int paper = ResolvePaperSize(paperSize);
                if (paper < 0)
                    return ToolResult.Error("Unsupported paper size. Use A4, A3, A2, A1 or A0.");
                double scaleNum = GetDouble(parameters, "ScaleNumerator", 1.0);
                double scaleDen = GetDouble(parameters, "ScaleDenominator", 1.0);
                if (scaleNum <= 0 || scaleDen <= 0)
                    return ToolResult.Error("Drawing scale values must be greater than zero.");

                drawing.NewSheet(
                    sheetName,
                    (short)paper,
                    (short)swDwgTemplates_e.swDwgTemplateNone,
                    scaleNum,
                    scaleDen);

                bool activated = drawing.ActivateSheet(sheetName);
                var currentSheet = drawing.GetCurrentSheet() as ISheet;
                int afterCount = drawing.GetSheetCount();

                if (!activated || currentSheet == null ||
                    !string.Equals(currentSheet.GetName(), sheetName, StringComparison.OrdinalIgnoreCase))
                    return ToolResult.Error("The new sheet was not activated successfully.");

                if (afterCount <= beforeCount)
                    return ToolResult.Error("SOLIDWORKS did not increase the drawing sheet count.");

                return ToolResult.Success(new DrawingSheetResult
                {
                    SheetName = sheetName,
                    PaperSize = paperSize.ToUpperInvariant(),
                    ScaleNumerator = scaleNum,
                    ScaleDenominator = scaleDen,
                    TotalSheetCount = afterCount
                });
            }
            catch (Exception ex)
            {
                return ToolResult.Error("CreateSheet failed: " + ex.Message);
            }
        }
        private static string BuildDefaultName(IDrawingDoc drawing, int startIndex)
        {
            int index = Math.Max(startIndex, 1);
            string name;
            do
            {
                name = "Sheet" + index;
                index++;
            }
            while (SheetExists(drawing, name));
            return name;
        }

        private static bool SheetExists(IDrawingDoc drawing, string name)
        {
            var names = drawing.GetSheetNames() as object[];
            if (names == null) return false;
            foreach (var item in names)
            {
                if (string.Equals(item as string, name, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static int ResolvePaperSize(string value)
        {
            switch ((value ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "A4": return (int)swDwgPaperSizes_e.swDwgPaperA4size;
                case "A3": return (int)swDwgPaperSizes_e.swDwgPaperA3size;
                case "A2": return (int)swDwgPaperSizes_e.swDwgPaperA2size;
                case "A1": return (int)swDwgPaperSizes_e.swDwgPaperA1size;
                case "A0": return (int)swDwgPaperSizes_e.swDwgPaperA0size;
                default: return -1;
            }
        }

        private static string GetString(Dictionary<string, object> parameters, string key)
        {
            if (parameters == null || !parameters.TryGetValue(key, out var value) || value == null)
                return string.Empty;
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        private static double GetDouble(Dictionary<string, object> parameters, string key, double fallback)
        {
            if (parameters == null || !parameters.TryGetValue(key, out var value) || value == null)
                return fallback;

            if (value is double d) return d;
            if (value is float f) return f;
            if (value is int i) return i;
            if (double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture),
                NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                return parsed;
            if (double.TryParse(Convert.ToString(value, CultureInfo.CurrentCulture),
                NumberStyles.Float, CultureInfo.CurrentCulture, out parsed))
                return parsed;
            return fallback;
        }
    }
}
