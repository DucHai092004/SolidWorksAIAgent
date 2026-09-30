using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SwMateAI.Core.BOM
{
    public sealed class BomPreviewImageCapture
    {
        private readonly ISldWorks _swApp;
        private readonly Dictionary<string, string> _cache =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public BomPreviewImageCapture(ISldWorks swApp)
        {
            _swApp = swApp ?? throw new ArgumentNullException(nameof(swApp));
        }

        public string Capture(BomItem item, string folder)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.SourcePath))
                return string.Empty;

            string sourcePath = item.SourcePath.Trim();
            if (!File.Exists(sourcePath)) return string.Empty;

            try
            {
                var attributes = File.GetAttributes(sourcePath);
                if ((attributes & FileAttributes.Offline) == FileAttributes.Offline)
                    return string.Empty;
            }
            catch { return string.Empty; }

            string configuration = item.Configuration ?? string.Empty;
            string key = sourcePath + "|" + configuration;
            if (_cache.TryGetValue(key, out var cached) && File.Exists(cached))
            {
                item.ImagePath = cached;
                return cached;
            }

            Directory.CreateDirectory(folder);
            string safeName = SafeFileName(
                string.IsNullOrWhiteSpace(item.PartNumber) ? "item" : item.PartNumber);
            if (safeName.Length > 48) safeName = safeName.Substring(0, 48);

            string stem = "bom_" + item.ItemNumber.ToString("D4") + "_" + safeName;
            string bitmapPath = Path.Combine(folder, stem + ".bmp");
            string pngPath = Path.Combine(folder, stem + ".png");

            try
            {
                bool ok = TryPreview(sourcePath, configuration, bitmapPath);
                if (!ok && !string.IsNullOrWhiteSpace(configuration))
                    ok = TryPreview(sourcePath, string.Empty, bitmapPath);

                if (ok && File.Exists(bitmapPath))
                {
                    using (var image = Image.FromFile(bitmapPath))
                        image.Save(pngPath, ImageFormat.Png);
                }
                else if (IsWorkerStagedPath(sourcePath))
                {
                    TryRenderWorkerCopy(sourcePath, configuration, pngPath);
                }

                if (!File.Exists(pngPath)) return string.Empty;
                item.ImagePath = pngPath;
                _cache[key] = pngPath;
                return pngPath;
            }
            catch { return string.Empty; }
            finally
            {
                try { if (File.Exists(bitmapPath)) File.Delete(bitmapPath); } catch { }
            }
        }

        private bool TryPreview(string sourcePath, string configuration, string bitmapPath)
        {
            try
            {
                if (File.Exists(bitmapPath)) File.Delete(bitmapPath);
                return _swApp.GetPreviewBitmapFile(sourcePath, configuration ?? string.Empty, bitmapPath);
            }
            catch { return false; }
        }

        private bool TryRenderWorkerCopy(string sourcePath, string configuration, string pngPath)
        {
            int documentType = ResolveDocumentType(sourcePath);
            if (documentType == (int)swDocumentTypes_e.swDocNONE) return false;

            IModelDoc2 model = null;
            string title = string.Empty;
            try
            {
                PrepareWorkerRenderWindow();

                int errors = 0;
                int warnings = 0;
                int options =
                    (int)swOpenDocOptions_e.swOpenDocOptions_Silent |
                    (int)swOpenDocOptions_e.swOpenDocOptions_ReadOnly;

                model = _swApp.OpenDoc6(
                    sourcePath,
                    documentType,
                    options,
                    configuration ?? string.Empty,
                    ref errors,
                    ref warnings) as IModelDoc2;

                if (model == null && !string.IsNullOrWhiteSpace(configuration))
                {
                    errors = 0;
                    warnings = 0;
                    model = _swApp.OpenDoc6(
                        sourcePath,
                        documentType,
                        options,
                        string.Empty,
                        ref errors,
                        ref warnings) as IModelDoc2;
                }

                if (model == null) return false;
                title = model.GetTitle() ?? string.Empty;

                int activateErrors = 0;
                try { _swApp.ActivateDoc3(title, false, 0, ref activateErrors); } catch { }
                try { model.Visible = true; } catch { }
                try { model.ForceRebuild3(false); } catch { }
                model.ShowNamedView2("*Isometric", (int)swStandardViews_e.swIsometricView);
                model.ViewZoomtofit2();
                try { model.GraphicsRedraw2(); } catch { }
                Thread.Sleep(500);

                int saveErrors = 0;
                int saveWarnings = 0;
                bool saved = model.Extension.SaveAs(
                    pngPath,
                    (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                    (int)swSaveAsOptions_e.swSaveAsOptions_Silent,
                    null,
                    ref saveErrors,
                    ref saveWarnings);

                return saved && saveErrors == 0 && File.Exists(pngPath);
            }
            catch { return false; }
            finally
            {
                if (!string.IsNullOrWhiteSpace(title))
                {
                    try { _swApp.CloseDoc(title); } catch { }
                }
                Thread.Sleep(200);
            }
        }

        private void PrepareWorkerRenderWindow()
        {
            try
            {
                _swApp.FrameState = (int)swWindowState_e.swWindowNormal;
                _swApp.FrameLeft = -30000;
                _swApp.FrameTop = -30000;
                _swApp.FrameWidth = 1024;
                _swApp.FrameHeight = 768;
                _swApp.Visible = true;
                Thread.Sleep(150);
            }
            catch
            {
                try { _swApp.Visible = true; } catch { }
            }
        }

        private static bool IsWorkerStagedPath(string sourcePath)
        {
            try
            {
                string fullPath = Path.GetFullPath(sourcePath);
                string marker = Path.DirectorySeparatorChar + "SW-MATE_AI" +
                                Path.DirectorySeparatorChar + "BOM_Worker" +
                                Path.DirectorySeparatorChar;
                return fullPath.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch { return false; }
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
            string result = value ?? string.Empty;
            foreach (char c in Path.GetInvalidFileNameChars())
                result = result.Replace(c, '_');
            return string.IsNullOrWhiteSpace(result) ? "bom_item" : result;
        }
    }
}
