using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SwMateAI.Core.Manufacturing;
using Environment = System.Environment;

namespace SwMateAI.StockWorker
{
    internal static class Program
    {
        private const int ImageSessionBatchSize = 20;

        private static int Main(string[] args)
        {
            ISldWorks swApp = null;
            IModelDoc2 model = null;
            Process swProcess = null;
            string tempRoot = null;
            int sessionCount = 0;
            int imagesInSession = 0;

            try
            {
                string source = Arg(args, "--source");
                string output = Arg(args, "--output");
                if (string.IsNullOrWhiteSpace(source) || !File.Exists(source))
                    return Fail("A saved Assembly source is required.");

                if (string.IsNullOrWhiteSpace(output))
                {
                    string root = Path.GetDirectoryName(source) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    string name = SafeFileName(Path.GetFileNameWithoutExtension(source));
                    output = Path.Combine(root, "SW-MATE_AI_Output", name + "_StockMaterial.xlsx");
                }
                output = EnsureUniquePath(output);
                string outputFolder = Path.GetDirectoryName(output) ?? ".";
                Directory.CreateDirectory(outputFolder);
                string logFolder = Path.Combine(outputFolder, "Logs");
                Directory.CreateDirectory(logFolder);

                tempRoot = Path.Combine(
                    Path.GetTempPath(),
                    "SW-MATE_AI",
                    "Stock_Worker",
                    Guid.NewGuid().ToString("N"));
                string localSourceFolder = Path.Combine(tempRoot, "source");
                string imageFolder = Path.Combine(tempRoot, "images");
                Directory.CreateDirectory(localSourceFolder);
                Directory.CreateDirectory(imageFolder);

                var clock = Stopwatch.StartNew();
                if (!StartSession(ref swApp, ref swProcess, ++sessionCount))
                    return Fail("Could not start isolated SOLIDWORKS process.");

                Console.WriteLine("PROGRESS|STOCK|OPEN|" + Escape(Path.GetFileName(source)));
                Console.Out.Flush();

                int errors = 0, warnings = 0;
                int options = (int)swOpenDocOptions_e.swOpenDocOptions_Silent |
                              (int)swOpenDocOptions_e.swOpenDocOptions_ReadOnly;
                model = swApp.OpenDoc6(
                    source,
                    (int)swDocumentTypes_e.swDocASSEMBLY,
                    options,
                    string.Empty,
                    ref errors,
                    ref warnings) as IModelDoc2;
                if (model == null)
                    return Fail("Could not open Assembly. Errors=" + errors + ", Warnings=" + warnings + ".");

                string modelTitle = model.GetTitle() ?? string.Empty;
                int activateErrors = 0;
                try { swApp.ActivateDoc3(modelTitle, false, 0, ref activateErrors); } catch { }

                Console.WriteLine("PROGRESS|STOCK|RESOLVE|Lightweight components");
                Console.Out.Flush();
                try
                {
                    var assembly = model as IAssemblyDoc;
                    assembly?.ResolveAllLightWeightComponents(false);
                    model.ForceRebuild3(false);
                }
                catch { }

                Console.WriteLine("PROGRESS|STOCK|BUILD|Material + stock calculation");
                Console.Out.Flush();
                BreakdownResult breakdown = new ManufacturingBreakdownBuilder(swApp, new StockCalculationOptions()).Build();
                if (breakdown == null || breakdown.Items.Count == 0)
                    return Fail("Manufacturing breakdown contains no Part items.");

                try { swApp.CloseDoc(modelTitle); } catch { }
                ReleaseCom(model);
                model = null;
                Thread.Sleep(250);

                Console.WriteLine("PROGRESS|STOCK|IMAGES|" + breakdown.Items.Count + " items");
                Console.Out.Flush();

                var localCopies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var skippedDetails = new List<string>();
                PartImageCapture capture = new PartImageCapture(swApp);
                int captured = 0;
                int skipped = 0;
                int consecutiveSessionFailures = 0;

                for (int i = 0; i < breakdown.Items.Count; i++)
                {
                    BreakdownItem item = breakdown.Items[i];
                    int index = i + 1;
                    string originalSource = item.SourcePath;
                    string localSource = LocalizeSource(originalSource, localSourceFolder, localCopies, index);

                    if (string.IsNullOrWhiteSpace(localSource))
                    {
                        skipped++;
                        skippedDetails.Add(ItemLabel(item) + "|SOURCE_COPY_FAILED|" + originalSource);
                        EmitImageProgress(index, breakdown.Items.Count, captured, skipped);
                        continue;
                    }

                    if (swApp == null || swProcess == null || imagesInSession >= ImageSessionBatchSize)
                    {
                        ShutdownSession(ref swApp, ref swProcess);
                        imagesInSession = 0;

                        if (!StartSession(ref swApp, ref swProcess, ++sessionCount))
                        {
                            consecutiveSessionFailures++;
                            skipped++;
                            skippedDetails.Add(ItemLabel(item) + "|SESSION_START_FAILED|" + originalSource);
                            EmitImageProgress(index, breakdown.Items.Count, captured, skipped);
                            if (consecutiveSessionFailures >= 2)
                            {
                                for (int r = i + 1; r < breakdown.Items.Count; r++)
                                {
                                    skipped++;
                                    skippedDetails.Add(ItemLabel(breakdown.Items[r]) + "|SESSION_UNAVAILABLE|" + breakdown.Items[r].SourcePath);
                                    EmitImageProgress(r + 1, breakdown.Items.Count, captured, skipped);
                                }
                                break;
                            }
                            continue;
                        }

                        consecutiveSessionFailures = 0;
                        capture = new PartImageCapture(swApp);
                    }

                    try
                    {
                        item.SourcePath = localSource;
                        string image = capture.Capture(item, imageFolder);
                        imagesInSession++;
                        if (!string.IsNullOrWhiteSpace(image) && File.Exists(image))
                        {
                            captured++;
                        }
                        else
                        {
                            skipped++;
                            skippedDetails.Add(ItemLabel(item) + "|IMAGE_CAPTURE_FAILED|" + originalSource);
                        }
                    }
                    catch (Exception ex)
                    {
                        skipped++;
                        skippedDetails.Add(ItemLabel(item) + "|EXCEPTION=" + Escape(ex.Message) + "|" + originalSource);
                        ShutdownSession(ref swApp, ref swProcess);
                        imagesInSession = 0;
                    }
                    finally
                    {
                        item.SourcePath = originalSource;
                    }

                    EmitImageProgress(index, breakdown.Items.Count, captured, skipped);
                    if ((index % 5) == 0)
                    {
                        try
                        {
                            GC.Collect();
                            GC.WaitForPendingFinalizers();
                        }
                        catch { }
                    }
                    Thread.Sleep(160);
                }

                ShutdownSession(ref swApp, ref swProcess);

                Console.WriteLine("PROGRESS|STOCK|EXPORT|OpenXML XLSX");
                Console.Out.Flush();
                BreakdownTable table = new BreakdownTableGenerator().Generate(breakdown);
                ExportExcelWithRetry(table, output);
                ValidateExcel(output);
                clock.Stop();

                string logPath = WriteLog(
                    logFolder,
                    breakdown,
                    captured,
                    skipped,
                    sessionCount,
                    clock.ElapsedMilliseconds,
                    skippedDetails);

                Console.WriteLine(
                    "RESULT|OK|" + Escape(output) +
                    "|ITEMS=" + breakdown.Items.Count +
                    "|IMAGES=" + captured +
                    "|SKIPPED=" + skipped +
                    "|UNLOADED=" + breakdown.UnloadedPartCount +
                    "|REVIEW=" + breakdown.StockMaterialsNeedReview +
                    "|SESSIONS=" + sessionCount +
                    "|MS=" + clock.ElapsedMilliseconds +
                    "|LOG=" + Escape(logPath));
                return 0;
            }
            catch (Exception ex)
            {
                return Fail(ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                if (swApp != null && model != null)
                {
                    try { swApp.CloseDoc(model.GetTitle()); } catch { }
                }
                ReleaseCom(model);
                ShutdownSession(ref swApp, ref swProcess);

                if (!string.IsNullOrWhiteSpace(tempRoot) && Directory.Exists(tempRoot))
                {
                    try { Directory.Delete(tempRoot, true); } catch { }
                }
            }
        }

        private static bool StartSession(ref ISldWorks swApp, ref Process swProcess, int sessionNumber)
        {
            Console.WriteLine("PROGRESS|INSTANCE|STARTING|SESSION=" + sessionNumber);
            Console.Out.Flush();

            swProcess = StartIsolatedSolidWorks();
            if (swProcess == null) return false;
            swApp = WaitForSolidWorksCom(swProcess.Id, 45000);
            if (swApp == null)
            {
                ShutdownSession(ref swApp, ref swProcess);
                return false;
            }

            ConfigureOffscreen(swApp);
            Console.WriteLine("PROGRESS|INSTANCE|READY|SESSION=" + sessionNumber + "|PID=" + swProcess.Id);
            Console.Out.Flush();
            return true;
        }

        private static void ShutdownSession(ref ISldWorks swApp, ref Process swProcess)
        {
            if (swApp != null)
            {
                try { swApp.ExitApp(); } catch { }
            }
            ReleaseCom(swApp);
            swApp = null;

            if (swProcess != null)
            {
                try
                {
                    if (!swProcess.WaitForExit(5000) && !swProcess.HasExited)
                        swProcess.Kill();
                }
                catch { }
                try { swProcess.Dispose(); } catch { }
            }
            swProcess = null;
        }

        private static void ConfigureOffscreen(ISldWorks swApp)
        {
            try
            {
                swApp.Visible = true;
                swApp.FrameState = (int)swWindowState_e.swWindowNormal;
                swApp.FrameLeft = -30000;
                swApp.FrameTop = -30000;
                swApp.FrameWidth = 1024;
                swApp.FrameHeight = 768;
            }
            catch { }
        }

        private static string LocalizeSource(
            string sourcePath,
            string localFolder,
            Dictionary<string, string> cache,
            int index)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                return string.Empty;
            if (cache.TryGetValue(sourcePath, out string cached) && File.Exists(cached))
                return cached;

            string extension = Path.GetExtension(sourcePath);
            string destination = Path.Combine(localFolder, "source_" + index.ToString("D4") + extension);
            try
            {
                File.Copy(sourcePath, destination, true);
                cache[sourcePath] = destination;
                return destination;
            }
            catch { return string.Empty; }
        }

        private static void EmitImageProgress(int index, int total, int captured, int skipped)
        {
            Console.WriteLine(
                "PROGRESS|IMAGE|" + index + "/" + total +
                "|OK=" + captured + "|SKIP=" + skipped);
            Console.Out.Flush();
        }

        private static void ExportExcelWithRetry(BreakdownTable table, string output)
        {
            Exception last = null;
            for (int attempt = 1; attempt <= 2; attempt++)
            {
                try
                {
                    new ExcelExporter().Export(table, output);
                    return;
                }
                catch (Exception ex)
                {
                    last = ex;
                    try { if (File.Exists(output)) File.Delete(output); } catch { }
                    if (attempt < 2) Thread.Sleep(500);
                }
            }
            throw new IOException("Excel export failed after retry: " + (last?.Message ?? "unknown error"), last);
        }

        private static void ValidateExcel(string path)
        {
            if (!File.Exists(path)) throw new IOException("Stock Excel was not created: " + path);
            var info = new FileInfo(path);
            if (info.Length < 1024) throw new IOException("Stock Excel is unexpectedly small: " + info.Length + " bytes.");
            using (var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                int first = stream.ReadByte();
                int second = stream.ReadByte();
                if (first != 'P' || second != 'K')
                    throw new IOException("Stock Excel does not have a valid XLSX/ZIP signature.");
            }
        }

        private static string WriteLog(
            string folder,
            BreakdownResult breakdown,
            int captured,
            int skipped,
            int sessions,
            long elapsedMilliseconds,
            IList<string> skippedDetails)
        {
            string path = Path.Combine(folder, "StockWorker_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".log");
            using (var writer = new StreamWriter(path, false, new UTF8Encoding(false)))
            {
                writer.WriteLine("SW-MATE AI Stock Worker");
                writer.WriteLine("Time: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                writer.WriteLine("Items: " + (breakdown?.Items?.Count ?? 0));
                writer.WriteLine("Occurrences: " + (breakdown?.TotalPartOccurrences ?? 0));
                writer.WriteLine("SuppressedSkipped: " + (breakdown?.SuppressedSkipped ?? 0));
                writer.WriteLine("UnloadedParts: " + (breakdown?.UnloadedPartCount ?? 0));
                writer.WriteLine("Images: " + captured);
                writer.WriteLine("ImageSkipped: " + skipped);
                writer.WriteLine("MaterialNeedsReview: " + (breakdown?.StockMaterialsNeedReview ?? 0));
                writer.WriteLine("MaterialImportErrors: " + (breakdown?.MaterialImportErrors ?? 0));
                writer.WriteLine("Sessions: " + sessions);
                writer.WriteLine("ElapsedMs: " + elapsedMilliseconds);
                foreach (string detail in skippedDetails ?? Array.Empty<string>())
                    writer.WriteLine("SKIP|" + detail);
            }
            return path;
        }

        private static string ItemLabel(BreakdownItem item)
        {
            if (item == null) return "Part";
            return string.IsNullOrWhiteSpace(item.PartNumber)
                ? Path.GetFileName(item.SourcePath)
                : item.PartNumber;
        }

        private static string EnsureUniquePath(string requested)
        {
            string full = Path.GetFullPath(requested);
            if (!Path.GetExtension(full).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
                full = Path.ChangeExtension(full, ".xlsx");
            if (!File.Exists(full)) return full;

            string dir = Path.GetDirectoryName(full) ?? string.Empty;
            string name = Path.GetFileNameWithoutExtension(full);
            for (int i = 1; i < 10000; i++)
            {
                string candidate = Path.Combine(dir, name + "_" + i + ".xlsx");
                if (!File.Exists(candidate)) return candidate;
            }
            throw new IOException("Could not allocate a unique stock Excel file name.");
        }

        private static string SafeFileName(string value)
        {
            string result = value ?? string.Empty;
            foreach (char c in Path.GetInvalidFileNameChars()) result = result.Replace(c, '_');
            return string.IsNullOrWhiteSpace(result) ? "Assembly" : result;
        }

        private static Process StartIsolatedSolidWorks()
        {
            var before = new HashSet<int>(Process.GetProcessesByName("SLDWORKS").Select(p => p.Id));
            string exe = ResolveSolidWorksExecutable();
            if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe)) return null;
            Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                Arguments = "/b",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });
            var clock = Stopwatch.StartNew();
            while (clock.ElapsedMilliseconds < 45000)
            {
                Process candidate = Process.GetProcessesByName("SLDWORKS")
                    .Where(p => !before.Contains(p.Id))
                    .OrderByDescending(SafeStartTime)
                    .FirstOrDefault();
                if (candidate != null) return candidate;
                Thread.Sleep(500);
            }
            return null;
        }

        private static string ResolveSolidWorksExecutable()
        {
            try
            {
                Process existing = Process.GetProcessesByName("SLDWORKS").FirstOrDefault();
                string path = existing?.MainModule?.FileName;
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path)) return path;
            }
            catch { }
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "SOLIDWORKS Corp",
                "SOLIDWORKS",
                "SLDWORKS.exe");
        }

        private static ISldWorks WaitForSolidWorksCom(int processId, int timeoutMilliseconds)
        {
            string target = "SolidWorks_PID_" + processId;
            var clock = Stopwatch.StartNew();
            while (clock.ElapsedMilliseconds < timeoutMilliseconds)
            {
                object value = TryGetRotObject(target);
                if (value is ISldWorks app) return app;
                ReleaseCom(value);
                Thread.Sleep(500);
            }
            return null;
        }

        private static object TryGetRotObject(string targetName)
        {
            IRunningObjectTable rot = null;
            IEnumMoniker enumerator = null;
            IBindCtx bindContext = null;
            try
            {
                if (GetRunningObjectTable(0, out rot) != 0 || rot == null) return null;
                rot.EnumRunning(out enumerator);
                if (enumerator == null) return null;
                var monikers = new IMoniker[1];
                while (enumerator.Next(1, monikers, IntPtr.Zero) == 0)
                {
                    IMoniker moniker = monikers[0];
                    try
                    {
                        if (CreateBindCtx(0, out bindContext) != 0 || bindContext == null) continue;
                        string displayName = string.Empty;
                        try { moniker.GetDisplayName(bindContext, null, out displayName); } catch { }
                        if (string.Equals(displayName, targetName, StringComparison.OrdinalIgnoreCase))
                        {
                            rot.GetObject(moniker, out object value);
                            return value;
                        }
                    }
                    finally
                    {
                        ReleaseCom(bindContext);
                        bindContext = null;
                        ReleaseCom(moniker);
                    }
                }
                return null;
            }
            finally
            {
                ReleaseCom(bindContext);
                ReleaseCom(enumerator);
                ReleaseCom(rot);
            }
        }

        private static DateTime SafeStartTime(Process process)
        {
            try { return process.StartTime; }
            catch { return DateTime.MinValue; }
        }

        private static string Arg(string[] args, string key)
        {
            for (int i = 0; args != null && i < args.Length - 1; i++)
                if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1] ?? string.Empty;
            return string.Empty;
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Replace("|", "/");
        }

        private static int Fail(string message)
        {
            Console.WriteLine("RESULT|ERROR|" + Escape(message));
            return 1;
        }

        private static void ReleaseCom(object value)
        {
            if (value == null || !Marshal.IsComObject(value)) return;
            try { Marshal.FinalReleaseComObject(value); } catch { }
        }

        [DllImport("ole32.dll")]
        private static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable runningObjectTable);

        [DllImport("ole32.dll")]
        private static extern int CreateBindCtx(int reserved, out IBindCtx bindContext);
    }
}
