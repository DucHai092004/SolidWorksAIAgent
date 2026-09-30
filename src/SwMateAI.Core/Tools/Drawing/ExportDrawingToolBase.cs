using System;
using System.Collections.Generic;
using System.IO;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SwMateAI.Core.Drawing;

namespace SwMateAI.Core.Tools.Drawing
{
    public abstract class ExportDrawingToolBase : SwToolBase
    {
        protected ExportDrawingToolBase(ISldWorks swApp) : base(swApp) { }

        public override bool CanExecute(out string reason)
        {
            reason = null;
            var model = SwApp.ActiveDoc as IModelDoc2;
            if (model == null || model.GetType() != (int)swDocumentTypes_e.swDocDRAWING)
            {
                reason = "Open a Drawing before exporting.";
                return false;
            }
            return true;
        }

        protected string ResolveOutput(Dictionary<string, object> parameters, string extension)
        {
            string requested = string.Empty;
            if (parameters != null && parameters.TryGetValue("OutputPath", out var raw))
                requested = Convert.ToString(raw)?.Trim().Trim('"') ?? string.Empty;

            if (requested.Length == 0)
            {
                var model = SwApp.ActiveDoc as IModelDoc2;
                string source = model?.GetPathName() ?? string.Empty;
                string root = source.Length > 0
                    ? Path.GetDirectoryName(source)
                    : System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
                string name = source.Length > 0
                    ? Path.GetFileNameWithoutExtension(source)
                    : Path.GetFileNameWithoutExtension(model?.GetTitle() ?? "Drawing");
                foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
                string folder = Path.Combine(root ?? string.Empty, "SW-MATE_AI_Output");
                Directory.CreateDirectory(folder);
                requested = Path.Combine(folder, name + extension);
            }
            else
            {
                requested = Path.ChangeExtension(requested, extension);
            }

            return UniquePath(requested);
        }

        protected ToolResult Save(string path, object exportData, string format)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            var model = SwApp.ActiveDoc as IModelDoc2;
            int errors = 0, warnings = 0;
            bool ok = model.Extension.SaveAs(
                path,
                (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                (int)swSaveAsOptions_e.swSaveAsOptions_Silent,
                exportData,
                ref errors,
                ref warnings);
            if (!ok || errors != 0 || !File.Exists(path))
                return ToolResult.Error($"{format} export failed. Errors={errors}, Warnings={warnings}.");
            return ToolResult.Success(new DrawingExportResult
            {
                Format = format,
                OutputPath = path,
                Errors = errors,
                Warnings = warnings
            });
        }

        private static string UniquePath(string path)
        {
            if (!File.Exists(path)) return path;
            string directory = Path.GetDirectoryName(path) ?? string.Empty;
            string name = Path.GetFileNameWithoutExtension(path);
            string extension = Path.GetExtension(path);
            for (int i = 1; i < 10000; i++)
            {
                string candidate = Path.Combine(directory, name + "_" + i + extension);
                if (!File.Exists(candidate)) return candidate;
            }
            throw new IOException("Could not allocate a unique export file name for " + path);
        }
    }
}
