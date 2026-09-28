using System;
using System.IO;
using System.Runtime.InteropServices;

namespace SwMateAI.Core.Manufacturing
{
    public class ExcelExporter
    {
        public string Export(BreakdownTable table, string outputPath)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (string.IsNullOrWhiteSpace(outputPath)) throw new ArgumentException("Output path is required.", nameof(outputPath));
            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

            Type excelType = Type.GetTypeFromProgID("Excel.Application");
            if (excelType == null) throw new InvalidOperationException("Microsoft Excel is not installed or COM automation is unavailable.");

            object excelObject = null;
            object workbookObject = null;
            object worksheetObject = null;
            try
            {
                excelObject = Activator.CreateInstance(excelType);
                dynamic excel = excelObject;
                excel.Visible = false;
                excel.DisplayAlerts = false;
                dynamic workbook = excel.Workbooks.Add();
                workbookObject = workbook;
                dynamic sheet = workbook.Worksheets[1];
                worksheetObject = sheet;
                sheet.Name = "Breakdown";

                for (int c = 0; c < table.Headers.Count; c++)
                {
                    dynamic cell = sheet.Cells[1, c + 1];
                    cell.Value2 = table.Headers[c];
                    cell.Font.Bold = true;
                    cell.Interior.ColorIndex = 15;
                }

                for (int r = 0; r < table.Rows.Count; r++)
                {
                    int excelRow = r + 2;
                    var row = table.Rows[r];
                    sheet.Rows[excelRow].RowHeight = 62;
                    for (int c = 1; c < row.Count; c++)
                        sheet.Cells[excelRow, c + 1].Value2 = row[c];

                    string imagePath = Convert.ToString(row.Count > 0 ? row[0] : null) ?? string.Empty;
                    if (File.Exists(imagePath))
                    {
                        dynamic imageCell = sheet.Cells[excelRow, 1];
                        double left = Convert.ToDouble(imageCell.Left) + 2;
                        double top = Convert.ToDouble(imageCell.Top) + 2;
                        sheet.Shapes.AddPicture(imagePath, false, true, left, top, 76, 56);
                    }
                }

                sheet.Columns.AutoFit();
                sheet.Columns[1].ColumnWidth = 14;
                sheet.Range["A1:K1"].AutoFilter();
                workbook.SaveAs(outputPath, 51);
                workbook.Close(false);
                excel.Quit();
                return outputPath;
            }
            finally
            {
                ReleaseCom(worksheetObject);
                ReleaseCom(workbookObject);
                ReleaseCom(excelObject);
            }
        }

        private static void ReleaseCom(object value)
        {
            if (value == null || !Marshal.IsComObject(value)) return;
            try { Marshal.FinalReleaseComObject(value); } catch { }
        }
    }
}
