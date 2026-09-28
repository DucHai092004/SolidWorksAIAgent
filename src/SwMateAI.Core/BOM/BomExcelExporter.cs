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
                string[] headers = { "Item", "Part Number", "Description", "Quantity", "Material", "Type", "Configuration", "Source Path" };

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
                    sheet.Cells[row, 1].Value2 = x.ItemNumber;
                    sheet.Cells[row, 2].Value2 = x.PartNumber;
                    sheet.Cells[row, 3].Value2 = x.Description;
                    sheet.Cells[row, 4].Value2 = x.Quantity;
                    sheet.Cells[row, 5].Value2 = x.Material;
                    sheet.Cells[row, 6].Value2 = x.ComponentType;
                    sheet.Cells[row, 7].Value2 = x.Configuration;
                    sheet.Cells[row, 8].Value2 = x.SourcePath;
                }

                sheet.Columns.AutoFit();
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
