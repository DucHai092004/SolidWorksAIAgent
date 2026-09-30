using System;
using System.Globalization;
using System.IO;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using A = DocumentFormat.OpenXml.Drawing;
using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;

namespace SwMateAI.Core.Manufacturing
{
    /// <summary>
    /// Writes the manufacturing breakdown directly as XLSX using OpenXML.
    /// Excel is never started. Existing image paths in column A are embedded.
    /// </summary>
    public class ExcelExporter
    {
        private const long ImageWidthEmu = 914400L;
        private const long ImageHeightEmu = 685800L;

        public string Export(BreakdownTable table, string outputPath)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentException("Output path is required.", nameof(outputPath));

            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

            using (var document = SpreadsheetDocument.Create(outputPath, SpreadsheetDocumentType.Workbook))
            {
                var workbookPart = document.AddWorkbookPart();
                workbookPart.Workbook = new Workbook();

                var stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
                stylesPart.Stylesheet = BuildStyles();
                stylesPart.Stylesheet.Save();

                var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
                var sheetData = new SheetData();
                var worksheet = new Worksheet();
                worksheet.Append(BuildColumns(table.Headers.Count));
                worksheet.Append(sheetData);

                sheetData.Append(BuildHeaderRow(table));
                for (int r = 0; r < table.Rows.Count; r++)
                    sheetData.Append(BuildDataRow(table.Rows[r], (uint)(r + 2)));

                uint lastRow = (uint)Math.Max(1, table.Rows.Count + 1);
                string lastColumn = ColumnName(Math.Max(1, table.Headers.Count));
                worksheet.Append(new AutoFilter { Reference = "A1:" + lastColumn + lastRow });

                // The Worksheet must exist on the part before AddImages appends the
                // drawing relationship. Previously this assignment happened after
                // AddImages, which caused a NullReferenceException whenever images existed.
                worksheetPart.Worksheet = worksheet;
                AddImages(worksheetPart, table);
                worksheetPart.Worksheet.Save();

                var sheets = workbookPart.Workbook.AppendChild(new Sheets());
                sheets.Append(new Sheet
                {
                    Id = workbookPart.GetIdOfPart(worksheetPart),
                    SheetId = 1,
                    Name = "Bang phoi"
                });
                workbookPart.Workbook.Save();
            }

            return outputPath;
        }

        private static Row BuildHeaderRow(BreakdownTable table)
        {
            var row = new Row { RowIndex = 1 };
            foreach (string header in table.Headers)
                row.Append(TextCell(header, 1));
            return row;
        }

        private static Row BuildDataRow(System.Collections.Generic.IList<object> values, uint rowIndex)
        {
            var row = new Row
            {
                RowIndex = rowIndex,
                Height = 58D,
                CustomHeight = true
            };

            for (int c = 0; c < values.Count; c++)
            {
                if (c == 0)
                {
                    row.Append(TextCell(string.Empty));
                    continue;
                }

                object value = values[c];
                if (value is int i) row.Append(NumberCell(i));
                else if (value is long l) row.Append(NumberCell(l));
                else if (value is double d) row.Append(NumberCell(d));
                else if (value is float f) row.Append(NumberCell(f));
                else if (value is decimal m) row.Append(NumberCell((double)m));
                else row.Append(TextCell(Convert.ToString(value) ?? string.Empty));
            }
            return row;
        }

        private static void AddImages(WorksheetPart worksheetPart, BreakdownTable table)
        {
            bool hasImages = false;
            foreach (var row in table.Rows)
            {
                string path = Convert.ToString(row.Count > 0 ? row[0] : null) ?? string.Empty;
                if (File.Exists(path)) { hasImages = true; break; }
            }
            if (!hasImages) return;

            var drawingsPart = worksheetPart.AddNewPart<DrawingsPart>();
            drawingsPart.WorksheetDrawing = new Xdr.WorksheetDrawing();
            uint pictureId = 1;

            for (int index = 0; index < table.Rows.Count; index++)
            {
                var row = table.Rows[index];
                string imagePath = Convert.ToString(row.Count > 0 ? row[0] : null) ?? string.Empty;
                if (!File.Exists(imagePath)) continue;

                try
                {
                    var imagePart = drawingsPart.AddImagePart(ImagePartType.Png);
                    using (var stream = File.OpenRead(imagePath)) imagePart.FeedData(stream);
                    string relId = drawingsPart.GetIdOfPart(imagePart);
                    drawingsPart.WorksheetDrawing.Append(BuildImageAnchor(relId, pictureId++, index + 1));
                }
                catch
                {
                    // One invalid image must not abort the table export.
                }
            }

            if (pictureId == 1) return;
            drawingsPart.WorksheetDrawing.Save();
            worksheetPart.Worksheet.Append(
                new DocumentFormat.OpenXml.Spreadsheet.Drawing
                {
                    Id = worksheetPart.GetIdOfPart(drawingsPart)
                });
        }

        private static Xdr.OneCellAnchor BuildImageAnchor(string relationshipId, uint pictureId, int zeroBasedExcelRow)
        {
            var nonVisual = new Xdr.NonVisualPictureProperties(
                new Xdr.NonVisualDrawingProperties { Id = pictureId, Name = "Stock Preview " + pictureId },
                new Xdr.NonVisualPictureDrawingProperties(new A.PictureLocks { NoChangeAspect = true }));

            var blipFill = new Xdr.BlipFill(
                new A.Blip { Embed = relationshipId, CompressionState = A.BlipCompressionValues.Print },
                new A.Stretch(new A.FillRectangle()));

            var shapeProperties = new Xdr.ShapeProperties(
                new A.Transform2D(
                    new A.Offset { X = 0L, Y = 0L },
                    new A.Extents { Cx = ImageWidthEmu, Cy = ImageHeightEmu }),
                new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle });

            return new Xdr.OneCellAnchor(
                new Xdr.FromMarker(
                    new Xdr.ColumnId("0"), new Xdr.ColumnOffset("0"),
                    new Xdr.RowId(zeroBasedExcelRow.ToString(CultureInfo.InvariantCulture)), new Xdr.RowOffset("0")),
                new Xdr.Extent { Cx = ImageWidthEmu, Cy = ImageHeightEmu },
                new Xdr.Picture(nonVisual, blipFill, shapeProperties),
                new Xdr.ClientData());
        }

        private static Columns BuildColumns(int count)
        {
            var columns = new Columns();
            for (uint i = 1; i <= Math.Max(1, count); i++)
            {
                double width;
                if (i == 1) width = 16;
                else if (i == 2 || i == 3) width = 26;
                else if (i == 4) width = 10;
                else if (i == 7 || i == 9) width = 28;
                else if (i == 11 || i == 12) width = 24;
                else width = 20;
                columns.Append(new Column { Min = i, Max = i, Width = width, CustomWidth = true });
            }
            return columns;
        }

        private static Cell TextCell(string value, uint styleIndex = 0)
        {
            return new Cell
            {
                DataType = CellValues.InlineString,
                StyleIndex = styleIndex,
                InlineString = new InlineString(new Text(Clean(value)) { Space = SpaceProcessingModeValues.Preserve })
            };
        }

        private static Cell NumberCell(double value)
        {
            return new Cell
            {
                DataType = CellValues.Number,
                CellValue = new CellValue(value.ToString("0.###", CultureInfo.InvariantCulture))
            };
        }

        private static Stylesheet BuildStyles()
        {
            var fonts = new Fonts(new Font(), new Font(new Bold()));
            var fills = new Fills(
                new Fill(new PatternFill { PatternType = PatternValues.None }),
                new Fill(new PatternFill { PatternType = PatternValues.Gray125 }),
                new Fill(new PatternFill(
                    new ForegroundColor { Rgb = "FFD9EAF7" },
                    new BackgroundColor { Indexed = 64U }) { PatternType = PatternValues.Solid }));
            var borders = new Borders(new Border());
            var formats = new CellFormats(
                new CellFormat(),
                new CellFormat
                {
                    FontId = 1,
                    FillId = 2,
                    BorderId = 0,
                    ApplyFont = true,
                    ApplyFill = true
                });
            return new Stylesheet(fonts, fills, borders, formats);
        }

        private static string ColumnName(int index)
        {
            var builder = new StringBuilder();
            while (index > 0)
            {
                index--;
                builder.Insert(0, (char)('A' + index % 26));
                index /= 26;
            }
            return builder.ToString();
        }

        private static string Clean(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            var builder = new StringBuilder(value.Length);
            foreach (char c in value)
                if (c == '\t' || c == '\n' || c == '\r' || c >= 0x20) builder.Append(c);
            return builder.ToString();
        }
    }
}
