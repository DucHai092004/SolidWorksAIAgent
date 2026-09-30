using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using SwMateAI.Core.Common;

namespace SwMateAI.Core.DocumentIntelligence
{
    public class XlsxMaterialSourceParser : IMaterialSourceParser
    {
        public bool CanRead(string path) =>
            Path.GetExtension(path).Equals(".xlsx", StringComparison.OrdinalIgnoreCase);

        public IReadOnlyList<MaterialSourceRecord> Parse(string path)
        {
            var result = new List<MaterialSourceRecord>();
            using (var doc = SpreadsheetDocument.Open(path, false))
            {
                var workbook = doc.WorkbookPart;
                if (workbook?.Workbook?.Sheets == null) return result;
                foreach (var sheet in workbook.Workbook.Sheets.OfType<Sheet>())
                    ParseSheet(workbook, sheet, path, result);
            }
            return result;
        }

        private static void ParseSheet(WorkbookPart workbook, Sheet sheet, string path, List<MaterialSourceRecord> result)
        {
            if (sheet?.Id == null) return;
            var part = workbook.GetPartById(sheet.Id.Value) as WorksheetPart;
            var rows = part?.Worksheet?.Descendants<Row>().ToList();
            if (rows == null || rows.Count == 0) return;

            int codeColumn = -1, materialColumn = -1, headerRow = -1;
            for (int r = 0; r < Math.Min(rows.Count, 20); r++)
            {
                var cells = ReadRow(workbook, rows[r]);
                if (!MaterialFieldDetector.TryFindColumns(cells, out codeColumn, out materialColumn)) continue;
                headerRow = r;
                break;
            }
            if (headerRow < 0) return;

            for (int r = headerRow + 1; r < rows.Count; r++)
            {
                var cells = ReadRow(workbook, rows[r]);
                string code = Get(cells, codeColumn);
                string material = Get(cells, materialColumn);
                if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(material)) continue;

                result.Add(new MaterialSourceRecord
                {
                    RawPartCode = code.Trim(),
                    NormalizedPartCode = PartCodeNormalizer.Normalize(code),
                    StockMaterial = material.Trim(),
                    SourceFile = path,
                    SheetName = sheet.Name?.Value ?? string.Empty,
                    RowNumber = (int)(rows[r].RowIndex?.Value ?? (uint)(r + 1)),
                    ExtractionMethod = "XLSX",
                    ExtractionConfidence = 1.0
                });
            }
        }

        private static List<string> ReadRow(WorkbookPart workbook, Row row)
        {
            var values = new List<string>();
            foreach (var cell in row.Elements<Cell>())
            {
                int index = ColumnIndex(cell.CellReference?.Value);
                while (values.Count <= index) values.Add(string.Empty);
                values[index] = CellText(workbook, cell);
            }
            return values;
        }

        private static string CellText(WorkbookPart workbook, Cell cell)
        {
            string value = cell.CellValue?.Text ?? cell.InnerText ?? string.Empty;
            if (cell.DataType?.Value == CellValues.SharedString &&
                int.TryParse(value, out int index))
            {
                var table = workbook.SharedStringTablePart?.SharedStringTable;
                var item = table?.Elements<SharedStringItem>().ElementAtOrDefault(index);
                return item?.InnerText ?? string.Empty;
            }
            return value;
        }

        private static int ColumnIndex(string reference)
        {
            if (string.IsNullOrWhiteSpace(reference)) return 0;
            int result = 0;
            foreach (char c in reference)
            {
                if (!char.IsLetter(c)) break;
                result = result * 26 + (char.ToUpperInvariant(c) - 'A' + 1);
            }
            return Math.Max(0, result - 1);
        }

        private static string Get(IReadOnlyList<string> values, int index) =>
            index >= 0 && index < values.Count ? values[index] ?? string.Empty : string.Empty;
    }
}
