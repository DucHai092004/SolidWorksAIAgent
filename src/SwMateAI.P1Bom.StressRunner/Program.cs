using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SwMateAI.Core.Agent;
using SwMateAI.Core.BOM;
using SwMateAI.Core.Tools;

namespace SwMateAI.P1Bom.StressRunner
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            const int occurrenceCount = 510;

            string workerExe = args != null && args.Length > 0
                ? Path.GetFullPath(args[0])
                : string.Empty;
            if (string.IsNullOrWhiteSpace(workerExe) || !File.Exists(workerExe))
            {
                Console.WriteLine("[FAIL] Worker executable not found: " + workerExe);
                return 2;
            }

            string root = Path.Combine(
                Path.GetTempPath(),
                "SW_MATE_AI_P1_STRESS_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(root);
            Console.WriteLine("Stress temp: " + root);

            ISldWorks sw = null;
            string partTitle = string.Empty;
            string assemblyTitle = string.Empty;
            BomWorkerJob job = null;

            try
            {
                sw = CreateDedicatedSolidWorks();
                sw.Visible = false;
                Thread.Sleep(1500);
                Console.WriteLine("[PASS] Dedicated SOLIDWORKS automation session created.");

                var agent = new AgentCore(sw);

                string partPath = Path.Combine(root, "TC012_StressPart.SLDPRT");
                if (!Require(agent.ExecuteTool("CreatePart", null), "CreatePart")) return 1;
                if (!Require(agent.ExecuteTool("CreateSketch", null), "CreateSketch")) return 1;
                if (!Require(agent.ExecuteTool("CreateRectangle", new Dictionary<string, object>
                {
                    ["Width"] = 20d,
                    ["Height"] = 12d
                }), "CreateRectangle")) return 1;
                if (!Require(agent.ExecuteTool("Extrude", new Dictionary<string, object>
                {
                    ["Depth"] = 5d
                }), "Extrude")) return 1;

                var part = sw.ActiveDoc as IModelDoc2;
                if (!Save(part, partPath)) return 1;
                partTitle = part?.GetTitle() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(partTitle))
                {
                    sw.CloseDoc(partTitle);
                    partTitle = string.Empty;
                }

                string assemblyTemplate = sw.GetUserPreferenceStringValue(
                    (int)swUserPreferenceStringValue_e.swDefaultTemplateAssembly);
                var assemblyModel = sw.NewDocument(assemblyTemplate, 0, 0, 0) as IModelDoc2;
                var assembly = assemblyModel as IAssemblyDoc;
                if (assembly == null)
                {
                    Console.WriteLine("[FAIL] Could not create stress assembly.");
                    return 1;
                }
                assemblyTitle = assemblyModel.GetTitle();

                string[] names = new string[occurrenceCount];
                string[] coordinateSystems = new string[occurrenceCount];
                double[] transforms = new double[occurrenceCount * 16];
                for (int i = 0; i < occurrenceCount; i++)
                {
                    names[i] = partPath;
                    coordinateSystems[i] = string.Empty;

                    int offset = i * 16;
                    transforms[offset + 0] = 1.0;
                    transforms[offset + 4] = 1.0;
                    transforms[offset + 8] = 1.0;
                    transforms[offset + 9] = (i % 30) * 0.03;
                    transforms[offset + 10] = (i / 30) * 0.03;
                    transforms[offset + 11] = 0.0;
                    transforms[offset + 12] = 1.0;
                }

                var insertWatch = Stopwatch.StartNew();
                object[] added = assembly.AddComponents3(names, transforms, coordinateSystems) as object[];
                insertWatch.Stop();

                int inserted = added?.Length ?? 0;
                Console.WriteLine("Bulk inserted=" + inserted + " in " + insertWatch.ElapsedMilliseconds + " ms");
                if (inserted != occurrenceCount)
                {
                    Console.WriteLine("[FAIL] TC012 fixture insertion: " + inserted + "/" + occurrenceCount);
                    return 1;
                }
                Console.WriteLine("[PASS] TC012 fixture has " + occurrenceCount + " component occurrences");

                string assemblyPath = Path.Combine(root, "TC012_StressAssembly.SLDASM");
                if (!Save(assemblyModel, assemblyPath)) return 1;

                var bomWatch = Stopwatch.StartNew();
                ToolResult readResult = agent.ExecuteTool("CreateBOM", new Dictionary<string, object>
                {
                    ["Mode"] = "Indented",
                    ["RespectChildDisplay"] = true,
                    ["IncludeHidden"] = true,
                    ["ExportExcel"] = false,
                    ["ExportCsv"] = false
                });
                bomWatch.Stop();

                var bom = readResult.Data as BomResult;
                bool bomOk = readResult.IsSuccess && bom != null &&
                             bom.TotalOccurrences == occurrenceCount &&
                             bom.Items.Count == 1 &&
                             bom.Items[0].Quantity == occurrenceCount;

                Console.WriteLine("BOM read elapsed=" + bomWatch.ElapsedMilliseconds + " ms");
                Console.WriteLine(bom == null
                    ? "BomResult missing"
                    : "Occurrences=" + bom.TotalOccurrences +
                      " Rows=" + bom.Items.Count +
                      " Quantity=" + (bom.Items.Count == 0 ? 0 : bom.Items[0].Quantity) +
                      " Warnings=" + bom.Warnings.Count);

                if (!bomOk)
                {
                    Console.WriteLine("[FAIL] TC012 >500-component BOM read :: " +
                        (readResult.ErrorMessage ?? "Result mismatch"));
                    return 1;
                }
                Console.WriteLine("[PASS] TC012 BOM reader returned 510 occurrences as one grouped row.");

                if (!string.IsNullOrWhiteSpace(assemblyTitle))
                {
                    try { sw.CloseDoc(assemblyTitle); } catch { }
                    assemblyTitle = string.Empty;
                }
                try { sw.ExitApp(); } catch { }
                try { Marshal.FinalReleaseComObject(sw); } catch { }
                sw = null;

                var jobBuilder = new BomWorkerJobBuilder();
                job = jobBuilder.Create(bom, workerExe);
                var startInfo = new ProcessStartInfo
                {
                    FileName = workerExe,
                    Arguments = Quote(job.ManifestPath),
                    WorkingDirectory = Path.GetDirectoryName(workerExe) ?? string.Empty,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                var workerWatch = Stopwatch.StartNew();
                using (Process worker = Process.Start(startInfo))
                {
                    if (worker == null)
                    {
                        Console.WriteLine("[FAIL] TC012 BOM worker could not start.");
                        return 1;
                    }

                    if (!worker.WaitForExit(240000))
                    {
                        try { worker.Kill(); } catch { }
                        Console.WriteLine("[FAIL] TC012 BOM worker timed out.");
                        return 1;
                    }
                    workerWatch.Stop();

                    BomWorkerManifest manifest = BomWorkerManifestSerializer.Read(job.ManifestPath);
                    jobBuilder.Apply(bom, manifest);
                    bool workerOk = worker.ExitCode == 0 &&
                                    manifest.Finished &&
                                    string.IsNullOrWhiteSpace(manifest.FatalError) &&
                                    bom.CapturedImageCount == 1;
                    Console.WriteLine("Worker elapsed=" + workerWatch.ElapsedMilliseconds +
                                      " ms Images=" + bom.CapturedImageCount +
                                      " Exit=" + worker.ExitCode);
                    if (!workerOk)
                    {
                        Console.WriteLine("[FAIL] TC012 BOM worker did not complete safely: " +
                            (manifest.FatalError ?? string.Empty));
                        return 1;
                    }
                }

                string xlsxPath = Path.Combine(root, "TC012_Stress_BOM.xlsx");
                var excelWatch = Stopwatch.StartNew();
                new BomExcelExporter().Export(bom, xlsxPath);
                excelWatch.Stop();

                bool excelOk = File.Exists(xlsxPath) && new FileInfo(xlsxPath).Length > 0;
                Console.WriteLine("Excel elapsed=" + excelWatch.ElapsedMilliseconds +
                                  " ms Path=" + xlsxPath);
                if (!excelOk)
                {
                    Console.WriteLine("[FAIL] TC012 Excel output was not created.");
                    return 1;
                }

                Console.WriteLine("[PASS] TC012 >500-component production BOM pipeline completed without abort/crash.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[FAIL] TC012 fatal :: " + ex);
                return 1;
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

                if (job != null && !string.IsNullOrWhiteSpace(job.JobDirectory))
                {
                    try { if (Directory.Exists(job.JobDirectory)) Directory.Delete(job.JobDirectory, true); } catch { }
                }
            }
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

        private static string Quote(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }

        private static ISldWorks CreateDedicatedSolidWorks()
        {
            Type type = Type.GetTypeFromProgID("SldWorks.Application", true);
            return (ISldWorks)Activator.CreateInstance(type);
        }
    }
}
