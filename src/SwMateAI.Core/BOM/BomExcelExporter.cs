using System;
using System.IO;
using System.Runtime.InteropServices;

namespace SwMateAI.Core.BOM
{
    public class BomExcelExporter
    {
        public string Export(BomResult result, string path)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
            Type excelType = Type.GetTypeFromProgID("Excel.Application");
            if (excelType == null) throw new InvalidOperationException("Microsoft Excel COM automation is unavailable.");

            object excelObject = null, workbookObject = null, worksheetObject = null;
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
                sheet.Name = "BOM";
                string[] headers =
                {
                    "Image", "Item", "Part Number", "Description",
                    "Quantity", "Material", "Type", "Configuration"
                };

                for (int c = 0; c < headers.Length; c++)
                {
                    dynamic cell = sheet.Cells[1, c + 1];
                    cell.Value2 = headers[c];
                    cell.Font.Bold = true;
                    cell.Interior.ColorIndex = 15;
                }

                for (int r = 0; r < result.Items.Count; r++)
                {
                    var x = result.Items[r];
                    int row = r + 2;
                    sheet.Rows[row].RowHeight = 62;
                    sheet.Cells[row, 2].Value2 = x.ItemNumber;
                    sheet.Cells[row, 3].Value2 = x.PartNumber;
                    if (x.Level > 0)
                    {
                        int indent = Math.Min(15, Math.Max(0, x.Level));
                        sheet.Cells[row, 3].IndentLevel = indent;
                    }
                    sheet.Cells[row, 4].Value2 = x.Description;
                    sheet.Cells[row, 5].Value2 = x.Quantity;
                    sheet.Cells[row, 6].Value2 = x.Material;
                    sheet.Cells[row, 7].Value2 = x.ComponentType;
                    sheet.Cells[row, 8].Value2 = x.Configuration;

                    if (File.Exists(x.ImagePath))
                    {
                        dynamic imageCell = sheet.Cells[row, 1];
                        double left = Convert.ToDouble(imageCell.Left) + 2;
                        double top = Convert.ToDouble(imageCell.Top) + 2;
                        sheet.Shapes.AddPicture(x.ImagePath, false, true, left, top, 76, 56);
                    }
                }

                sheet.Columns.AutoFit();
                sheet.Columns[1].ColumnWidth = 14;
                sheet.Range["A1:H1"].AutoFilter();
                workbook.SaveAs(path, 51);
                workbook.Close(false);
                excel.Quit();
                return path;
            }
            finally
            {
                Release(worksheetObject); Release(workbookObject); Release(excelObject);
            }
        }

        private static void Release(object value)
        {
            if (value == null || !Marshal.IsComObject(value)) return;
            try { Marshal.FinalReleaseComObject(value); } catch { }
        }
    }
}
