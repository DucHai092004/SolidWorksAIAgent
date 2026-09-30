using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using SolidWorks.Interop.sldworks;

namespace SwMateAI.Core.Manufacturing
{
    /// <summary>
    /// Safe stock-table image capture. It reads the preview already stored in the
    /// SOLIDWORKS file and never activates, rebuilds or switches the user's document.
    /// Missing previews are skipped instead of opening the Part on the UI thread.
    /// </summary>
    public class PartImageCapture
    {
        private readonly ISldWorks _swApp;
        public PartImageCapture(ISldWorks swApp) { _swApp = swApp; }

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
            catch
            {
                return string.Empty;
            }

            Directory.CreateDirectory(folder);
            string imageId =
                (string.IsNullOrWhiteSpace(item.PartNumber) ? "part" : item.PartNumber) + "_" +
                (item.Configuration ?? string.Empty);
            string stem = SafeFileName(imageId);
            string bitmapPath = Path.Combine(folder, stem + ".bmp");
            string pngPath = Path.Combine(folder, stem + ".png");

            try
            {
                bool ok = TryPreview(sourcePath, item.Configuration, bitmapPath);
                if (!ok && !string.IsNullOrWhiteSpace(item.Configuration))
                    ok = TryPreview(sourcePath, string.Empty, bitmapPath);

                if (!ok || !File.Exists(bitmapPath)) return string.Empty;

                using (var image = Image.FromFile(bitmapPath))
                    image.Save(pngPath, ImageFormat.Png);

                if (!File.Exists(pngPath)) return string.Empty;
                item.ImagePath = pngPath;
                return pngPath;
            }
            catch
            {
                return string.Empty;
            }
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
            catch
            {
                return false;
            }
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
