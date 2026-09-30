using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SwMateAI.Core.Tools.Drawing
{
    public class ExportDrawingPackageTool : SwToolBase
    {
        public ExportDrawingPackageTool(ISldWorks swApp) : base(swApp) { }

        public override string Name => "ExportDrawingPackage";
        public override string Description =>
            "Creates SOLIDWORKS drawings with standard views and optionally exports PDF for one active model or every unique Part in an Assembly.";

        public override bool CanExecute(out string reason)
        {
            reason = null;
            var model = SwApp.ActiveDoc as IModelDoc2;
            if (model == null)
            {
                reason = "Open a Part or Assembly before exporting drawings.";
                return false;
            }

            int type = model.GetType();
            if (type != (int)swDocumentTypes_e.swDocPART &&
                type != (int)swDocumentTypes_e.swDocASSEMBLY)
            {
                reason = "The active document must be a Part or Assembly.";
                return false;
            }

            string template = SwApp.GetUserPreferenceStringValue(
                (int)swUserPreferenceStringValue_e.swDefaultTemplateDrawing);
            if (string.IsNullOrWhiteSpace(template) || !File.Exists(template))
            {
                reason = "No valid default Drawing template is configured in SOLIDWORKS.";
                return false;
            }

            return true;
        }

        public override ToolResult Execute(Dictionary<string, object> parameters)
        {
            var activeModel = SwApp.ActiveDoc as IModelDoc2;
            if (activeModel == null) return ToolResult.Error("No active source model.");

            bool batch = Bool(parameters, "Batch", false);
            bool saveDrawing = Bool(parameters, "SaveDrawing", true);
            bool exportPdf = Bool(parameters, "ExportPdf", true);
            bool previewOnly = Bool(parameters, "PreviewOnly", false);
            int pauseMilliseconds = Int(parameters, "PauseMilliseconds", batch ? 300 : 150, 0, 3000);
            string projection = Text(parameters, "Projection");
            if (string.IsNullOrWhiteSpace(projection)) projection = "Third";

            string outputFolder = Text(parameters, "OutputFolder");
            if (string.IsNullOrWhiteSpace(outputFolder))
                outputFolder = DefaultOutputFolder(activeModel);

            string template = SwApp.GetUserPreferenceStringValue(
                (int)swUserPreferenceStringValue_e.swDefaultTemplateDrawing);
            if (string.IsNullOrWhiteSpace(template) || !File.Exists(template))
                return ToolResult.Error("Default Drawing template is missing or invalid.");

            SourceResolution resolution = ResolveSourcePaths(activeModel, batch);
            var sourcePaths = resolution.Paths;
            if (sourcePaths.Count == 0)
            {
                return ToolResult.Error(batch
                    ? "No valid saved Part files were found in the active Assembly."
                    : "The active model must be saved before a drawing can be generated.");
            }

            if (previewOnly)
            {
                return ToolResult.Success(new Dictionary<string, object>
                {
                    ["Mode"] = batch ? "Batch" : "Single",
                    ["OutputFolder"] = outputFolder,
                    ["TemplatePath"] = template,
                    ["Projection"] = projection,
                    ["SaveDrawing"] = saveDrawing,
                    ["ExportPdf"] = exportPdf,
                    ["SourceFiles"] = sourcePaths.ToArray(),
                    ["SourceCount"] = sourcePaths.Count,
                    ["SuppressedSkipped"] = resolution.SuppressedSkipped,
                    ["MissingSkipped"] = resolution.MissingSkipped,
                    ["UnsupportedSkipped"] = resolution.UnsupportedSkipped,
                    ["PauseMilliseconds"] = pauseMilliseconds
                });
            }

            Directory.CreateDirectory(outputFolder);
            string drawingFolder = Path.Combine(outputFolder, "SLDDRW");
            string pdfFolder = Path.Combine(outputFolder, "PDF");
            string logFolder = Path.Combine(outputFolder, "Logs");
            if (saveDrawing) Directory.CreateDirectory(drawingFolder);
            if (exportPdf) Directory.CreateDirectory(pdfFolder);
            Directory.CreateDirectory(logFolder);

            string returnTitle = activeModel.GetTitle() ?? string.Empty;
            var succeeded = new List<string>();
            var failed = new List<string>();

            foreach (string sourcePath in sourcePaths)
            {
                try
                {
                    ExportOne(sourcePath, template, projection, drawingFolder, pdfFolder, saveDrawing, exportPdf);
                    succeeded.Add(sourcePath);
                }
                catch (Exception ex)
                {
                    failed.Add(Path.GetFileName(sourcePath) + ": " + ex.Message);
                }
                finally
                {
                    Reactivate(returnTitle);
                    if (pauseMilliseconds > 0) Thread.Sleep(pauseMilliseconds);
                }
            }

            string logPath = WriteLog(logFolder, succeeded, failed);
            var summary = new Dictionary<string, object>
            {
                ["Mode"] = batch ? "Batch" : "Single",
                ["OutputFolder"] = outputFolder,
                ["Succeeded"] = succeeded.Count,
                ["Failed"] = failed.Count,
                ["SucceededFiles"] = succeeded.ToArray(),
                ["Errors"] = failed.ToArray(),
                ["LogPath"] = logPath,
                ["SuppressedSkipped"] = resolution.SuppressedSkipped,
                ["MissingSkipped"] = resolution.MissingSkipped,
                ["UnsupportedSkipped"] = resolution.UnsupportedSkipped
            };

            if (succeeded.Count == 0)
                return ToolResult.Error("Drawing export failed for every source. Log=" + logPath);

            return ToolResult.Success(summary);
        }

        private void ExportOne(
            string sourcePath,
            string template,
            string projection,
            string drawingFolder,
            string pdfFolder,
            bool saveDrawing,
            bool exportPdf)
        {
            if (!File.Exists(sourcePath))
                throw new FileNotFoundException("Source model was not found.", sourcePath);
            if (string.IsNullOrWhiteSpace(template) || !File.Exists(template))
                throw new FileNotFoundException("Drawing template was not found.", template);

            var drawingModel = SwApp.NewDocument(template, 0, 0, 0) as IModelDoc2;
            var drawing = drawingModel as IDrawingDoc;
            if (drawingModel == null || drawing == null)
                throw new InvalidOperationException("SOLIDWORKS did not create a valid Drawing document.");

            string drawingTitle = drawingModel.GetTitle() ?? string.Empty;
            try
            {
                bool created = IsFirstAngle(projection)
                    ? drawing.Create1stAngleViews2(sourcePath)
                    : drawing.Create3rdAngleViews2(sourcePath);
                if (!created)
                    throw new InvalidOperationException("SOLIDWORKS could not create standard drawing views.");

                drawingModel.ForceRebuild3(false);
                drawingModel.GraphicsRedraw2();
                Thread.Sleep(150);
                string baseName = SafeBaseName(sourcePath);

                if (saveDrawing)
                    SaveDrawing(drawingModel, UniquePath(Path.Combine(drawingFolder, baseName + ".SLDDRW")));
                if (exportPdf)
                    SavePdf(drawingModel, UniquePath(Path.Combine(pdfFolder, baseName + ".pdf")));
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(drawingTitle))
                {
                    try { SwApp.CloseDoc(drawingTitle); } catch { }
                }
            }
        }

        private void SaveDrawing(IModelDoc2 drawingModel, string path)
        {
            int errors = 0, warnings = 0;
            bool ok = drawingModel.Extension.SaveAs(
                path,
                (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                (int)swSaveAsOptions_e.swSaveAsOptions_Silent,
                null,
                ref errors,
                ref warnings);
            if (!ok || errors != 0 || !File.Exists(path))
                throw new IOException("SLDDRW save failed. Errors=" + errors + ", Warnings=" + warnings + ".");
        }

        private void SavePdf(IModelDoc2 drawingModel, string path)
        {
            var data = SwApp.GetExportFileData((int)swExportDataFileType_e.swExportPdfData) as IExportPdfData;
            if (data == null) throw new InvalidOperationException("SOLIDWORKS did not provide PDF export data.");
            try
            {
                data.ViewPdfAfterSaving = false;
                data.SetSheets((int)swExportDataSheetsToExport_e.swExportData_ExportAllSheets, null);

                int errors = 0, warnings = 0;
                bool ok = drawingModel.Extension.SaveAs(
                    path,
                    (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                    (int)swSaveAsOptions_e.swSaveAsOptions_Silent,
                    data,
                    ref errors,
                    ref warnings);
                if (!ok || errors != 0 || !File.Exists(path))
                    throw new IOException("PDF export failed. Errors=" + errors + ", Warnings=" + warnings + ".");
            }
            finally
            {
                if (data != null && Marshal.IsComObject(data))
                {
                    try { Marshal.FinalReleaseComObject(data); } catch { }
                }
            }
        }

        private static SourceResolution ResolveSourcePaths(IModelDoc2 activeModel, bool batch)
        {
            var result = new SourceResolution();
            string activePath = activeModel.GetPathName() ?? string.Empty;
            if (!batch)
            {
                if (!string.IsNullOrWhiteSpace(activePath) && File.Exists(activePath))
                    result.Paths.Add(activePath);
                else
                    result.MissingSkipped++;
                return result;
            }

            if (activeModel.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
                return result;

            var assembly = activeModel as IAssemblyDoc;
            var components = assembly?.GetComponents(false) as object[];
            if (components == null) return result;

            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (object item in components)
            {
                var component = item as IComponent2;
                if (component == null) continue;

                try
                {
                    if (component.IsSuppressed())
                    {
                        result.SuppressedSkipped++;
                        continue;
                    }
                }
                catch { }

                string path = component.GetPathName() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(path))
                {
                    result.MissingSkipped++;
                    continue;
                }
                if (!path.EndsWith(".sldprt", StringComparison.OrdinalIgnoreCase))
                {
                    result.UnsupportedSkipped++;
                    continue;
                }
                if (!File.Exists(path))
                {
                    result.MissingSkipped++;
                    continue;
                }
                paths.Add(path);
            }

            result.Paths.AddRange(paths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase));
            return result;
        }

        private void Reactivate(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return;
            try
            {
                int errors = 0;
                SwApp.ActivateDoc3(title, false, 0, ref errors);
            }
            catch { }
        }

        private static string WriteLog(string folder, IList<string> succeeded, IList<string> failed)
        {
            string path = Path.Combine(folder, "DrawingExport_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".log");
            using (var writer = new StreamWriter(path, false, System.Text.Encoding.UTF8))
            {
                writer.WriteLine("SW-MATE AI Drawing Export");
                writer.WriteLine("Time: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                writer.WriteLine("Succeeded: " + succeeded.Count);
                writer.WriteLine("Failed: " + failed.Count);
                foreach (string value in succeeded) writer.WriteLine("OK|" + value);
                foreach (string value in failed) writer.WriteLine("ERROR|" + value);
            }
            return path;
        }

        private static string SafeBaseName(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path ?? string.Empty);
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return string.IsNullOrWhiteSpace(name) ? "Drawing" : name;
        }

        private static bool IsFirstAngle(string value)
        {
            string v = (value ?? string.Empty).Trim().ToLowerInvariant();
            return v == "first" || v == "first angle" || v == "góc thứ nhất" || v == "goc thu nhat" || v == "1";
        }

        private static bool Bool(Dictionary<string, object> input, string key, bool defaultValue)
        {
            if (input == null || !input.TryGetValue(key, out var raw) || raw == null) return defaultValue;
            if (raw is bool value) return value;
            return bool.TryParse(Convert.ToString(raw), out var parsed) ? parsed : defaultValue;
        }

        private static int Int(Dictionary<string, object> input, string key, int defaultValue, int min, int max)
        {
            if (input == null || !input.TryGetValue(key, out var raw) || raw == null) return defaultValue;
            if (!int.TryParse(Convert.ToString(raw), out int value)) return defaultValue;
            return Math.Max(min, Math.Min(max, value));
        }

        private static string Text(Dictionary<string, object> input, string key)
        {
            return input != null && input.TryGetValue(key, out var raw)
                ? Convert.ToString(raw)?.Trim() ?? string.Empty
                : string.Empty;
        }

        private static string DefaultOutputFolder(IModelDoc2 model)
        {
            string path = model?.GetPathName() ?? string.Empty;
            string root = !string.IsNullOrWhiteSpace(path)
                ? Path.GetDirectoryName(path)
                : System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
            return Path.Combine(root ?? string.Empty, "SW-MATE_AI_Output", "DrawingPackage");
        }

        private static string UniquePath(string path)
        {
            if (!File.Exists(path)) return path;
            string directory = Path.GetDirectoryName(path) ?? string.Empty;
            string name = Path.GetFileNameWithoutExtension(path);
            string extension = Path.GetExtension(path);
            for (int index = 1; index < 10000; index++)
            {
                string candidate = Path.Combine(directory, name + "_" + index + extension);
                if (!File.Exists(candidate)) return candidate;
            }
            throw new IOException("Could not allocate a unique output file name for " + path);
        }

        private sealed class SourceResolution
        {
            public List<string> Paths { get; } = new List<string>();
            public int SuppressedSkipped { get; set; }
            public int MissingSkipped { get; set; }
            public int UnsupportedSkipped { get; set; }
        }
    }
}
