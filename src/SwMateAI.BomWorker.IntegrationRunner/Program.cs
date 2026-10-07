using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SwMateAI.Core.Agent;
using SwMateAI.Core.BOM;
using SwMateAI.Core.Tools;

namespace SwMateAI.BomWorker.IntegrationRunner
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            string workerExe = args != null && args.Length > 0 ? Path.GetFullPath(args[0]) : string.Empty;
            if (string.IsNullOrWhiteSpace(workerExe) || !File.Exists(workerExe))
            {
                Console.WriteLine("[FAIL] Worker executable not found: " + workerExe);
                return 2;
            }

            string root = Path.Combine(
                Path.GetTempPath(),
                "SW_MATE_AI_WORKER_TEST_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(root);

            ISldWorks sw = null;
            string originalTitle = string.Empty;
            string createdTitle = string.Empty;
            BomWorkerJob job = null;
            try
            {
                sw = ConnectSolidWorks();
                sw.Visible = true;
                originalTitle = (sw.ActiveDoc as IModelDoc2)?.GetTitle() ?? string.Empty;
                var agent = new AgentCore(sw);

                if (!Require(agent.ExecuteTool("CreatePart", null), "CreatePart")) return 1;
                if (!Require(agent.ExecuteTool("CreateSketch", null), "CreateSketch")) return 1;
                if (!Require(agent.ExecuteTool("CreateCircle", new Dictionary<string, object>
                {
                    ["Radius"] = 18d
                }), "CreateCircle")) return 1;
                if (!Require(agent.ExecuteTool("Extrude", new Dictionary<string, object>
                {
                    ["Depth"] = 25d
                }), "Extrude")) return 1;

                var model = sw.ActiveDoc as IModelDoc2;
                if (model == null)
                {
                    Console.WriteLine("[FAIL] Active Part missing.");
                    return 1;
                }
                createdTitle = model.GetTitle();
                string partPath = Path.Combine(root, "Worker_Curved_Part.SLDPRT");
                if (!Save(model, partPath)) return 1;

                sw.CloseDoc(createdTitle);
                createdTitle = string.Empty;

                var bom = new BomResult();
                bom.Items.Add(new BomItem
                {
                    ItemNumber = 1,
                    PartNumber = "Worker_Curved_Part",
                    Quantity = 1,
                    ComponentType = "Part",
                    Configuration = "Default",
                    SourcePath = partPath
                });

                var builder = new BomWorkerJobBuilder();
                job = builder.Create(bom, workerExe);

                var startInfo = new ProcessStartInfo
                {
                    FileName = workerExe,
                    Arguments = Quote(job.ManifestPath),
                    WorkingDirectory = Path.GetDirectoryName(workerExe) ?? string.Empty,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                var watch = Stopwatch.StartNew();
                using (Process process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        Console.WriteLine("[FAIL] Worker process could not start.");
                        return 1;
                    }

                    if (!process.WaitForExit(240000))
                    {
                        try { process.Kill(); } catch { }
                        Console.WriteLine("[FAIL] Worker timed out.");
                        return 1;
                    }
                    watch.Stop();

                    var manifest = BomWorkerManifestSerializer.Read(job.ManifestPath);
                    builder.Apply(bom, manifest);
                    bool ok = process.ExitCode == 0 &&
                              manifest.Finished &&
                              string.IsNullOrWhiteSpace(manifest.FatalError) &&
                              manifest.Items.Count == 1 &&
                              manifest.Items[0].Completed &&
                              File.Exists(manifest.Items[0].OutputImagePath) &&
                              new FileInfo(manifest.Items[0].OutputImagePath).Length > 0 &&
                              bom.CapturedImageCount == 1;

                    Console.WriteLine("Worker elapsed=" + watch.ElapsedMilliseconds + " ms");
                    Console.WriteLine("Exit=" + process.ExitCode +
                                      " Finished=" + manifest.Finished +
                                      " Completed=" + manifest.Items[0].Completed +
                                      " Image=" + manifest.Items[0].OutputImagePath +
                                      " Error=" + manifest.Items[0].Error);

                    if (!ok)
                    {
                        Console.WriteLine("[FAIL] Isolated BOM worker did not produce a valid thumbnail/checkpoint.");
                        return 1;
                    }
                }

                string xlsx = Path.Combine(root, "Worker_Result.xlsx");
                new BomExcelExporter().Export(bom, xlsx);
                bool workbookOk = File.Exists(xlsx) && new FileInfo(xlsx).Length > 0;
                Console.WriteLine(workbookOk
                    ? "[PASS] Isolated BOM worker thumbnail was embedded into Excel."
                    : "[FAIL] Result workbook was not created.");
                return workbookOk ? 0 : 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[FAIL] Worker integration fatal :: " + ex);
                return 1;
            }
            finally
            {
                if (sw != null)
                {
                    try { if (!string.IsNullOrWhiteSpace(createdTitle)) sw.CloseDoc(createdTitle); } catch { }
                    if (!string.IsNullOrWhiteSpace(originalTitle))
                    {
                        int errors = 0;
                        try { sw.ActivateDoc3(originalTitle, false, 0, ref errors); } catch { }
                    }
                }

                if (job != null)
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
                "Save worker fixture Errors=" + errors + " Warnings=" + warnings);
            return ok && errors == 0;
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }

        private static ISldWorks ConnectSolidWorks()
        {
            try { return (ISldWorks)Marshal.GetActiveObject("SldWorks.Application"); }
            catch
            {
                Type type = Type.GetTypeFromProgID("SldWorks.Application", true);
                return (ISldWorks)Activator.CreateInstance(type);
            }
        }
    }
}
