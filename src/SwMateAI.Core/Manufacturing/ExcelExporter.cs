using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using A = DocumentFormat.OpenXml.Drawing;
using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;

namespace SwMateAI.Core.Manufacturing
{
    /// <summary>
    /// Writes the manufacturing breakdown directly as an XLSX package.
    /// Microsoft Excel / Office COM automation is not required.
    /// The first table column is treated as an image path and embedded into column A.
    /// </summary>
    public class ExcelExporter
    {
        private const long EmusPerPixel = 9525L;
        private const int ThumbnailWidthPx = 76;
        private const int ThumbnailHeightPx = 56;

        public string Export(BreakdownTable table, string outputPath)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentException("Output path is required.", nameof(outputPath));

            string fullPath = Path.GetFullPath(outputPath);
            if (!string.Equals(Path.GetExtension(fullPath), ".xlsx", StringComparison.OrdinalIgnoreCase))
                fullPath += ".xlsx";

            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

            if (File.Exists(fullPath))
                throw new IOException("The manufacturing workbook already exists: " + fullPath);

            try
            {
                using (var document = SpreadsheetDocument.Create(fullPath, SpreadsheetDocumentType.Workbook))
                {
                    WorkbookPart workbookPart = document.AddWorkbookPart();
                    workbookPart.Workbook = new Workbook();
                    AddStyles(workbookPart);

                    WorksheetPart worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
                    var sheetData = new SheetData();
                    worksheetPart.Worksheet = new Worksheet(BuildColumns(table.Headers.Count), sheetData);

                    var sheets = workbookPart.Workbook.AppendChild(new Sheets());
                    sheets.Append(new Sheet
                    {
                        Id = workbookPart.GetIdOfPart(worksheetPart),
                        SheetId = 1U,
                        Name = "Bang phoi"
                    });

                    WriteHeader(sheetData, table.Headers);

                    var imageRows = new List<Tuple<uint, string>>();
                    for (int r = 0; r < table.Rows.Count; r++)
                    {
                        uint rowIndex = (uint)(r + 2);
                        IList<object> values = table.Rows[r] ?? new List<object>();
                        WriteRow(sheetData, values, table.Headers.Count, rowIndex);

                        string imagePath = values.Count > 0
                            ? Convert.ToString(values[0], CultureInfo.InvariantCulture) ?? string.Empty
                            : string.Empty;
                        if (!string.IsNullOrWhiteSpace(imagePath) && File.Exists(imagePath))
                            imageRows.Add(Tuple.Create(rowIndex, imagePath));
                    }

                    if (imageRows.Count > 0)
                        EmbedImages(worksheetPart, imageRows);

                    if (table.Headers.Count > 0 && table.Rows.Count >= 0)
                    {
                        string lastColumn = ColumnName(table.Headers.Count);
                        worksheetPart.Worksheet.Append(new AutoFilter
                        {
                            Reference = "A1:" + lastColumn + Math.Max(1, table.Rows.Count + 1)
                        });
                    }

                    worksheetPart.Worksheet.Save();
                    workbookPart.Workbook.Save();
                }
            }
            catch
            {
                try { if (File.Exists(fullPath)) File.Delete(fullPath); } catch { }
                throw;
            }

            var info = new FileInfo(fullPath);
            if (!info.Exists || info.Length <= 0)
                throw new IOException("Manufacturing workbook was not written correctly: " + fullPath);

            return fullPath;
        }

        private static Columns BuildColumns(int count)
        {
            var columns = new Columns();
            for (int i = 1; i <= Math.Max(1, count); i++)
            {
                double width;
                if (i == 1) width = 14;
                else if (i == 2) width = 22;
                else if (i == 3) width = 30;
                else width = 20;

                columns.Append(new Column
                {
                    Min = (uint)i,
                    Max = (uint)i,
                    Width = width,
                    CustomWidth = true
                });
            }
            return columns;
        }

        private static void WriteHeader(SheetData sheetData, IList<string> headers)
        {
            var row = new Row { RowIndex = 1U, Height = 22D, CustomHeight = true };
            foreach (string header in headers)
                row.Append(TextCell(header ?? string.Empty, 1U));
            sheetData.Append(row);
        }

        private static void WriteRow(
            SheetData sheetData,
            IList<object> values,
            int expectedColumns,
            uint rowIndex)
        {
            var row = new Row { RowIndex = rowIndex, Height = 62D, CustomHeight = true };
            for (int c = 0; c < expectedColumns; c++)
            {
                object value = c < values.Count ? values[c] : null;
                // Column A stores the image path internally but is visually represented by the embedded image.
                if (c == 0)
                {
                    row.Append(TextCell(string.Empty));
                    continue;
                }

                row.Append(ValueCell(value));
            }
            sheetData.Append(row);
        }

        private static Cell ValueCell(object value)
        {
            if (value == null) return TextCell(string.Empty);

            switch (Type.GetTypeCode(value.GetType()))
            {
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                case TypeCode.UInt64:
                case TypeCode.Single:
                case TypeCode.Double:
                case TypeCode.Decimal:
                    return new Cell
                    {
                        DataType = CellValues.Number,
                        CellValue = new CellValue(Convert.ToString(value, CultureInfo.InvariantCulture))
                    };
                default:
                    return TextCell(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
            }
        }

        private static Cell TextCell(string value, uint styleIndex = 0U)
        {
            return new Cell
            {
                DataType = CellValues.InlineString,
                StyleIndex = styleIndex,
                InlineString = new InlineString(new Text(value ?? string.Empty)
                {
                    Space = SpaceProcessingModeValues.Preserve
                })
            };
        }

        private static void AddStyles(WorkbookPart workbookPart)
        {
            WorkbookStylesPart stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
            stylesPart.Stylesheet = new Stylesheet(
                new Fonts(
                    new Font(),
                    new Font(new Bold())),
                new Fills(
                    new Fill(new PatternFill { PatternType = PatternValues.None }),
                    new Fill(new PatternFill { PatternType = PatternValues.Gray125 })),
                new Borders(new Border()),
                new CellStyleFormats(new CellFormat()),
                new CellFormats(
                    new CellFormat(),
                    new CellFormat { FontId = 1U, ApplyFont = true }));
            stylesPart.Stylesheet.Save();
        }

        private static void EmbedImages(
            WorksheetPart worksheetPart,
            IEnumerable<Tuple<uint, string>> imageRows)
        {
            DrawingsPart drawingsPart = worksheetPart.AddNewPart<DrawingsPart>();
            drawingsPart.WorksheetDrawing = new Xdr.WorksheetDrawing();

            uint imageId = 1U;
            foreach (Tuple<uint, string> entry in imageRows)
            {
                string imagePath = entry.Item2;
                ImagePart imagePart = drawingsPart.AddImagePart(ResolveImageContentType(imagePath));
                using (FileStream stream = File.Open(imagePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    imagePart.FeedData(stream);

                string relationshipId = drawingsPart.GetIdOfPart(imagePart);
                drawingsPart.WorksheetDrawing.Append(CreateAnchor(
                    relationshipId,
                    imageId++,
                    entry.Item1 - 1U,
                    Path.GetFileName(imagePath)));
            }

            drawingsPart.WorksheetDrawing.Save();
            worksheetPart.Worksheet.Append(new DocumentFormat.OpenXml.Spreadsheet.Drawing
            {
                Id = worksheetPart.GetIdOfPart(drawingsPart)
            });
        }

        private static Xdr.OneCellAnchor CreateAnchor(
            string relationshipId,
            uint imageId,
            uint rowIndex,
            string name)
        {
            long cx = ThumbnailWidthPx * EmusPerPixel;
            long cy = ThumbnailHeightPx * EmusPerPixel;

            var from = new Xdr.FromMarker(
                new Xdr.ColumnId("0"),
                new Xdr.ColumnOffset((2L * EmusPerPixel).ToString(CultureInfo.InvariantCulture)),
                new Xdr.RowId(rowIndex.ToString(CultureInfo.InvariantCulture)),
                new Xdr.RowOffset((2L * EmusPerPixel).ToString(CultureInfo.InvariantCulture)));

            var picture = new Xdr.Picture(
                new Xdr.NonVisualPictureProperties(
                    new Xdr.NonVisualDrawingProperties
                    {
                        Id = imageId,
                        Name = string.IsNullOrWhiteSpace(name) ? "Stock image " + imageId : name
                    },
                    new Xdr.NonVisualPictureDrawingProperties(
                        new A.PictureLocks { NoChangeAspect = true })),
                new Xdr.BlipFill(
                    new A.Blip { Embed = relationshipId },
                    new A.Stretch(new A.FillRectangle())),
                new Xdr.ShapeProperties(
                    new A.Transform2D(
                        new A.Offset { X = 0L, Y = 0L },
                        new A.Extents { Cx = cx, Cy = cy }),
                    new A.PresetGeometry(new A.AdjustValueList())
                    {
                        Preset = A.ShapeTypeValues.Rectangle
                    }));

            return new Xdr.OneCellAnchor(
                from,
                new Xdr.Extent { Cx = cx, Cy = cy },
                picture,
                new Xdr.ClientData());
        }

        private static string ResolveImageContentType(string path)
        {
            string extension = Path.GetExtension(path) ?? string.Empty;
            switch (extension.ToLowerInvariant())
            {
                case ".jpg":
                case ".jpeg": return "image/jpeg";
                case ".gif": return "image/gif";
                case ".bmp": return "image/bmp";
                case ".tif":
                case ".tiff": return "image/tiff";
                case ".png":
                default: return "image/png";
            }
        }

        private static string ColumnName(int columnNumber)
        {
            if (columnNumber <= 0) return "A";
            string name = string.Empty;
            int value = columnNumber;
            while (value > 0)
            {
                value--;
                name = (char)('A' + (value % 26)) + name;
                value /= 26;
            }
            return name;
        }
    }
}
