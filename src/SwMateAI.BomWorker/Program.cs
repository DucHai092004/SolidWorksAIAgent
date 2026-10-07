using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SwMateAI.Core.BOM;

namespace SwMateAI.BomWorker
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args == null || args.Length != 1 || string.IsNullOrWhiteSpace(args[0]))
            {
                Console.Error.WriteLine("Usage: SwMateAI.BomWorker.exe <manifest.json>");
                return 2;
            }

            string manifestPath = Path.GetFullPath(args[0]);
            BomWorkerManifest manifest;
            try
            {
                manifest = BomWorkerManifestSerializer.Read(manifestPath);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Cannot read manifest: " + ex.Message);
                return 3;
            }

            ISldWorks sw = null;
            try
            {
                // CoCreate a dedicated automation session. Do not attach to the user's
                // already-running SOLIDWORKS instance because this worker owns its lifetime.
                Type progId = Type.GetTypeFromProgID("SldWorks.Application", true);
                sw = (ISldWorks)Activator.CreateInstance(progId);
                sw.Visible = false;

                int processed = 0;
                foreach (BomWorkerItem item in manifest.Items)
                {
                    if (item.Completed && File.Exists(item.OutputImagePath))
                    {
                        processed++;
                        continue;
                    }

                    item.Error = string.Empty;
                    item.Skipped = false;
                    try
                    {
                        Render(sw, item);
                        item.Completed = File.Exists(item.OutputImagePath) &&
                                         new FileInfo(item.OutputImagePath).Length > 0;
                        if (!item.Completed)
                            item.Error = "Image file was not created.";
                    }
                    catch (Exception ex)
                    {
                        item.Completed = false;
                        item.Error = ex.Message;
                    }

                    processed++;
                    BomWorkerManifestSerializer.Write(manifestPath, manifest);

                    // Keep long jobs bounded: release RCWs in batches and yield briefly.
                    if (processed % 25 == 0)
                    {
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        Thread.Sleep(25);
                    }
                }

                manifest.Finished = true;
                BomWorkerManifestSerializer.Write(manifestPath, manifest);
                return 0;
            }
            catch (Exception ex)
            {
                manifest.FatalError = ex.ToString();
                manifest.Finished = true;
                try { BomWorkerManifestSerializer.Write(manifestPath, manifest); } catch { }
                Console.Error.WriteLine(ex);
                return 4;
            }
            finally
            {
                if (sw != null)
                {
                    try { sw.ExitApp(); } catch { }
                    try { Marshal.FinalReleaseComObject(sw); } catch { }
                }
            }
        }

        private static void Render(ISldWorks sw, BomWorkerItem item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (string.IsNullOrWhiteSpace(item.SourcePath))
                throw new InvalidOperationException("Source path is empty.");
            if (!File.Exists(item.SourcePath))
            {
                item.Skipped = true;
                throw new FileNotFoundException("Source CAD file is missing.", item.SourcePath);
            }

            string imageDirectory = Path.GetDirectoryName(item.OutputImagePath);
            if (!string.IsNullOrWhiteSpace(imageDirectory)) Directory.CreateDirectory(imageDirectory);

            int errors = 0, warnings = 0;
            int documentType = DocumentType(item.SourcePath);
            var model = sw.OpenDoc6(
                item.SourcePath,
                documentType,
                (int)(swOpenDocOptions_e.swOpenDocOptions_Silent | swOpenDocOptions_e.swOpenDocOptions_ReadOnly),
                item.Configuration ?? string.Empty,
                ref errors,
                ref warnings) as IModelDoc2;

            if (model == null)
                throw new InvalidOperationException("OpenDoc6 failed. Errors=" + errors + ", Warnings=" + warnings);

            string title = model.GetTitle();
            try
            {
                if (!string.IsNullOrWhiteSpace(item.Configuration))
                {
                    try { model.ShowConfiguration2(item.Configuration); } catch { }
                }

                model.ShowNamedView2("*Isometric", (int)swStandardViews_e.swIsometricView);
                model.ViewZoomtofit2();

                int saveErrors = 0, saveWarnings = 0;
                bool saved = model.Extension.SaveAs(
                    item.OutputImagePath,
                    (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                    (int)swSaveAsOptions_e.swSaveAsOptions_Silent,
                    null,
                    ref saveErrors,
                    ref saveWarnings);
                if (!saved || saveErrors != 0)
                    throw new IOException("PNG export failed. Errors=" + saveErrors + ", Warnings=" + saveWarnings);
            }
            finally
            {
                try { sw.CloseDoc(title); } catch { }
            }
        }

        private static int DocumentType(string path)
        {
            string extension = Path.GetExtension(path) ?? string.Empty;
            if (extension.Equals(".SLDASM", StringComparison.OrdinalIgnoreCase))
                return (int)swDocumentTypes_e.swDocASSEMBLY;
            return (int)swDocumentTypes_e.swDocPART;
        }
    }
}
