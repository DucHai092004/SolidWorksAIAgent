using System;
using System.IO;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SwMateAI.Core.Common
{
    /// <summary>
    /// Owns access to a component model without leaking document lifecycle side effects.
    /// A model opened by this resolver is closed by Dispose; user-owned documents are preserved.
    /// </summary>
    internal sealed class ComponentModelSession : IDisposable
    {
        private readonly ISldWorks _swApp;
        private readonly string _returnDocumentTitle;
        private readonly bool _closeOnDispose;
        private readonly bool _hideOnDispose;
        private readonly string _openedTitle;
        private string _originalConfiguration;
        private bool _configurationChanged;

        public IModelDoc2 Model { get; }
        public bool OpenedByResolver { get; }
        public string SourcePath { get; }

        private ComponentModelSession(
            ISldWorks swApp,
            IModelDoc2 model,            string sourcePath,
            string returnDocumentTitle,
            bool closeOnDispose,
            bool hideOnDispose,
            string openedTitle)
        {
            _swApp = swApp;
            Model = model;
            SourcePath = sourcePath ?? string.Empty;
            _returnDocumentTitle = returnDocumentTitle ?? string.Empty;
            _closeOnDispose = closeOnDispose;
            _hideOnDispose = hideOnDispose;
            _openedTitle = openedTitle ?? string.Empty;
            OpenedByResolver = closeOnDispose;
        }

        public static ComponentModelSession Acquire(
            ISldWorks swApp,
            IComponent2 component,
            string sourcePath,
            string returnDocumentTitle,
            bool readOnly = true,
            bool hideNewDocument = true)
        {
            if (swApp == null)
                return Empty(returnDocumentTitle);

            var preferred = component?.GetModelDoc2() as IModelDoc2;            if (preferred != null)
                return FromExisting(swApp, preferred, sourcePath, returnDocumentTitle);

            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                return Empty(returnDocumentTitle);

            var existing = FindOpenDocument(swApp, sourcePath);
            if (existing != null)
                return FromExisting(swApp, existing, sourcePath, returnDocumentTitle);

            int docType = GetDocumentType(sourcePath);
            if (docType == 0)
                return Empty(returnDocumentTitle);

            int errors = 0;
            int warnings = 0;
            int options = (int)swOpenDocOptions_e.swOpenDocOptions_Silent;
            if (readOnly)
                options |= (int)swOpenDocOptions_e.swOpenDocOptions_ReadOnly;

            bool visibilityChanged = false;
            try
            {
                if (hideNewDocument)
                {
                    swApp.DocumentVisible(false, docType);
                    visibilityChanged = true;
                }                var opened = swApp.OpenDoc6(
                    sourcePath,
                    docType,
                    options,
                    string.Empty,
                    ref errors,
                    ref warnings) as IModelDoc2;

                if (opened == null)
                    return Empty(returnDocumentTitle);

                return new ComponentModelSession(
                    swApp,
                    opened,
                    sourcePath,
                    returnDocumentTitle,
                    closeOnDispose: true,
                    hideOnDispose: false,
                    openedTitle: opened.GetTitle());
            }
            finally
            {
                if (visibilityChanged)
                {
                    try { swApp.DocumentVisible(true, docType); } catch { }
                }
            }
        }

        public void ActivateConfiguration(string configuration)        {
            if (Model == null || string.IsNullOrWhiteSpace(configuration)) return;

            try
            {
                var active = Model.ConfigurationManager?.ActiveConfiguration;
                _originalConfiguration = active?.Name ?? string.Empty;
                if (string.Equals(_originalConfiguration, configuration, StringComparison.OrdinalIgnoreCase))
                    return;

                if (Model.ShowConfiguration2(configuration))
                    _configurationChanged = true;
            }
            catch { }
        }

        public bool SaveIfOpenedByResolver(out int errors, out int warnings)
        {
            errors = 0;
            warnings = 0;
            if (Model == null) return false;
            if (!OpenedByResolver) return true;

            try
            {
                bool ok = Model.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref errors, ref warnings);
                return ok && errors == 0;
            }
            catch
            {
                return false;
            }        }

        public void Dispose()
        {
            RestoreConfiguration();

            if (_swApp == null) return;
            if (_closeOnDispose && !string.IsNullOrWhiteSpace(_openedTitle) &&
                !string.Equals(_openedTitle, _returnDocumentTitle, StringComparison.OrdinalIgnoreCase))
            {
                try { _swApp.CloseDoc(_openedTitle); } catch { }
            }

            if (!string.IsNullOrWhiteSpace(_returnDocumentTitle))
            {
                int errors = 0;
                try { _swApp.ActivateDoc3(_returnDocumentTitle, false, 0, ref errors); } catch { }
            }

            if (_hideOnDispose && Model != null)
            {
                try { Model.Visible = false; } catch { }
            }
        }

        private void RestoreConfiguration()
        {
            if (!_configurationChanged || Model == null || string.IsNullOrWhiteSpace(_originalConfiguration)) return;
            try { Model.ShowConfiguration2(_originalConfiguration); } catch { }
        }
        private static ComponentModelSession FromExisting(
            ISldWorks swApp,
            IModelDoc2 model,
            string sourcePath,
            string returnDocumentTitle)
        {
            bool hideAfter = !IsVisible(model) &&
                !string.Equals(model.GetTitle(), returnDocumentTitle, StringComparison.OrdinalIgnoreCase);
            return new ComponentModelSession(
                swApp,
                model,
                sourcePath,
                returnDocumentTitle,
                closeOnDispose: false,
                hideOnDispose: hideAfter,
                openedTitle: model.GetTitle());
        }

        private static ComponentModelSession Empty(string returnDocumentTitle)
        {
            return new ComponentModelSession(
                null, null, string.Empty, returnDocumentTitle,
                closeOnDispose: false, hideOnDispose: false, openedTitle: string.Empty);
        }

        private static IModelDoc2 FindOpenDocument(ISldWorks swApp, string sourcePath)
        {            string[] candidates =
            {
                sourcePath,
                Path.GetFileName(sourcePath),
                Path.GetFileNameWithoutExtension(sourcePath)
            };

            foreach (var candidate in candidates)
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

        private static int GetDocumentType(string path)
        {            string extension = Path.GetExtension(path) ?? string.Empty;
            if (extension.Equals(".SLDPRT", StringComparison.OrdinalIgnoreCase))
                return (int)swDocumentTypes_e.swDocPART;
            if (extension.Equals(".SLDASM", StringComparison.OrdinalIgnoreCase))
                return (int)swDocumentTypes_e.swDocASSEMBLY;
            return 0;
        }
    }
}
