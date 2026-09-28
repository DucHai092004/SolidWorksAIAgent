using System;
using System.IO;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SwMateAI.Core.BOM
{
    /// <summary>
    /// Captures an isometric preview for one representative BOM component.
    /// The image is intended to be embedded in Excel and may be deleted afterwards.
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
            if (item == null || !item.IsLoaded || string.IsNullOrWhiteSpace(item.RepresentativeComponentName))
                return string.Empty;

            var assemblyModel = _swApp.ActiveDoc as IModelDoc2;
            var assembly = assemblyModel as IAssemblyDoc;
            var component = assembly?.GetComponentByName(item.RepresentativeComponentName);
            var componentModel = component?.GetModelDoc2() as IModelDoc2;
            if (assemblyModel == null || componentModel == null) return string.Empty;

            Directory.CreateDirectory(folder);
            string imageId = (string.IsNullOrWhiteSpace(item.PartNumber)
                ? item.RepresentativeComponentName
                : item.PartNumber) + "_" + item.Configuration;
            string output = Path.Combine(folder, SafeFileName(imageId) + ".png");
            string assemblyTitle = assemblyModel.GetTitle() ?? string.Empty;
            int activateErrors = 0;

            try
            {
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
            }
        }

        private static string SafeFileName(string value)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) value = value.Replace(c, '_');
            return string.IsNullOrWhiteSpace(value) ? "bom_item" : value;
        }
    }
}
