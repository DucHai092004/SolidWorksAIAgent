using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SwMateAI.Core.Tools.Drawing
{
    /// <summary>
    /// Creates production drawing packages from the active Part/Assembly.
    /// Single mode exports the active Part/Assembly.
    /// Batch mode exports every unique Part referenced by the active Assembly.
    /// </summary>
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
            if (string.IsNullOrWhiteSpace(template))
            {
                reason = "No default Drawing template is configured in SOLIDWORKS.";
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
            string projection = Text(parameters, "Projection");
            if (string.IsNullOrWhiteSpace(projection)) projection = "Third";

            string outputFolder = Text(parameters, "OutputFolder");
            if (string.IsNullOrWhiteSpace(outputFolder))
                outputFolder = DefaultOutputFolder(activeModel);

            Directory.CreateDirectory(outputFolder);
            string drawingFolder = Path.Combine(outputFolder, "SLDDRW");
            string pdfFolder = Path.Combine(outputFolder, "PDF");
            if (saveDrawing) Directory.CreateDirectory(drawingFolder);
            if (exportPdf) Directory.CreateDirectory(pdfFolder);

            var sourcePaths = ResolveSourcePaths(activeModel, batch);
            if (sourcePaths.Count == 0)
            {
                return ToolResult.Error(batch
                    ? "No saved Part files were found in the active Assembly."
                    : "The active model must be saved before a drawing can be generated.");
            }

            string returnTitle = activeModel.GetTitle() ?? string.Empty;
            var succeeded = new List<string>();
            var failed = new List<string>();

            foreach (string sourcePath in sourcePaths)
            {
                try
                {
                    ExportOne(
                        sourcePath,
                        projection,
                        drawingFolder,
                        pdfFolder,
                        saveDrawing,
                        exportPdf);
                    succeeded.Add(sourcePath);
                }
                catch (Exception ex)
                {
                    failed.Add(Path.GetFileName(sourcePath) + ": " + ex.Message);
                }
                finally
                {
                    Reactivate(returnTitle);
                }
            }

            var summary = new Dictionary<string, object>
            {
                ["Mode"] = batch ? "Batch" : "Single",
                ["OutputFolder"] = outputFolder,
                ["Succeeded"] = succeeded.Count,
                ["Failed"] = failed.Count,
                ["SucceededFiles"] = succeeded.ToArray(),
                ["Errors"] = failed.ToArray()
            };

            if (succeeded.Count == 0)
                return ToolResult.Error("Drawing export failed for every source. " + string.Join(" | ", failed));

            return ToolResult.Success(summary);
        }

        private void ExportOne(
            string sourcePath,
            string projection,
            string drawingFolder,
            string pdfFolder,
            bool saveDrawing,
            bool exportPdf)
        {
            if (!File.Exists(sourcePath))
                throw new FileNotFoundException("Source model was not found.", sourcePath);

            string template = SwApp.GetUserPreferenceStringValue(
                (int)swUserPreferenceStringValue_e.swDefaultTemplateDrawing);
            if (string.IsNullOrWhiteSpace(template))
                throw new InvalidOperationException("No default Drawing template is configured in SOLIDWORKS.");

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
                string baseName = Path.GetFileNameWithoutExtension(sourcePath);

                if (saveDrawing)
                {
                    string drawingPath = UniquePath(Path.Combine(drawingFolder, baseName + ".SLDDRW"));
                    SaveDrawing(drawingModel, drawingPath);
                }

                if (exportPdf)
                {
                    string pdfPath = UniquePath(Path.Combine(pdfFolder, baseName + ".pdf"));
                    SavePdf(drawingModel, pdfPath);
                }
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
            int errors = 0;
            int warnings = 0;
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
            if (data == null)
                throw new InvalidOperationException("SOLIDWORKS did not provide PDF export data.");

            data.ViewPdfAfterSaving = false;
            data.SetSheets((int)swExportDataSheetsToExport_e.swExportData_ExportAllSheets, null);

            int errors = 0;
            int warnings = 0;
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

        private static List<string> ResolveSourcePaths(IModelDoc2 activeModel, bool batch)
        {
            string activePath = activeModel.GetPathName() ?? string.Empty;
            if (!batch)
            {
                return string.IsNullOrWhiteSpace(activePath)
                    ? new List<string>()
                    : new List<string> { activePath };
            }

            if (activeModel.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
                return new List<string>();

            var assembly = activeModel as IAssemblyDoc;
            object raw = assembly?.GetComponents(false);
            var components = raw as object[];
            if (components == null) return new List<string>();

            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (object item in components)
            {
                var component = item as IComponent2;
                if (component == null) continue;

                string path = component.GetPathName() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(path)) continue;
                if (!path.EndsWith(".sldprt", StringComparison.OrdinalIgnoreCase)) continue;
                if (!File.Exists(path)) continue;
                paths.Add(path);
            }

            return paths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
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

        private static bool IsFirstAngle(string value)
        {
            string v = (value ?? string.Empty).Trim().ToLowerInvariant();
            return v == "first" || v == "first angle" || v == "góc thứ nhất" ||
                   v == "goc thu nhat" || v == "1";
        }

        private static bool Bool(Dictionary<string, object> input, string key, bool defaultValue)
        {
            if (input == null || !input.TryGetValue(key, out var raw) || raw == null)
                return defaultValue;
            if (raw is bool value) return value;
            return bool.TryParse(Convert.ToString(raw), out var parsed) ? parsed : defaultValue;
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
                : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
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
    }
}
