using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SwMateAI.Core.BOM;
using SwMateAI.Core.Tools;
using SwMateAI.Core.Tools.BOM;

namespace SwMateAI.BomWorker
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            try
            {
                string outputFolder = ParseOutputFolder(args);
                var sw = (ISldWorks)Marshal.GetActiveObject("SldWorks.Application");
                var model = sw.ActiveDoc as IModelDoc2;
                if (model == null)
                    return Fail("No active SOLIDWORKS document.");

                var parameters = new Dictionary<string, object>
                {
                    ["Mode"] = "LegacyFlat",
                    ["RespectChildDisplay"] = true,
                    ["ExportExcel"] = true,
                    ["ExportCsv"] = false,
                    ["CaptureImages"] = true
                };
                if (!string.IsNullOrWhiteSpace(outputFolder))
                    parameters["OutputFolder"] = outputFolder;

                var clock = Stopwatch.StartNew();
                ToolResult result = new CreateBomTool(sw).Execute(parameters);
                clock.Stop();

                if (!result.IsSuccess)
                    return Fail(result.ErrorMessage ?? "BOM export failed.");

                var bom = result.Data as BomResult;
                if (bom == null)
                    return Fail("BOM export returned no result data.");

                Console.WriteLine(
                    "RESULT|OK|" + Escape(bom.ExcelPath) +
                    "|ITEMS=" + bom.Items.Count +
                    "|IMAGES=" + bom.CapturedImageCount +
                    "|MS=" + clock.ElapsedMilliseconds);
                return 0;
            }
            catch (Exception ex)
            {
                return Fail(ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static string ParseOutputFolder(string[] args)
        {
            if (args == null) return string.Empty;
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], "--output", StringComparison.OrdinalIgnoreCase))
                    return args[i + 1]?.Trim() ?? string.Empty;
            }
            return string.Empty;
        }

        private static int Fail(string message)
        {
            Console.WriteLine("RESULT|ERROR|" + Escape(message));
            return 1;
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty)
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Replace("|", "/");
        }
    }
}
