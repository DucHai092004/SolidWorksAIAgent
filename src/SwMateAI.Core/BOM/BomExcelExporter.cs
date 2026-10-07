using System;
using System.Collections.Generic;
using System.IO;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using A = DocumentFormat.OpenXml.Drawing;
using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;

namespace SwMateAI.Core.BOM
{
    /// <summary>
    /// Writes the BOM directly as an .xlsx package. Microsoft Excel is not required.
    /// Thumbnail files referenced by BomItem.ImagePath are embedded into column A.
    /// </summary>
    public class BomExcelExporter
    {
        private const long EmusPerPixel = 9525L;
        private const int ThumbnailWidthPx = 76;
        private const int ThumbnailHeightPx = 56;

        public string Export(BomResult result, string path)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Output path is required.", nameof(path));

            string fullPath = Path.GetFullPath(path);
            string dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);

            // Create() must not silently replace a file selected by a previous operation.
            // The caller is expected to allocate a unique output name.
            if (File.Exists(fullPath))
                throw new IOException("The BOM output file already exists: " + fullPath);

            try
            {
                using (var document = SpreadsheetDocument.Create(fullPath, SpreadsheetDocumentType.Workbook))
                {
                    WorkbookPart workbookPart = document.AddWorkbookPart();
                    workbookPart.Workbook = new Workbook();
                    AddStyles(workbookPart);

                    WorksheetPart worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
                    var sheetData = new SheetData();
                    worksheetPart.Worksheet = new Worksheet(
                        new Columns(
                            Column(1, 1, 14),
                            Column(2, 2, 9),
                            Column(3, 3, 28),
                            Column(4, 4, 36),
                            Column(5, 5, 10),
                            Column(6, 6, 22),
                            Column(7, 7, 16),
                            Column(8, 8, 20)),
                        sheetData);

                    var sheets = workbookPart.Workbook.AppendChild(new Sheets());
                    sheets.Append(new Sheet
                    {
                        Id = workbookPart.GetIdOfPart(worksheetPart),
                        SheetId = 1U,
                        Name = "BOM"
                    });

                    WriteHeader(sheetData);

                    var imageRows = new List<Tuple<uint, string>>();
                    for (int i = 0; i < result.Items.Count; i++)
                    {
                        BomItem item = result.Items[i];
                        uint rowIndex = (uint)(i + 2);
                        WriteItem(sheetData, item, rowIndex);
                        if (!string.IsNullOrWhiteSpace(item.ImagePath) && File.Exists(item.ImagePath))
                            imageRows.Add(Tuple.Create(rowIndex, item.ImagePath));
                    }

                    if (imageRows.Count > 0)
                        EmbedImages(worksheetPart, imageRows);

                    worksheetPart.Worksheet.Save();
                    workbookPart.Workbook.Save();
                }
            }
            catch
            {
                // Do not leave a corrupt/partial workbook after a failed export.
                try { if (File.Exists(fullPath)) File.Delete(fullPath); } catch { }
                throw;
            }

            var info = new FileInfo(fullPath);
            if (!info.Exists || info.Length <= 0)
                throw new IOException("BOM workbook was not written correctly: " + fullPath);

            return fullPath;
        }

        private static void WriteHeader(SheetData sheetData)
        {
            string[] headers =
            {
                "Image", "Item", "Part Number", "Description",
                "Quantity", "Material", "Type", "Configuration"
            };
            var row = new Row { RowIndex = 1U, Height = 22D, CustomHeight = true };
            for (int i = 0; i < headers.Length; i++)
                row.Append(TextCell(headers[i], 1U));
            sheetData.Append(row);
        }

        private static void WriteItem(SheetData sheetData, BomItem item, uint rowIndex)
        {
            var row = new Row { RowIndex = rowIndex, Height = 62D, CustomHeight = true };
            row.Append(TextCell(string.Empty));
            row.Append(NumberCell(item == null ? 0 : item.ItemNumber));

            string partNumber = item?.PartNumber ?? string.Empty;
            if (item != null && item.Level > 0)
                partNumber = new string(' ', Math.Min(15, item.Level) * 2) + partNumber;

            row.Append(TextCell(partNumber));
            row.Append(TextCell(item?.Description ?? string.Empty));
            row.Append(NumberCell(item == null ? 0 : item.Quantity));
            row.Append(TextCell(item?.Material ?? string.Empty));
            row.Append(TextCell(item?.ComponentType ?? string.Empty));
            row.Append(TextCell(item?.Configuration ?? string.Empty));
            sheetData.Append(row);
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

        private static Cell NumberCell(int value)
        {
            return new Cell
            {
                DataType = CellValues.Number,
                CellValue = new CellValue(value.ToString(System.Globalization.CultureInfo.InvariantCulture))
            };
        }

        private static Column Column(uint min, uint max, double width)
        {
            return new Column { Min = min, Max = max, Width = width, CustomWidth = true };
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
                string contentType = ResolveImageContentType(imagePath);
                ImagePart imagePart = drawingsPart.AddImagePart(contentType);
                using (FileStream stream = File.Open(imagePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    imagePart.FeedData(stream);

                string relationshipId = drawingsPart.GetIdOfPart(imagePart);
                uint zeroBasedRow = entry.Item1 - 1U;
                drawingsPart.WorksheetDrawing.Append(CreateAnchor(
                    relationshipId,
                    imageId++,
                    zeroBasedRow,
                    Path.GetFileName(imagePath)));
            }

            drawingsPart.WorksheetDrawing.Save();
            worksheetPart.Worksheet.Append(new Drawing
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
                new Xdr.ColumnOffset((2L * EmusPerPixel).ToString()),
                new Xdr.RowId(rowIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new Xdr.RowOffset((2L * EmusPerPixel).ToString()));

            var picture = new Xdr.Picture(
                new Xdr.NonVisualPictureProperties(
                    new Xdr.NonVisualDrawingProperties
                    {
                        Id = imageId,
                        Name = string.IsNullOrWhiteSpace(name) ? "BOM image " + imageId : name
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
    }
}
