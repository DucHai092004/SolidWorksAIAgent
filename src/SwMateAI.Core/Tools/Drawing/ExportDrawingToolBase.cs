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
            { reason = "Open a Drawing before exporting."; return false; }
            return true;
        }

        protected string ResolveOutput(Dictionary<string, object> parameters, string extension)
        {
            if (parameters != null && parameters.TryGetValue("OutputPath", out var raw))
            {
                string requested = Convert.ToString(raw)?.Trim() ?? string.Empty;
                if (requested.Length > 0) return Path.ChangeExtension(requested, extension);
            }
            var model = SwApp.ActiveDoc as IModelDoc2;
            string source = model?.GetPathName() ?? string.Empty;
            string root = source.Length > 0 ? Path.GetDirectoryName(source) : System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
            string name = source.Length > 0 ? Path.GetFileNameWithoutExtension(source) : Path.GetFileNameWithoutExtension(model?.GetTitle() ?? "Drawing");
            string folder = Path.Combine(root ?? string.Empty, "SW-MATE_AI_Output");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, name + extension);
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
            return ToolResult.Success(new DrawingExportResult { Format = format, OutputPath = path, Errors = errors, Warnings = warnings });
        }
    }
}
