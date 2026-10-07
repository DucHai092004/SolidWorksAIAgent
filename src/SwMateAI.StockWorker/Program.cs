using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SwMateAI.Core.Manufacturing;

namespace SwMateAI.StockWorker
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args == null || args.Length != 1 || string.IsNullOrWhiteSpace(args[0]))
            {
                Console.Error.WriteLine("Usage: SwMateAI.StockWorker.exe <manifest.json>");
                return 2;
            }

            string manifestPath = Path.GetFullPath(args[0]);
            StockWorkerManifest manifest;
            try
            {
                manifest = StockWorkerManifestSerializer.Read(manifestPath);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Cannot read stock manifest: " + ex.Message);
                return 3;
            }

            ISldWorks sw = null;
            string assemblyTitle = string.Empty;
            try
            {
                if (CheckCancelled(manifest, manifestPath, "Cancelled before start")) return 5;
                if (!File.Exists(manifest.AssemblyPath))
                    throw new FileNotFoundException("Assembly file was not found.", manifest.AssemblyPath);

                manifest.StatusMessage = "Starting SOLIDWORKS worker";
                StockWorkerManifestSerializer.Write(manifestPath, manifest);

                Type progId = Type.GetTypeFromProgID("SldWorks.Application", true);
                sw = (ISldWorks)Activator.CreateInstance(progId);
                sw.Visible = false;
                Thread.Sleep(750);

                if (CheckCancelled(manifest, manifestPath, "Cancelled during startup")) return 5;

                int openErrors = 0, openWarnings = 0;
                var model = sw.OpenDoc6(
                    manifest.AssemblyPath,
                    (int)swDocumentTypes_e.swDocASSEMBLY,
                    (int)(swOpenDocOptions_e.swOpenDocOptions_Silent | swOpenDocOptions_e.swOpenDocOptions_ReadOnly),
                    string.Empty,
                    ref openErrors,
                    ref openWarnings) as IModelDoc2;
                if (model == null || model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
                    throw new InvalidOperationException("Could not open Assembly. Errors=" + openErrors + ", Warnings=" + openWarnings);
                assemblyTitle = model.GetTitle() ?? string.Empty;

                manifest.StatusMessage = "Calculating stock breakdown";
                StockWorkerManifestSerializer.Write(manifestPath, manifest);
                BreakdownResult breakdown = new StockExcelBreakdownBuilder(sw).Build();
                manifest.TotalItems = breakdown.Items.Count;
                manifest.StatusMessage = "Capturing part images";
                StockWorkerManifestSerializer.Write(manifestPath, manifest);

                var capture = new PartImageCapture(sw);
                var imageCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < breakdown.Items.Count; i++)
                {
                    if (CheckCancelled(manifest, manifestPath, "Cancelled during image capture")) return 5;

                    BreakdownItem item = breakdown.Items[i];
                    string cacheKey = (item.RepresentativeComponentName ?? string.Empty) + "|" +
                                      (item.Configuration ?? string.Empty);
                    if (imageCache.TryGetValue(cacheKey, out string cached) && File.Exists(cached))
                    {
                        item.ImagePath = cached;
                    }
                    else
                    {
                        string image = string.Empty;
                        try { image = capture.Capture(item, manifest.ImageDirectory); }
                        catch { }

                        if (!string.IsNullOrWhiteSpace(image) && File.Exists(image))
                        {
                            imageCache[cacheKey] = image;
                            manifest.CapturedImageCount++;
                        }
                    }

                    manifest.ProcessedItems = i + 1;
                    manifest.StatusMessage = "Processed " + manifest.ProcessedItems + "/" + manifest.TotalItems;
                    StockWorkerManifestSerializer.Write(manifestPath, manifest);

                    if ((i + 1) % 25 == 0)
                    {
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        Thread.Sleep(10);
                    }
                }

                if (CheckCancelled(manifest, manifestPath, "Cancelled before Excel export")) return 5;

                manifest.StatusMessage = "Writing OpenXML workbook";
                StockWorkerManifestSerializer.Write(manifestPath, manifest);
                BreakdownTable table = new BreakdownTableGenerator().Generate(breakdown);
                string excelPath = new ExcelExporter().Export(table, manifest.OutputExcelPath);
                if (!File.Exists(excelPath) || new FileInfo(excelPath).Length <= 0)
                    throw new IOException("Stock workbook was not created correctly.");

                manifest.OutputRows = table.Rows.Count;
                manifest.Finished = true;
                manifest.StatusMessage = "Completed";
                StockWorkerManifestSerializer.Write(manifestPath, manifest);
                return 0;
            }
            catch (Exception ex)
            {
                manifest.FatalError = ex.ToString();
                manifest.Finished = true;
                manifest.StatusMessage = "Failed";
                try { StockWorkerManifestSerializer.Write(manifestPath, manifest); } catch { }
                Console.Error.WriteLine(ex);
                return 4;
            }
            finally
            {
                if (sw != null)
                {
                    try { if (!string.IsNullOrWhiteSpace(assemblyTitle)) sw.CloseDoc(assemblyTitle); } catch { }
                    try { sw.ExitApp(); } catch { }
                    try { Marshal.FinalReleaseComObject(sw); } catch { }
                }
            }
        }

        private static bool CheckCancelled(
            StockWorkerManifest manifest,
            string manifestPath,
            string status)
        {
            if (manifest == null || string.IsNullOrWhiteSpace(manifest.CancelFlagPath) ||
                !File.Exists(manifest.CancelFlagPath)) return false;

            manifest.Cancelled = true;
            manifest.Finished = true;
            manifest.StatusMessage = status;
            StockWorkerManifestSerializer.Write(manifestPath, manifest);
            return true;
        }
    }
}
