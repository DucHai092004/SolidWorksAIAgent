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
using SwMateAI.Core.BOM;
using SwMateAI.Core.Tools;
using SwMateAI.Core.Tools.BOM;

namespace SwMateAI.BomWorker
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            ISldWorks isolated = null;
            IModelDoc2 isolatedModel = null;
            Process isolatedProcess = null;
            try
            {
                string assemblyPath = ParseArgument(args, "--source");
                string configuration = ParseArgument(args, "--configuration");
                string outputFolder = ParseArgument(args, "--output");

                if (string.IsNullOrWhiteSpace(assemblyPath) || !File.Exists(assemblyPath))
                    return Fail("A saved source Assembly is required.");

                Console.WriteLine("PROGRESS|SOURCE|" + Escape(assemblyPath));
                Console.WriteLine("PROGRESS|INSTANCE|STARTING");
                Console.Out.Flush();

                isolatedProcess = StartIsolatedSolidWorks();
                if (isolatedProcess == null)
                    return Fail("Could not start a second SOLIDWORKS process.");

                isolated = WaitForSolidWorksCom(isolatedProcess.Id, 30000);
                if (isolated == null)
                    return Fail("Second SOLIDWORKS process started, but its COM object did not become available.");

                isolated.Visible = false;
                Console.WriteLine("PROGRESS|INSTANCE|READY|PID=" + isolatedProcess.Id);
                Console.Out.Flush();

                int openErrors = 0;
                int openWarnings = 0;
                int openOptions =
                    (int)swOpenDocOptions_e.swOpenDocOptions_Silent |
                    (int)swOpenDocOptions_e.swOpenDocOptions_ReadOnly;

                isolatedModel = isolated.OpenDoc6(
                    assemblyPath,
                    (int)swDocumentTypes_e.swDocASSEMBLY,
                    openOptions,
                    configuration ?? string.Empty,
                    ref openErrors,
                    ref openWarnings) as IModelDoc2;

                if (isolatedModel == null)
                    return Fail("Could not open Assembly in background SOLIDWORKS. Errors=" + openErrors + ", Warnings=" + openWarnings + ".");

                var parameters = new Dictionary<string, object>
                {
                    ["Mode"] = "LegacyFlat",
                    ["RespectChildDisplay"] = true,
                    ["ExportExcel"] = true,
                    ["ExportCsv"] = false,
                    ["CaptureImages"] = true,
                    ["ImageCaptureMode"] = "Render"
                };
                if (!string.IsNullOrWhiteSpace(outputFolder))
                    parameters["OutputFolder"] = outputFolder;

                Console.WriteLine("PROGRESS|BOM|RUNNING");
                Console.Out.Flush();

                var clock = Stopwatch.StartNew();
                ToolResult result = new CreateBomTool(isolated).Execute(parameters);
                clock.Stop();

                if (!result.IsSuccess)
                    return Fail(result.ErrorMessage ?? "BOM export failed.");

                var bom = result.Data as BomResult;
                if (bom == null)
                    return Fail("BOM export returned no result data.");

                Console.WriteLine(
                    "RESULT|OK|" + Escape(bom.ExcelPath) +
                    "|ITEMS=" + bom.Items.Count +
                    "|IMAGES=" + bom.CapturedImageCount +
                    "|MS=" + clock.ElapsedMilliseconds +
                    "|PID=" + isolatedProcess.Id);
                return 0;
            }
            catch (Exception ex)
            {
                return Fail(ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                if (isolated != null)
                {
                    try
                    {
                        if (isolatedModel != null)
                            isolated.CloseDoc(isolatedModel.GetTitle());
                    }
                    catch { }
                    try { isolated.ExitApp(); } catch { }
                }

                ReleaseCom(isolatedModel);
                ReleaseCom(isolated);

                if (isolatedProcess != null)
                {
                    try
                    {
                        if (!isolatedProcess.WaitForExit(5000) && !isolatedProcess.HasExited)
                            isolatedProcess.Kill();
                    }
                    catch { }
                    try { isolatedProcess.Dispose(); } catch { }
                }
            }
        }

        private static Process StartIsolatedSolidWorks()
        {
            var before = new HashSet<int>(
                Process.GetProcessesByName("SLDWORKS").Select(process => process.Id));

            string executable = ResolveSolidWorksExecutable();
            if (string.IsNullOrWhiteSpace(executable) || !File.Exists(executable))
                return null;

            Process.Start(new ProcessStartInfo
            {
                FileName = executable,
                Arguments = "/b",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });

            var clock = Stopwatch.StartNew();
            while (clock.ElapsedMilliseconds < 30000)
            {
                Process candidate = Process.GetProcessesByName("SLDWORKS")
                    .Where(process => !before.Contains(process.Id))
                    .OrderByDescending(process => SafeStartTime(process))
                    .FirstOrDefault();
                if (candidate != null)
                    return candidate;
                Thread.Sleep(250);
            }

            return null;
        }

        private static DateTime SafeStartTime(Process process)
        {
            try { return process.StartTime; }
            catch { return DateTime.MinValue; }
        }

        private static string ResolveSolidWorksExecutable()
        {
            try
            {
                Process existing = Process.GetProcessesByName("SLDWORKS").FirstOrDefault();
                string path = existing?.MainModule?.FileName;
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                    return path;
            }
            catch { }

            string fallback = Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles),
                "SOLIDWORKS Corp",
                "SOLIDWORKS",
                "SLDWORKS.exe");
            return fallback;
        }

        private static ISldWorks WaitForSolidWorksCom(int processId, int timeoutMilliseconds)
        {
            string monikerName = "SolidWorks_PID_" + processId;
            var clock = Stopwatch.StartNew();
            while (clock.ElapsedMilliseconds < timeoutMilliseconds)
            {
                object value = TryGetRotObject(monikerName);
                if (value is ISldWorks app)
                    return app;
                ReleaseCom(value);
                Thread.Sleep(250);
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
                if (GetRunningObjectTable(0, out rot) != 0 || rot == null)
                    return null;

                rot.EnumRunning(out enumerator);
                if (enumerator == null) return null;

                var monikers = new IMoniker[1];
                while (enumerator.Next(1, monikers, IntPtr.Zero) == 0)
                {
                    IMoniker moniker = monikers[0];
                    try
                    {
                        if (CreateBindCtx(0, out bindContext) != 0 || bindContext == null)
                            continue;

                        string displayName = string.Empty;
                        try { moniker.GetDisplayName(bindContext, null, out displayName); }
                        catch { displayName = string.Empty; }

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

        private static string ParseArgument(string[] args, string key)
        {
            if (args == null) return string.Empty;
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1]?.Trim() ?? string.Empty;
            return string.Empty;
        }

        private static int Fail(string message)
        {
            Console.WriteLine("RESULT|ERROR|" + Escape(message));
            return 1;
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty)
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Replace("|", "/");
        }

        private static void ReleaseCom(object value)
        {
            if (value == null || !Marshal.IsComObject(value)) return;
            try { Marshal.FinalReleaseComObject(value); } catch { }
        }

        [DllImport("ole32.dll")]
        private static extern int GetRunningObjectTable(
            int reserved,
            out IRunningObjectTable runningObjectTable);

        [DllImport("ole32.dll")]
        private static extern int CreateBindCtx(
            int reserved,
            out IBindCtx bindContext);
    }
}
