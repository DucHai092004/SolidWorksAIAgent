using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SwMateAI.Core.Agent;
using SwMateAI.Core.Manufacturing;
using SwMateAI.Core.Tools;

namespace SwMateAI.P2Stock.IntegrationRunner
{
    internal static class Program
    {
        private const int UniquePartCount = 10;

        private static int Main(string[] args)
        {
            string workerExe = args != null && args.Length > 0
                ? Path.GetFullPath(args[0])
                : string.Empty;
            if (string.IsNullOrWhiteSpace(workerExe) || !File.Exists(workerExe))
            {
                Console.WriteLine("[FAIL] Stock worker executable not found: " + workerExe);
                return 2;
            }

            string root = Path.Combine(
                Path.GetTempPath(),
                "SW_MATE_AI_P2_STOCK_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(root);
            Console.WriteLine("P2 Stock temp: " + root);

            StockWorkerJob completedJob = null;
            StockWorkerJob cancelJob = null;
            try
            {
                string assemblyPath = BuildFixture(root);
                if (string.IsNullOrWhiteSpace(assemblyPath) || !File.Exists(assemblyPath))
                    return Fail("P2 worker fixture Assembly was not created.");

                // TC028: Process.Start must return immediately; the expensive SOLIDWORKS work
                // happens in SwMateAI.StockWorker, not in the caller/UI process.
                string output1 = Path.Combine(root, "TC028_Stock.xlsx");
                completedJob = new StockWorkerJobBuilder().Create(assemblyPath, output1);
                Process worker1 = StartWorker(workerExe, completedJob.ManifestPath, out long startMs);
                if (worker1 == null) return Fail("TC028 worker could not start.");
                Console.WriteLine("Worker Process.Start elapsed=" + startMs + " ms");
                if (startMs > 3000)
                {
                    try { worker1.Kill(); } catch { }
                    return Fail("TC028 Process.Start blocked the caller for too long.");
                }

                Console.WriteLine("[PASS] TC028 caller remains responsive after launching background worker.");
                if (!worker1.WaitForExit(240000))
                {
                    try { worker1.Kill(); } catch { }
                    return Fail("TC028 worker timed out.");
                }

                StockWorkerManifest manifest1 = StockWorkerManifestSerializer.Read(completedJob.ManifestPath);
                bool tc028 = worker1.ExitCode == 0 &&
                             manifest1.Finished &&
                             !manifest1.Cancelled &&
                             string.IsNullOrWhiteSpace(manifest1.FatalError) &&
                             manifest1.TotalItems == UniquePartCount &&
                             manifest1.ProcessedItems == UniquePartCount &&
                             manifest1.OutputRows == UniquePartCount &&
                             File.Exists(output1) && new FileInfo(output1).Length > 0;
                worker1.Dispose();

                Console.WriteLine(
                    "TC028 Total=" + manifest1.TotalItems +
                    " Processed=" + manifest1.ProcessedItems +
                    " Images=" + manifest1.CapturedImageCount +
                    " Rows=" + manifest1.OutputRows +
                    " Status=" + manifest1.StatusMessage);
                if (!tc028) return Fail("TC028 background Stock Excel worker did not complete safely.");
                Console.WriteLine("[PASS] TC028 background worker created Stock Excel without blocking caller/UI thread.");

                // TC032: start a second job, wait until the worker reaches the image loop,
                // then signal cancellation through the job's cancel.flag.
                string output2 = Path.Combine(root, "TC032_Cancelled.xlsx");
                cancelJob = new StockWorkerJobBuilder().Create(assemblyPath, output2);
                Process worker2 = StartWorker(workerExe, cancelJob.ManifestPath, out _);
                if (worker2 == null) return Fail("TC032 worker could not start.");

                bool reachedWork = WaitForImagePhase(cancelJob.ManifestPath, worker2, 120000);
                if (!reachedWork)
                {
                    try { worker2.Kill(); } catch { }
                    return Fail("TC032 worker never reached a cancellable processing phase.");
                }

                StockWorkerJobBuilder.RequestCancel(cancelJob);
                Console.WriteLine("TC032 cancel.flag created.");
                if (!worker2.WaitForExit(120000))
                {
                    try { worker2.Kill(); } catch { }
                    return Fail("TC032 worker did not stop after cancellation.");
                }

                StockWorkerManifest manifest2 = StockWorkerManifestSerializer.Read(cancelJob.ManifestPath);
                bool tc032 = worker2.ExitCode == 5 &&
                             manifest2.Finished &&
                             manifest2.Cancelled &&
                             string.IsNullOrWhiteSpace(manifest2.FatalError) &&
                             !File.Exists(output2);
                worker2.Dispose();

                Console.WriteLine(
                    "TC032 Exit=5? " + tc032 +
                    " Processed=" + manifest2.ProcessedItems + "/" + manifest2.TotalItems +
                    " Status=" + manifest2.StatusMessage);
                if (!tc032) return Fail("TC032 worker cancellation did not cleanly stop before workbook export.");
                Console.WriteLine("[PASS] TC032 cancellation stops worker cleanly without partial XLSX.");

                return 0;
            }
            catch (Exception ex)
            {
                return Fail("P2 Stock runner fatal :: " + ex);
            }
            finally
            {
                CleanupJob(completedJob);
                CleanupJob(cancelJob);
                try { Directory.Delete(root, true); } catch { }
            }
        }

        private static string BuildFixture(string root)
        {
            ISldWorks sw = null;
            string partTitle = string.Empty;
            string assemblyTitle = string.Empty;
            try
            {
                Type progId = Type.GetTypeFromProgID("SldWorks.Application", true);
                sw = (ISldWorks)Activator.CreateInstance(progId);
                sw.Visible = false;
                Thread.Sleep(1000);

                var agent = new AgentCore(sw);
                if (!Require(agent.ExecuteTool("CreatePart", null), "CreatePart")) return string.Empty;
                if (!Require(agent.ExecuteTool("CreateSketch", null), "CreateSketch")) return string.Empty;
                if (!Require(agent.ExecuteTool("CreateRectangle", new Dictionary<string, object>
                {
                    ["Width"] = 40d,
                    ["Height"] = 24d
                }), "CreateRectangle")) return string.Empty;
                if (!Require(agent.ExecuteTool("Extrude", new Dictionary<string, object>
                {
                    ["Depth"] = 8d
                }), "Extrude")) return string.Empty;

                var part = sw.ActiveDoc as IModelDoc2;
                string sourcePart = Path.Combine(root, "P2_Source.SLDPRT");
                if (!Save(part, sourcePart)) return string.Empty;
                partTitle = part?.GetTitle() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(partTitle))
                {
                    sw.CloseDoc(partTitle);
                    partTitle = string.Empty;
                }

                string[] partPaths = new string[UniquePartCount];
                for (int i = 0; i < UniquePartCount; i++)
                {
                    partPaths[i] = Path.Combine(root, "P2_Part_" + (i + 1).ToString("D2") + ".SLDPRT");
                    File.Copy(sourcePart, partPaths[i], true);
                }

                string assemblyTemplate = sw.GetUserPreferenceStringValue(
                    (int)swUserPreferenceStringValue_e.swDefaultTemplateAssembly);
                var assemblyModel = sw.NewDocument(assemblyTemplate, 0, 0, 0) as IModelDoc2;
                var assembly = assemblyModel as IAssemblyDoc;
                if (assemblyModel == null || assembly == null) return string.Empty;
                assemblyTitle = assemblyModel.GetTitle() ?? string.Empty;

                string[] coordinateSystems = new string[UniquePartCount];
                double[] transforms = new double[UniquePartCount * 16];
                for (int i = 0; i < UniquePartCount; i++)
                {
                    coordinateSystems[i] = string.Empty;
                    int o = i * 16;
                    transforms[o + 0] = 1.0;
                    transforms[o + 4] = 1.0;
                    transforms[o + 8] = 1.0;
                    transforms[o + 9] = (i % 5) * 0.06;
                    transforms[o + 10] = (i / 5) * 0.06;
                    transforms[o + 11] = 0.0;
                    transforms[o + 12] = 1.0;
                }

                object[] added = assembly.AddComponents3(partPaths, transforms, coordinateSystems) as object[];
                if (added == null || added.Length != UniquePartCount)
                    return string.Empty;

                string assemblyPath = Path.Combine(root, "P2_Stock_Test.SLDASM");
                if (!Save(assemblyModel, assemblyPath)) return string.Empty;
                return assemblyPath;
            }
            finally
            {
                if (sw != null)
                {
                    try { if (!string.IsNullOrWhiteSpace(assemblyTitle)) sw.CloseDoc(assemblyTitle); } catch { }
                    try { if (!string.IsNullOrWhiteSpace(partTitle)) sw.CloseDoc(partTitle); } catch { }
                    try { sw.ExitApp(); } catch { }
                    try { Marshal.FinalReleaseComObject(sw); } catch { }
                }
            }
        }

        private static Process StartWorker(string workerExe, string manifestPath, out long startMs)
        {
            var info = new ProcessStartInfo
            {
                FileName = workerExe,
                Arguments = Quote(manifestPath),
                WorkingDirectory = Path.GetDirectoryName(workerExe) ?? string.Empty,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            var watch = Stopwatch.StartNew();
            Process process = Process.Start(info);
            watch.Stop();
            startMs = watch.ElapsedMilliseconds;
            return process;
        }

        private static bool WaitForImagePhase(string manifestPath, Process worker, int timeoutMs)
        {
            var watch = Stopwatch.StartNew();
            while (watch.ElapsedMilliseconds < timeoutMs && worker != null && !worker.HasExited)
            {
                try
                {
                    StockWorkerManifest manifest = StockWorkerManifestSerializer.Read(manifestPath);
                    if (manifest.ProcessedItems > 0 ||
                        string.Equals(manifest.StatusMessage, "Capturing part images", StringComparison.OrdinalIgnoreCase) ||
                        (manifest.StatusMessage ?? string.Empty).StartsWith("Processed ", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                catch { }
                Thread.Sleep(100);
            }
            return false;
        }

        private static bool Require(ToolResult result, string name)
        {
            bool ok = result != null && result.IsSuccess;
            Console.WriteLine((ok ? "[PASS] " : "[FAIL] ") + name +
                (ok ? string.Empty : " :: " + (result?.ErrorMessage ?? "No result")));
            return ok;
        }

        private static bool Save(IModelDoc2 model, string path)
        {
            int errors = 0, warnings = 0;
            bool ok = model != null && model.Extension.SaveAs(
                path,
                (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                (int)swSaveAsOptions_e.swSaveAsOptions_Silent,
                null,
                ref errors,
                ref warnings);
            Console.WriteLine((ok && errors == 0 ? "[PASS] " : "[FAIL] ") +
                "Save " + Path.GetFileName(path) +
                " Errors=" + errors + " Warnings=" + warnings);
            return ok && errors == 0;
        }

        private static void CleanupJob(StockWorkerJob job)
        {
            if (job == null || string.IsNullOrWhiteSpace(job.JobDirectory)) return;
            try { if (Directory.Exists(job.JobDirectory)) Directory.Delete(job.JobDirectory, true); } catch { }
        }

        private static string Quote(string value)
            => "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";

        private static int Fail(string message)
        {
            Console.WriteLine("[FAIL] " + message);
            return 1;
        }
    }
}
