using System;
using System.IO;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SwMateAI.Core.Common
{
    internal sealed class TemporaryModelCaptureSession : IDisposable
    {
        private readonly ISldWorks _swApp;
        private readonly string _returnDocumentTitle;
        private readonly bool _closeAfterCapture;
        private readonly bool _hideAfterCapture;
        private readonly string _openedTitle;

        public IModelDoc2 Model { get; }

        private TemporaryModelCaptureSession(ISldWorks swApp, IModelDoc2 model,
            string returnDocumentTitle, bool closeAfterCapture, bool hideAfterCapture, string openedTitle)
        {
            _swApp = swApp;
            Model = model;
            _returnDocumentTitle = returnDocumentTitle ?? string.Empty;
            _closeAfterCapture = closeAfterCapture;
            _hideAfterCapture = hideAfterCapture;
            _openedTitle = openedTitle ?? string.Empty;
        }

        public static TemporaryModelCaptureSession Acquire(ISldWorks swApp,
            IModelDoc2 preferredModel, string sourcePath, string returnDocumentTitle)
        {
            if (swApp == null) return new TemporaryModelCaptureSession(null, null, returnDocumentTitle, false, false, string.Empty);

            if (preferredModel != null)
            {
                bool hideAfter = !IsVisible(preferredModel) &&
                    !string.Equals(preferredModel.GetTitle(), returnDocumentTitle, StringComparison.OrdinalIgnoreCase);
                return new TemporaryModelCaptureSession(
                    swApp, preferredModel, returnDocumentTitle, false, hideAfter, preferredModel.GetTitle());
            }

            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                return new TemporaryModelCaptureSession(swApp, null, returnDocumentTitle, false, false, string.Empty);

            var existing = FindOpenDocument(swApp, sourcePath);
            if (existing != null)
            {
                bool hideAfter = !IsVisible(existing) &&
                    !string.Equals(existing.GetTitle(), returnDocumentTitle, StringComparison.OrdinalIgnoreCase);
                return new TemporaryModelCaptureSession(
                    swApp, existing, returnDocumentTitle, false, hideAfter, existing.GetTitle());
            }

            int docType = DocumentType(sourcePath);
            if (docType == 0)
                return new TemporaryModelCaptureSession(swApp, null, returnDocumentTitle, false, false, string.Empty);

            int errors = 0, warnings = 0;
            int options = (int)(swOpenDocOptions_e.swOpenDocOptions_Silent |
                                swOpenDocOptions_e.swOpenDocOptions_ReadOnly);
            var opened = swApp.OpenDoc6(sourcePath, docType, options, string.Empty, ref errors, ref warnings) as IModelDoc2;
            if (opened == null)
                return new TemporaryModelCaptureSession(swApp, null, returnDocumentTitle, false, false, string.Empty);

            return new TemporaryModelCaptureSession(swApp, opened, returnDocumentTitle, true, false, opened.GetTitle());
        }

        public void ActivateConfiguration(string configuration)
        {
            if (Model == null || string.IsNullOrWhiteSpace(configuration)) return;
            try { Model.ShowConfiguration2(configuration); } catch { }
        }

        public void Dispose()
        {
            if (_swApp == null) return;

            if (_closeAfterCapture && !string.IsNullOrWhiteSpace(_openedTitle) &&
                !string.Equals(_openedTitle, _returnDocumentTitle, StringComparison.OrdinalIgnoreCase))
            {
                try { _swApp.CloseDoc(_openedTitle); } catch { }
            }

            if (!string.IsNullOrWhiteSpace(_returnDocumentTitle))
            {
                int errors = 0;
                try { _swApp.ActivateDoc3(_returnDocumentTitle, false, 0, ref errors); } catch { }
            }

            if (_hideAfterCapture && Model != null)
            {
                try { Model.Visible = false; } catch { }
            }
        }

        private static IModelDoc2 FindOpenDocument(ISldWorks swApp, string sourcePath)
        {
            if (swApp == null || string.IsNullOrWhiteSpace(sourcePath)) return null;
            string[] candidates =
            {
                sourcePath,
                Path.GetFileName(sourcePath),
                Path.GetFileNameWithoutExtension(sourcePath)
            };
            foreach (string candidate in candidates)
            {
                if (string.IsNullOrWhiteSpace(candidate)) continue;
                try
                {
                    var model = swApp.GetOpenDocumentByName(candidate) as IModelDoc2;
                    if (model != null) return model;
                }
                catch { }
            }
            return null;
        }

        private static bool IsVisible(IModelDoc2 model)
        {
            if (model == null) return false;
            try { return model.Visible; } catch { return false; }
        }

        private static int DocumentType(string path)
        {
            string ext = Path.GetExtension(path) ?? string.Empty;
            if (ext.Equals(".SLDPRT", StringComparison.OrdinalIgnoreCase)) return (int)swDocumentTypes_e.swDocPART;
            if (ext.Equals(".SLDASM", StringComparison.OrdinalIgnoreCase)) return (int)swDocumentTypes_e.swDocASSEMBLY;
            return 0;
        }
    }
}