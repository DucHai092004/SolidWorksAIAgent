using System;
using System.IO;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SwMateAI.Core.BOM
{
    /// <summary>
    /// Captures an isometric preview for one representative BOM component.
    /// Source documents are resolved by path first so nested components can be captured reliably.
    /// Lightweight/unloaded files are opened invisibly and closed afterwards.
    /// Loaded documents that were hidden are restored to their hidden state.
    /// </summary>
    public class BomImageCapture
    {
        private readonly ISldWorks _swApp;

        public BomImageCapture(ISldWorks swApp)
        {
            _swApp = swApp;
        }

        public string Capture(BomItem item, string folder)
        {
            if (item == null) return string.Empty;

            var assemblyModel = _swApp.ActiveDoc as IModelDoc2;
            var assembly = assemblyModel as IAssemblyDoc;
            if (assemblyModel == null || assembly == null) return string.Empty;

            IModelDoc2 componentModel = null;
            if (!string.IsNullOrWhiteSpace(item.SourcePath))
            {
                try { componentModel = _swApp.GetOpenDocumentByName(item.SourcePath) as IModelDoc2; }
                catch { }
            }

            if (componentModel == null && !string.IsNullOrWhiteSpace(item.RepresentativeComponentName))
            {
                try
                {
                    var component = assembly.GetComponentByName(item.RepresentativeComponentName);
                    componentModel = component?.GetModelDoc2() as IModelDoc2;
                }
                catch { }
            }

            bool openedTemporarily = false;
            bool wasVisible = componentModel != null && componentModel.Visible;
            string assemblyTitle = assemblyModel.GetTitle() ?? string.Empty;
            int activateErrors = 0;

            try
            {
                if (componentModel == null)
                {
                    if (string.IsNullOrWhiteSpace(item.SourcePath) || !File.Exists(item.SourcePath))
                        return string.Empty;

                    int documentType = ResolveDocumentType(item.SourcePath);
                    if (documentType == (int)swDocumentTypes_e.swDocNONE)
                        return string.Empty;

                    int openErrors = 0;
                    int openWarnings = 0;
                    try
                    {
                        _swApp.DocumentVisible(false, documentType);
                        componentModel = _swApp.OpenDoc6(
                            item.SourcePath,
                            documentType,
                            (int)swOpenDocOptions_e.swOpenDocOptions_Silent,
                            item.Configuration ?? string.Empty,
                            ref openErrors,
                            ref openWarnings) as IModelDoc2;
                        openedTemporarily = componentModel != null;
                    }
                    finally
                    {
                        try { _swApp.DocumentVisible(true, documentType); } catch { }
                    }
                }

                if (componentModel == null) return string.Empty;

                try { componentModel.Visible = true; } catch { }
                Directory.CreateDirectory(folder);
                string shortName = SafeFileName(string.IsNullOrWhiteSpace(item.PartNumber)
                    ? "item"
                    : item.PartNumber);
                if (shortName.Length > 60) shortName = shortName.Substring(0, 60);
                string output = Path.Combine(
                    folder,
                    "bom_" + item.ItemNumber.ToString("D4") + "_" + shortName + ".png");

                _swApp.ActivateDoc3(componentModel.GetTitle(), false, 0, ref activateErrors);
                componentModel.ShowNamedView2("*Isometric", (int)swStandardViews_e.swIsometricView);
                componentModel.ViewZoomtofit2();

                int errors = 0;
                int warnings = 0;
                bool ok = componentModel.Extension.SaveAs(
                    output,
                    (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                    (int)swSaveAsOptions_e.swSaveAsOptions_Silent,
                    null,
                    ref errors,
                    ref warnings);

                if (!ok || errors != 0 || !File.Exists(output)) return string.Empty;
                item.ImagePath = output;
                return output;
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(assemblyTitle))
                    _swApp.ActivateDoc3(assemblyTitle, false, 0, ref activateErrors);

                if (openedTemporarily && componentModel != null)
                {
                    try { _swApp.CloseDoc(componentModel.GetTitle()); }
                    catch { }
                }
                else if (componentModel != null && !wasVisible)
                {
                    try { componentModel.Visible = false; } catch { }
                }
            }
        }

        private static int ResolveDocumentType(string path)
        {
            if (path.EndsWith(".SLDPRT", StringComparison.OrdinalIgnoreCase))
                return (int)swDocumentTypes_e.swDocPART;
            if (path.EndsWith(".SLDASM", StringComparison.OrdinalIgnoreCase))
                return (int)swDocumentTypes_e.swDocASSEMBLY;
            return (int)swDocumentTypes_e.swDocNONE;
        }

        private static string SafeFileName(string value)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) value = value.Replace(c, '_');
            return string.IsNullOrWhiteSpace(value) ? "bom_item" : value;
        }
    }
}
