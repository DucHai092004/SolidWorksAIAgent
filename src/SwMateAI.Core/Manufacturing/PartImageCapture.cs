using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SwMateAI.Core.Manufacturing
{
    public class PartImageCapture
    {
        private readonly ISldWorks _swApp;
        private readonly Dictionary<string, string> _cache =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public PartImageCapture(ISldWorks swApp)
        {
            _swApp = swApp ?? throw new ArgumentNullException(nameof(swApp));
        }

        public string Capture(BreakdownItem item, string folder)
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
            if (_cache.TryGetValue(key, out string cached) && File.Exists(cached))
            {
                item.ImagePath = cached;
                return cached;
            }

            Directory.CreateDirectory(folder);
            string imageId =
                (string.IsNullOrWhiteSpace(item.PartNumber) ? "part" : item.PartNumber) + "_" +
                configuration;
            string stem = SafeFileName(imageId);
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
                else if (IsStockWorkerStagedPath(sourcePath))
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
                    (int)swDocumentTypes_e.swDocPART,
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
                        (int)swDocumentTypes_e.swDocPART,
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
                Thread.Sleep(450);

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
                Thread.Sleep(180);
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
                Thread.Sleep(120);
            }
            catch
            {
                try { _swApp.Visible = true; } catch { }
            }
        }

        private static bool IsStockWorkerStagedPath(string sourcePath)
        {
            try
            {
                string fullPath = Path.GetFullPath(sourcePath);
                string marker = Path.DirectorySeparatorChar + "SW-MATE_AI" +
                                Path.DirectorySeparatorChar + "Stock_Worker" +
                                Path.DirectorySeparatorChar;
                return fullPath.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch { return false; }
        }

        private static string SafeFileName(string value)
        {
            string result = value ?? string.Empty;
            foreach (char c in Path.GetInvalidFileNameChars()) result = result.Replace(c, '_');
            if (result.Length > 80) result = result.Substring(0, 80);
            return string.IsNullOrWhiteSpace(result) ? "part" : result;
        }
    }
}
