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
using Environment = System.Environment;

namespace SwMateAI.DrawingWorker
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            ISldWorks swApp = null;
            Process swProcess = null;
            try
            {
                string manifestPath = Arg(args, "--manifest");
                if (string.IsNullOrWhiteSpace(manifestPath) || !File.Exists(manifestPath))
                    return Fail("Drawing manifest was not found.");

                Job job = ReadManifest(manifestPath);
                if (job.Sources.Count == 0) return Fail("Drawing manifest has no source files.");
                if (string.IsNullOrWhiteSpace(job.TemplatePath) || !File.Exists(job.TemplatePath))
                    return Fail("Drawing template was not found.");

                Directory.CreateDirectory(job.OutputFolder);
                string drawingFolder = Path.Combine(job.OutputFolder, "SLDDRW");
                string pdfFolder = Path.Combine(job.OutputFolder, "PDF");
                string logFolder = Path.Combine(job.OutputFolder, "Logs");
                if (job.SaveDrawing) Directory.CreateDirectory(drawingFolder);
                if (job.ExportPdf) Directory.CreateDirectory(pdfFolder);
                Directory.CreateDirectory(logFolder);

                Console.WriteLine("PROGRESS|INSTANCE|STARTING");
                Console.Out.Flush();
                swProcess = StartIsolatedSolidWorks();
                if (swProcess == null) return Fail("Could not start isolated SOLIDWORKS process.");

                swApp = WaitForSolidWorksCom(swProcess.Id, 45000);
                if (swApp == null) return Fail("Isolated SOLIDWORKS process did not become available.");
                ConfigureOffscreen(swApp);
                Console.WriteLine("PROGRESS|INSTANCE|READY|PID=" + swProcess.Id);
                Console.Out.Flush();

                var succeeded = new List<string>();
                var failed = new List<string>();
                var clock = Stopwatch.StartNew();

                for (int i = 0; i < job.Sources.Count; i++)
                {
                    string source = job.Sources[i];
                    try
                    {
                        ExportOne(swApp, job, source, drawingFolder, pdfFolder);
                        succeeded.Add(source);
                        Console.WriteLine("PROGRESS|FILE|" + (i + 1) + "/" + job.Sources.Count + "|OK|" + Escape(Path.GetFileName(source)));
                    }
                    catch (Exception ex)
                    {
                        string error = Path.GetFileName(source) + ": " + ex.Message;
                        failed.Add(error);
                        Console.WriteLine("PROGRESS|FILE|" + (i + 1) + "/" + job.Sources.Count + "|ERROR|" + Escape(error));
                    }
                    Console.Out.Flush();
                    if (job.PauseMilliseconds > 0) Thread.Sleep(job.PauseMilliseconds);
                }

                clock.Stop();
                string logPath = WriteLog(logFolder, succeeded, failed);
                if (succeeded.Count == 0)
                    return Fail("All drawing exports failed. Log=" + logPath);

                Console.WriteLine(
                    "RESULT|OK|" + Escape(job.OutputFolder) +
                    "|SUCCEEDED=" + succeeded.Count +
                    "|FAILED=" + failed.Count +
                    "|MS=" + clock.ElapsedMilliseconds +
                    "|LOG=" + Escape(logPath) +
                    "|PID=" + swProcess.Id);
                return 0;
            }
            catch (Exception ex)
            {
                return Fail(ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                if (swApp != null)
                {
                    try { swApp.ExitApp(); } catch { }
                }
                ReleaseCom(swApp);
                if (swProcess != null)
                {
                    try
                    {
                        if (!swProcess.WaitForExit(5000) && !swProcess.HasExited) swProcess.Kill();
                    }
                    catch { }
                    try { swProcess.Dispose(); } catch { }
                }
            }
        }

        private static void ExportOne(ISldWorks swApp, Job job, string sourcePath, string drawingFolder, string pdfFolder)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                throw new FileNotFoundException("Source model was not found.", sourcePath);

            int sourceType = ResolveDocumentType(sourcePath);
            if (sourceType == (int)swDocumentTypes_e.swDocNONE)
                throw new InvalidOperationException("Unsupported source type: " + Path.GetExtension(sourcePath));

            IModelDoc2 sourceModel = null;
            IModelDoc2 drawingModel = null;
            string sourceTitle = string.Empty;
            string drawingTitle = string.Empty;
            try
            {
                int openErrors = 0, openWarnings = 0;
                int openOptions = (int)swOpenDocOptions_e.swOpenDocOptions_Silent |
                                  (int)swOpenDocOptions_e.swOpenDocOptions_ReadOnly;
                sourceModel = swApp.OpenDoc6(
                    sourcePath,
                    sourceType,
                    openOptions,
                    string.Empty,
                    ref openErrors,
                    ref openWarnings) as IModelDoc2;
                if (sourceModel == null)
                    throw new InvalidOperationException(
                        "Could not open source model. Errors=" + openErrors + ", Warnings=" + openWarnings + ".");

                sourceTitle = sourceModel.GetTitle() ?? string.Empty;
                int activateErrors = 0;
                swApp.ActivateDoc3(sourceTitle, false, 0, ref activateErrors);
                try { sourceModel.ForceRebuild3(false); } catch { }
                Thread.Sleep(120);

                drawingModel = swApp.NewDocument(job.TemplatePath, 0, 0, 0) as IModelDoc2;
                var drawing = drawingModel as IDrawingDoc;
                if (drawingModel == null || drawing == null)
                    throw new InvalidOperationException("SOLIDWORKS could not create a Drawing document.");
                drawingTitle = drawingModel.GetTitle() ?? string.Empty;

                bool created = IsFirstAngle(job.Projection)
                    ? drawing.Create1stAngleViews2(sourcePath)
                    : drawing.Create3rdAngleViews2(sourcePath);
                if (!created)
                    throw new InvalidOperationException("Could not create standard drawing views.");

                drawingModel.ForceRebuild3(false);
                drawingModel.GraphicsRedraw2();
                Thread.Sleep(220);
                string baseName = SafeBaseName(sourcePath);

                if (job.SaveDrawing)
                    SaveDrawing(drawingModel, UniquePath(Path.Combine(drawingFolder, baseName + ".SLDDRW")));
                if (job.ExportPdf)
                    SavePdf(swApp, drawingModel, UniquePath(Path.Combine(pdfFolder, baseName + ".pdf")));
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(drawingTitle))
                {
                    try { swApp.CloseDoc(drawingTitle); } catch { }
                }
                ReleaseCom(drawingModel);

                if (!string.IsNullOrWhiteSpace(sourceTitle))
                {
                    try { swApp.CloseDoc(sourceTitle); } catch { }
                }
                ReleaseCom(sourceModel);
            }
        }

        private static int ResolveDocumentType(string path)
        {
            if (path.EndsWith(".SLDPRT", StringComparison.OrdinalIgnoreCase))
                return (int)swDocumentTypes_e.swDocPART;
            if (path.EndsWith(".SLDASM", StringComparison.OrdinalIgnoreCase))
                return (int)swDocumentTypes_e.swDocASSEMBLY;
            return (int)swDocumentTypes_e.swDocNONE;
        }

        private static void SaveDrawing(IModelDoc2 model, string path)
        {
            int errors = 0, warnings = 0;
            bool ok = model.Extension.SaveAs(path,
                (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                (int)swSaveAsOptions_e.swSaveAsOptions_Silent,
                null, ref errors, ref warnings);
            if (!ok || errors != 0 || !File.Exists(path))
                throw new IOException("SLDDRW save failed. Errors=" + errors + ", Warnings=" + warnings + ".");
        }

        private static void SavePdf(ISldWorks swApp, IModelDoc2 model, string path)
        {
            var data = swApp.GetExportFileData((int)swExportDataFileType_e.swExportPdfData) as IExportPdfData;
            if (data == null) throw new InvalidOperationException("PDF export data is unavailable.");
            try
            {
                data.ViewPdfAfterSaving = false;
                data.SetSheets((int)swExportDataSheetsToExport_e.swExportData_ExportAllSheets, null);
                int errors = 0, warnings = 0;
                bool ok = model.Extension.SaveAs(path,
                    (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                    (int)swSaveAsOptions_e.swSaveAsOptions_Silent,
                    data, ref errors, ref warnings);
                if (!ok || errors != 0 || !File.Exists(path))
                    throw new IOException("PDF export failed. Errors=" + errors + ", Warnings=" + warnings + ".");
            }
            finally
            {
                ReleaseCom(data);
            }
        }

        private static Job ReadManifest(string path)
        {
            var job = new Job();
            foreach (string raw in File.ReadLines(path, Encoding.UTF8))
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                int split = raw.IndexOf('|');
                string key = split < 0 ? raw : raw.Substring(0, split);
                string value = split < 0 ? string.Empty : raw.Substring(split + 1);
                switch (key)
                {
                    case "TEMPLATE": job.TemplatePath = Decode(value); break;
                    case "OUTPUT": job.OutputFolder = Decode(value); break;
                    case "PROJECTION": job.Projection = Decode(value); break;
                    case "SAVEDRAWING": job.SaveDrawing = value == "1"; break;
                    case "EXPORTPDF": job.ExportPdf = value == "1"; break;
                    case "PAUSE": if (int.TryParse(value, out int p)) job.PauseMilliseconds = Math.Max(0, Math.Min(3000, p)); break;
                    case "SOURCE": job.Sources.Add(Decode(value)); break;
                }
            }
            if (string.IsNullOrWhiteSpace(job.OutputFolder))
                job.OutputFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SW-MATE_AI_Output", "DrawingPackage");
            if (string.IsNullOrWhiteSpace(job.Projection)) job.Projection = "Third";
            return job;
        }

        private static string WriteLog(string folder, IList<string> succeeded, IList<string> failed)
        {
            string path = Path.Combine(folder, "DrawingWorker_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".log");
            using (var writer = new StreamWriter(path, false, Encoding.UTF8))
            {
                writer.WriteLine("SW-MATE AI Drawing Worker");
                writer.WriteLine("Time: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                writer.WriteLine("Succeeded: " + succeeded.Count);
                writer.WriteLine("Failed: " + failed.Count);
                foreach (string s in succeeded) writer.WriteLine("OK|" + s);
                foreach (string s in failed) writer.WriteLine("ERROR|" + s);
            }
            return path;
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

        private static void ConfigureOffscreen(ISldWorks swApp)
        {
            try
            {
                swApp.Visible = true;
                swApp.FrameState = (int)swWindowState_e.swWindowNormal;
                swApp.FrameLeft = -20000;
                swApp.FrameTop = -20000;
                swApp.FrameWidth = 1200;
                swApp.FrameHeight = 900;
            }
            catch { }
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
            try { return process.StartTime; } catch { return DateTime.MinValue; }
        }

        private static bool IsFirstAngle(string value)
        {
            string v = (value ?? string.Empty).Trim().ToLowerInvariant();
            return v == "first" || v == "first angle" || v == "1";
        }

        private static string SafeBaseName(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path ?? string.Empty);
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return string.IsNullOrWhiteSpace(name) ? "Drawing" : name;
        }

        private static string UniquePath(string path)
        {
            if (!File.Exists(path)) return path;
            string directory = Path.GetDirectoryName(path) ?? string.Empty;
            string name = Path.GetFileNameWithoutExtension(path);
            string extension = Path.GetExtension(path);
            for (int i = 1; i < 10000; i++)
            {
                string candidate = Path.Combine(directory, name + "_" + i + extension);
                if (!File.Exists(candidate)) return candidate;
            }
            throw new IOException("Could not allocate output path.");
        }

        private static string Arg(string[] args, string key)
        {
            for (int i = 0; args != null && i < args.Length - 1; i++)
                if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1] ?? string.Empty;
            return string.Empty;
        }

        private static string Decode(string value)
        {
            try { return Encoding.UTF8.GetString(Convert.FromBase64String(value ?? string.Empty)); }
            catch { return string.Empty; }
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

        private sealed class Job
        {
            public string TemplatePath { get; set; } = string.Empty;
            public string OutputFolder { get; set; } = string.Empty;
            public string Projection { get; set; } = "Third";
            public bool SaveDrawing { get; set; } = true;
            public bool ExportPdf { get; set; }
            public int PauseMilliseconds { get; set; } = 250;
            public List<string> Sources { get; } = new List<string>();
        }

        [DllImport("ole32.dll")]
        private static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable runningObjectTable);

        [DllImport("ole32.dll")]
        private static extern int CreateBindCtx(int reserved, out IBindCtx bindContext);
    }
}
