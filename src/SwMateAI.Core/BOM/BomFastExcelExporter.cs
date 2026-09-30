using System;
using System.IO;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace SwMateAI.Core.BOM
{
    /// <summary>
    /// Fast BOM exporter that writes XLSX directly with OpenXML.
    /// It intentionally does not launch Excel or embed preview images.
    /// </summary>
    public class BomFastExcelExporter
    {
        public string Export(BomResult result, string path)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Output path is required.", nameof(path));

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

            using (var document = SpreadsheetDocument.Create(path, SpreadsheetDocumentType.Workbook))
            {
                var workbookPart = document.AddWorkbookPart();
                workbookPart.Workbook = new Workbook();

                var stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
                stylesPart.Stylesheet = BuildStyles();
                stylesPart.Stylesheet.Save();

                var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
                var sheetData = new SheetData();
                var worksheet = new Worksheet();
                worksheet.Append(BuildColumns());
                worksheet.Append(sheetData);
                worksheetPart.Worksheet = worksheet;

                sheetData.Append(BuildHeaderRow());
                foreach (var item in result.Items)
                    sheetData.Append(BuildDataRow(item));

                uint lastRow = (uint)Math.Max(1, result.Items.Count + 1);
                worksheet.Append(new AutoFilter { Reference = "A1:H" + lastRow });
                worksheetPart.Worksheet.Save();

                var sheets = workbookPart.Workbook.AppendChild(new Sheets());
                sheets.Append(new Sheet
                {
                    Id = workbookPart.GetIdOfPart(worksheetPart),
                    SheetId = 1,
                    Name = "BOM"
                });
                workbookPart.Workbook.Save();
            }

            return path;
        }

        private static Row BuildHeaderRow()
        {
            var row = new Row { RowIndex = 1 };
            string[] headers =
            {
                "Image", "Item", "Part Number", "Description",
                "Quantity", "Material", "Type", "Configuration"
            };

            foreach (string header in headers)
                row.Append(TextCell(header, 1));
            return row;
        }

        private static Row BuildDataRow(BomItem item)
        {
            var row = new Row();
            row.Append(TextCell(string.Empty));
            row.Append(NumberCell(item.ItemNumber));

            string partNumber = Clean(item.PartNumber);
            if (item.Level > 0)
                partNumber = new string(' ', Math.Min(12, item.Level) * 2) + partNumber;

            row.Append(TextCell(partNumber));
            row.Append(TextCell(Clean(item.Description)));
            row.Append(NumberCell(item.Quantity));
            row.Append(TextCell(Clean(item.Material)));
            row.Append(TextCell(Clean(item.ComponentType)));
            row.Append(TextCell(Clean(item.Configuration)));
            return row;
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

        private static Cell NumberCell(int value)
        {
            return new Cell
            {
                DataType = CellValues.Number,
                CellValue = new CellValue(value.ToString(System.Globalization.CultureInfo.InvariantCulture))
            };
        }

        private static Columns BuildColumns()
        {
            return new Columns(
                new Column { Min = 1, Max = 1, Width = 10, CustomWidth = true },
                new Column { Min = 2, Max = 2, Width = 8, CustomWidth = true },
                new Column { Min = 3, Max = 3, Width = 28, CustomWidth = true },
                new Column { Min = 4, Max = 4, Width = 40, CustomWidth = true },
                new Column { Min = 5, Max = 5, Width = 10, CustomWidth = true },
                new Column { Min = 6, Max = 6, Width = 22, CustomWidth = true },
                new Column { Min = 7, Max = 7, Width = 16, CustomWidth = true },
                new Column { Min = 8, Max = 8, Width = 20, CustomWidth = true });
        }

        private static Stylesheet BuildStyles()
        {
            var fonts = new Fonts(
                new Font(),
                new Font(new Bold()));

            var fills = new Fills(
                new Fill(new PatternFill { PatternType = PatternValues.None }),
                new Fill(new PatternFill { PatternType = PatternValues.Gray125 }),
                new Fill(new PatternFill(
                    new ForegroundColor { Rgb = "FFD9EAF7" },
                    new BackgroundColor { Indexed = 64U })
                { PatternType = PatternValues.Solid }));

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

        private static string Clean(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            var builder = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                if (c == '\t' || c == '\n' || c == '\r' || c >= 0x20)
                    builder.Append(c);
            }
            return builder.ToString();
        }
    }
}
