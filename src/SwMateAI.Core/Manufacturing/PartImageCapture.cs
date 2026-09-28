using System;
using System.IO;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SwMateAI.Core.Manufacturing
{
    public class PartImageCapture
    {
        private readonly ISldWorks _swApp;
        public PartImageCapture(ISldWorks swApp) { _swApp = swApp; }

        public string Capture(BreakdownItem item, string folder)
        {
            if (item == null || !item.IsLoaded || string.IsNullOrWhiteSpace(item.RepresentativeComponentName))
                return string.Empty;
            var assemblyModel = _swApp.ActiveDoc as IModelDoc2;
            var assembly = assemblyModel as IAssemblyDoc;
            var component = assembly?.GetComponentByName(item.RepresentativeComponentName);
            var partModel = component?.GetModelDoc2() as IModelDoc2;
            if (assemblyModel == null || partModel == null) return string.Empty;

            Directory.CreateDirectory(folder);
            string imageId = (string.IsNullOrWhiteSpace(item.PartNumber) ? item.RepresentativeComponentName : item.PartNumber) + "_" + item.Configuration + "_" + item.RepresentativeComponentName;
            string fileName = SafeFileName(imageId) + ".png";
            string output = Path.Combine(folder, fileName);
            string assemblyTitle = assemblyModel.GetTitle() ?? string.Empty;

            int activateErrors = 0;
            try
            {
                _swApp.ActivateDoc3(partModel.GetTitle(), false, 0, ref activateErrors);
                partModel.ShowNamedView2("*Isometric", (int)swStandardViews_e.swIsometricView);
                partModel.ViewZoomtofit2();

                int errors = 0, warnings = 0;
                bool ok = partModel.Extension.SaveAs(
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
            return string.IsNullOrWhiteSpace(value) ? "part" : value;
        }
    }
}


