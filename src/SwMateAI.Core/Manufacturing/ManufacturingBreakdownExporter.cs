using System;
using System.IO;
using SolidWorks.Interop.sldworks;

namespace SwMateAI.Core.Manufacturing
{
    public class ManufacturingBreakdownExporter
    {
        private readonly ISldWorks _swApp;
        public ManufacturingBreakdownExporter(ISldWorks swApp) { _swApp = swApp; }

        public BreakdownExportResult Export(string outputPath, string imageFolder, StockCalculationOptions options = null)
        {
            outputPath = EnsureUniquePath(outputPath);
            if (string.IsNullOrWhiteSpace(imageFolder))
                imageFolder = Path.Combine(Path.GetDirectoryName(outputPath) ?? string.Empty, "PartImages");
            Directory.CreateDirectory(imageFolder);

            var breakdown = new ManufacturingBreakdownBuilder(_swApp, options).Build();
            var capture = new PartImageCapture(_swApp);
            int imageCount = 0;
            foreach (var item in breakdown.Items)
                if (!string.IsNullOrWhiteSpace(capture.Capture(item, imageFolder))) imageCount++;

            var table = new BreakdownTableGenerator().Generate(breakdown);
            string excelPath = new ExcelExporter().Export(table, outputPath);
            return new BreakdownExportResult
            {
                Breakdown = breakdown,
                ExcelPath = excelPath,
                ImageFolder = imageFolder,
                CapturedImageCount = imageCount
            };
        }

        private static string EnsureUniquePath(string requested)
        {
            if (string.IsNullOrWhiteSpace(requested))
                throw new ArgumentException("Excel output path is required.");
            string full = Path.GetFullPath(requested);
            string ext = Path.GetExtension(full);
            if (!ext.Equals(".xlsx", StringComparison.OrdinalIgnoreCase)) full += ".xlsx";
            if (!File.Exists(full)) return full;

            string dir = Path.GetDirectoryName(full) ?? string.Empty;
            string name = Path.GetFileNameWithoutExtension(full);
            for (int i = 1; i < 1000; i++)
            {
                string candidate = Path.Combine(dir, name + "_" + i + ".xlsx");
                if (!File.Exists(candidate)) return candidate;
            }
            throw new IOException("Could not allocate a unique Excel output file name.");
        }
    }
}
