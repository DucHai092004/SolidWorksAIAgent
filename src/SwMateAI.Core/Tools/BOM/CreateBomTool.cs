using System;
using System.Collections.Generic;
using System.IO;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SwMateAI.Core.BOM;

namespace SwMateAI.Core.Tools.BOM
{
    public class CreateBomTool : SwToolBase
    {
        public CreateBomTool(ISldWorks swApp) : base(swApp) { }
        public override string Name => "CreateBOM";
        public override string Description => "Builds an Assembly BOM and optionally exports Excel and CSV files.";

        public override bool CanExecute(out string reason)
        {
            reason = null;
            var model = SwApp.ActiveDoc as IModelDoc2;
            if (model == null) { reason = "No active SOLIDWORKS document."; return false; }
            if (model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
            { reason = "The active document must be an Assembly."; return false; }
            return true;
        }

        public override ToolResult Execute(Dictionary<string, object> parameters)
        {
            var result = new BomBuilder(SwApp).Build();
            bool excel = Bool(parameters, "ExportExcel");
            bool csv = Bool(parameters, "ExportCsv");
            string folder = Text(parameters, "OutputFolder");
            if ((excel || csv) && string.IsNullOrWhiteSpace(folder))
                folder = DefaultFolder(SwApp.ActiveDoc as IModelDoc2);

            string baseName = SafeBaseName(SwApp.ActiveDoc as IModelDoc2);
            string tempImageFolder = null;

            try
            {
                if (excel)
                {
                    tempImageFolder = Path.Combine(
                        Path.GetTempPath(),
                        "SW-MATE_AI",
                        "BOM_Images",
                        Guid.NewGuid().ToString("N"));

                    var capture = new BomImageCapture(SwApp);
                    foreach (var item in result.Items)
                    {
                        string image = capture.Capture(item, tempImageFolder);
                        if (!string.IsNullOrWhiteSpace(image)) result.CapturedImageCount++;
                    }

                    string path = UniquePath(Path.Combine(folder, baseName + "_BOM.xlsx"));
                    result.ExcelPath = new BomExcelExporter().Export(result, path);
                }

                if (csv)
                {
                    string path = UniquePath(Path.Combine(folder, baseName + "_BOM.csv"));
                    result.CsvPath = new BomCsvExporter().Export(result, path);
                }

                return ToolResult.Success(result);
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(tempImageFolder) && Directory.Exists(tempImageFolder))
                {
                    try { Directory.Delete(tempImageFolder, true); } catch { }
                }
            }
        }

        private static bool Bool(Dictionary<string, object> input, string key)
        {
            if (input == null || !input.TryGetValue(key, out var raw) || raw == null) return false;
            if (raw is bool value) return value;
            return bool.TryParse(Convert.ToString(raw), out var parsed) && parsed;
        }

        private static string Text(Dictionary<string, object> input, string key)
        {
            return input != null && input.TryGetValue(key, out var raw)
                ? Convert.ToString(raw)?.Trim() ?? string.Empty
                : string.Empty;
        }

        private static string DefaultFolder(IModelDoc2 model)
        {
            string path = model?.GetPathName() ?? string.Empty;
            string root = !string.IsNullOrWhiteSpace(path)
                ? Path.GetDirectoryName(path)
                : System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
            string folder = Path.Combine(root ?? string.Empty, "SW-MATE_AI_Output");
            Directory.CreateDirectory(folder);
            return folder;
        }

        private static string SafeBaseName(IModelDoc2 model)
        {
            string path = model?.GetPathName() ?? string.Empty;
            string rawName = !string.IsNullOrWhiteSpace(path)
                ? Path.GetFileName(path)
                : model?.GetTitle() ?? "Assembly";
            string name = CleanCadName(rawName);
            return string.IsNullOrWhiteSpace(name) ? "Assembly" : name;
        }

        private static string CleanCadName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            string name = Path.GetFileName(value.Trim());
            string[] extensions = { ".sldprt", ".sldasm", ".slddrw", ".step", ".stp", ".iges", ".igs", ".x_t", ".x_b", ".sat" };
            bool removed;
            do
            {
                removed = false;
                foreach (var ext in extensions)
                {
                    if (!name.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) continue;
                    name = name.Substring(0, name.Length - ext.Length);
                    removed = true;
                    break;
                }
            } while (removed && !string.IsNullOrWhiteSpace(name));
            return name;
        }

        private static string UniquePath(string path)
        {
            if (!File.Exists(path)) return path;
            string dir = Path.GetDirectoryName(path) ?? string.Empty;
            string name = Path.GetFileNameWithoutExtension(path);
            string ext = Path.GetExtension(path);
            for (int i = 1; i < 1000; i++)
            {
                string candidate = Path.Combine(dir, name + "_" + i + ext);
                if (!File.Exists(candidate)) return candidate;
            }
            throw new IOException("Could not allocate a unique BOM output file name.");
        }
    }
}
