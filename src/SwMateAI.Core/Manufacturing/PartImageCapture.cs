using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SwMateAI.Core.Common;

namespace SwMateAI.Core.Manufacturing
{
    public class PartImageCapture
    {
        private readonly ISldWorks _swApp;
        public PartImageCapture(ISldWorks swApp) { _swApp = swApp; }

        public string Capture(BreakdownItem item, string folder)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.RepresentativeComponentName))
                return string.Empty;

            var assemblyModel = _swApp.ActiveDoc as IModelDoc2;
            var assembly = assemblyModel as IAssemblyDoc;
            if (assemblyModel == null || assembly == null) return string.Empty;

            var component = assembly.GetComponentByName(item.RepresentativeComponentName);
            Directory.CreateDirectory(folder);
            string output = Path.Combine(folder, BuildShortImageName(item));
            string assemblyTitle = assemblyModel.GetTitle() ?? string.Empty;
            using (var session = ComponentModelSession.Acquire(
                _swApp,
                component,
                item.SourcePath,
                assemblyTitle,
                readOnly: true,
                hideNewDocument: true))
            {
                var partModel = session.Model;
                if (partModel == null || partModel.GetType() != (int)swDocumentTypes_e.swDocPART)
                    return string.Empty;

                session.ActivateConfiguration(item.Configuration);
                int activateErrors = 0;
                _swApp.ActivateDoc3(partModel.GetTitle(), false, 0, ref activateErrors);
                partModel.ShowNamedView2("*Isometric", (int)swStandardViews_e.swIsometricView);
                partModel.ViewZoomtofit2();

                int errors = 0;
                int warnings = 0;
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
        }

        private static string BuildShortImageName(BreakdownItem item)
        {
            string identity = string.Join("|", new[]
            {
                item.SourcePath ?? string.Empty,
                item.Configuration ?? string.Empty,
                item.RepresentativeComponentName ?? string.Empty,
                item.PartNumber ?? string.Empty
            });

            string hash;
            using (var sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(identity));
                var sb = new StringBuilder(12);
                for (int i = 0; i < 6; i++) sb.Append(bytes[i].ToString("X2"));
                hash = sb.ToString();
            }

            return $"part_{hash}.png";
        }
    }
}
