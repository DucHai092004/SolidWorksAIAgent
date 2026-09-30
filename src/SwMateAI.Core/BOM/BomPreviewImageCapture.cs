using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using SolidWorks.Interop.sldworks;

namespace SwMateAI.Core.BOM
{
    /// <summary>
    /// Lightweight BOM preview capture.
    /// Uses the preview already stored in each SOLIDWORKS file and never opens,
    /// activates, rebuilds or changes the active component document.
    /// </summary>
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
            catch
            {
                return string.Empty;
            }

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

                if (!ok || !File.Exists(bitmapPath)) return string.Empty;

                using (var image = Image.FromFile(bitmapPath))
                    image.Save(pngPath, ImageFormat.Png);

                if (!File.Exists(pngPath)) return string.Empty;
                item.ImagePath = pngPath;
                _cache[key] = pngPath;
                return pngPath;
            }
            catch
            {
                return string.Empty;
            }
            finally
            {
                try
                {
                    if (File.Exists(bitmapPath)) File.Delete(bitmapPath);
                }
                catch { }
            }
        }

        private bool TryPreview(string sourcePath, string configuration, string bitmapPath)
        {
            try
            {
                if (File.Exists(bitmapPath)) File.Delete(bitmapPath);
                return _swApp.GetPreviewBitmapFile(
                    sourcePath,
                    configuration ?? string.Empty,
                    bitmapPath);
            }
            catch
            {
                return false;
            }
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
