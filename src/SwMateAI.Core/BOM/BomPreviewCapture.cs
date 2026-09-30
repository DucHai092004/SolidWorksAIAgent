using System;
using System.IO;
using SolidWorks.Interop.sldworks;

namespace SwMateAI.Core.BOM
{
    /// <summary>
    /// Reads the preview bitmap embedded in a saved SOLIDWORKS document.
    /// Unlike BomImageCapture this does not open, activate, zoom or save the Part/Assembly.
    /// </summary>
    public sealed class BomPreviewCapture
    {
        private readonly ISldWorks _swApp;

        public BomPreviewCapture(ISldWorks swApp)
        {
            _swApp = swApp ?? throw new ArgumentNullException(nameof(swApp));
        }

        public string Capture(BomItem item, string folder)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.SourcePath)) return string.Empty;
            if (!File.Exists(item.SourcePath)) return string.Empty;

            Directory.CreateDirectory(folder);
            string shortName = SafeFileName(string.IsNullOrWhiteSpace(item.PartNumber)
                ? "item"
                : item.PartNumber);
            if (shortName.Length > 60) shortName = shortName.Substring(0, 60);

            string output = Path.Combine(
                folder,
                "preview_" + item.ItemNumber.ToString("D4") + "_" + shortName + ".bmp");

            string configuration = item.Configuration ?? string.Empty;
            try
            {
                bool ok = _swApp.GetPreviewBitmapFile(item.SourcePath, configuration, output);
                if (!ok && !string.IsNullOrWhiteSpace(configuration))
                    ok = _swApp.GetPreviewBitmapFile(item.SourcePath, string.Empty, output);

                if (!ok || !File.Exists(output)) return string.Empty;
                item.ImagePath = output;
                return output;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string SafeFileName(string value)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                value = value.Replace(c, '_');
            return string.IsNullOrWhiteSpace(value) ? "bom_item" : value;
        }
    }
}
