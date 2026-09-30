using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SwMateAI.Core.Manufacturing;
using Environment = System.Environment;

namespace SwMateAI.StockWorker
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            ISldWorks swApp = null;
            IModelDoc2 model = null;
            Process swProcess = null;
            try
            {
                string source = Arg(args, "--source");
                string output = Arg(args, "--output");
                if (string.IsNullOrWhiteSpace(source) || !File.Exists(source))
                    return Fail("A saved Assembly source is required.");

                if (string.IsNullOrWhiteSpace(output))
                {
                    string root = Path.GetDirectoryName(source) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    string name = Path.GetFileNameWithoutExtension(source);
                    output = Path.Combine(root, "SW-MATE_AI_Output", name + "_StockMaterial.xlsx");
                }
                Directory.CreateDirectory(Path.GetDirectoryName(output) ?? ".");

                Console.WriteLine("PROGRESS|INSTANCE|STARTING");
                Console.Out.Flush();
                swProcess = StartIsolatedSolidWorks();
                if (swProcess == null) return Fail("Could not start isolated SOLIDWORKS process.");
                swApp = WaitForSolidWorksCom(swProcess.Id, 45000);
                if (swApp == null) return Fail("Isolated SOLIDWORKS process did not become available.");
                try { swApp.Visible = false; } catch { }
                Console.WriteLine("PROGRESS|INSTANCE|READY|PID=" + swProcess.Id);
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

                try
                {
                    var assembly = model as IAssemblyDoc;
                    assembly?.ResolveAllLightWeightComponents(false);
                    model.ForceRebuild3(false);
                }
                catch { }

                Console.WriteLine("PROGRESS|STOCK|BUILDING");
                Console.Out.Flush();
                var clock = Stopwatch.StartNew();
                string imageFolder = Path.Combine(Path.GetDirectoryName(output) ?? string.Empty, "PartImages");
                var result = new ManufacturingBreakdownExporter(swApp).Export(
                    output,
                    imageFolder,
                    new StockCalculationOptions());
                clock.Stop();

                Console.WriteLine(
                    "RESULT|OK|" + Escape(result.ExcelPath) +
                    "|ITEMS=" + (result.Breakdown?.Items?.Count ?? 0) +
                    "|IMAGES=" + result.CapturedImageCount +
                    "|MS=" + clock.ElapsedMilliseconds +
                    "|PID=" + swProcess.Id);
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
            try { return process.StartTime; } catch { return DateTime.MinValue; }
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
