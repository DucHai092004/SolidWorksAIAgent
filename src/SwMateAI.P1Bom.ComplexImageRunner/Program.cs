using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SwMateAI.Core.Agent;
using SwMateAI.Core.BOM;
using SwMateAI.Core.Tools;

namespace SwMateAI.P1Bom.ComplexImageRunner
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
                "SW_MATE_AI_TC018_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(root);
            Console.WriteLine("TC018 temp: " + root);

            ISldWorks sw = null;
            string originalTitle = string.Empty;
            string fixtureTitle = string.Empty;
            BomWorkerJob job = null;

            try
            {
                sw = ConnectSolidWorks();
                sw.Visible = true;
                originalTitle = (sw.ActiveDoc as IModelDoc2)?.GetTitle() ?? string.Empty;
                var agent = new AgentCore(sw);

                var holes = new List<CadHoleSpec>
                {
                    new CadHoleSpec { Diameter = 14, X = -40, Y = -22 },
                    new CadHoleSpec { Diameter = 14, X =  40, Y = -22 },
                    new CadHoleSpec { Diameter = 14, X = -40, Y =  22 },
                    new CadHoleSpec { Diameter = 14, X =  40, Y =  22 }
                };

                ToolResult plate = agent.ExecuteTool("CreatePlateWithHole", new Dictionary<string, object>
                {
                    ["Width"] = 120d,
                    ["Height"] = 80d,
                    ["Thickness"] = 20d,
                    ["HoleDepth"] = 20d,
                    ["Holes"] = holes
                });
                if (!Require(plate, "CreatePlateWithHole (4 holes)")) return 1;

                ToolResult fillet = agent.ExecuteTool("FilletPlateCorners", new Dictionary<string, object>
                {
                    ["Radius"] = 10d
                });
                if (!Require(fillet, "FilletPlateCorners R10")) return 1;

                var model = sw.ActiveDoc as IModelDoc2;
                var part = model as IPartDoc;
                if (model == null || part == null)
                    return Fail("TC018 complex fixture is not an active Part.");

                int nonPlanarFaces = CountNonPlanarFaces(part);
                Console.WriteLine("Non-planar faces=" + nonPlanarFaces);
                if (nonPlanarFaces < 8)
                    return Fail("TC018 fixture is not complex enough; expected at least 8 non-planar faces.");

                string partPath = Path.Combine(root, "TC018_MultiCurved_Part.SLDPRT");
                if (!Save(model, partPath)) return 1;
                fixtureTitle = model.GetTitle() ?? string.Empty;

                if (!string.IsNullOrWhiteSpace(fixtureTitle))
                {
                    sw.CloseDoc(fixtureTitle);
                    fixtureTitle = string.Empty;
                }

                var bom = new BomResult();
                bom.Items.Add(new BomItem
                {
                    ItemNumber = 1,
                    PartNumber = "TC018_MultiCurved_Part",
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
                    if (process == null) return Fail("TC018 worker process could not start.");
                    if (!process.WaitForExit(240000))
                    {
                        try { process.Kill(); } catch { }
                        return Fail("TC018 worker timed out.");
                    }
                    watch.Stop();

                    BomWorkerManifest manifest = BomWorkerManifestSerializer.Read(job.ManifestPath);
                    builder.Apply(bom, manifest);
                    BomWorkerItem workerItem = manifest.Items.SingleOrDefault();

                    bool imageOk = process.ExitCode == 0 &&
                                   manifest.Finished &&
                                   string.IsNullOrWhiteSpace(manifest.FatalError) &&
                                   workerItem != null &&
                                   workerItem.Completed &&
                                   File.Exists(workerItem.OutputImagePath) &&
                                   new FileInfo(workerItem.OutputImagePath).Length > 0 &&
                                   bom.CapturedImageCount == 1;

                    Console.WriteLine("Worker elapsed=" + watch.ElapsedMilliseconds + " ms");
                    Console.WriteLine("Exit=" + process.ExitCode +
                                      " Finished=" + manifest.Finished +
                                      " Completed=" + (workerItem?.Completed.ToString() ?? "<null>") +
                                      " Image=" + (workerItem?.OutputImagePath ?? "<null>") +
                                      " Error=" + (workerItem?.Error ?? manifest.FatalError));

                    if (!imageOk)
                        return Fail("TC018 worker did not create a valid isometric thumbnail for the multi-curved fixture.");
                }

                string xlsx = Path.Combine(root, "TC018_MultiCurved_Result.xlsx");
                new BomExcelExporter().Export(bom, xlsx);
                if (!File.Exists(xlsx) || new FileInfo(xlsx).Length == 0)
                    return Fail("TC018 thumbnail could not be embedded into Excel.");

                Console.WriteLine("[PASS] TC018 multi-curved mechanical part captured safely in isometric view and embedded into Excel.");
                return 0;
            }
            catch (Exception ex)
            {
                return Fail("TC018 fatal :: " + ex);
            }
            finally
            {
                if (sw != null)
                {
                    try { if (!string.IsNullOrWhiteSpace(fixtureTitle)) sw.CloseDoc(fixtureTitle); } catch { }
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

        private static int CountNonPlanarFaces(IPartDoc part)
        {
            int count = 0;
            object[] bodies = part.GetBodies2((int)swBodyType_e.swSolidBody, true) as object[];
            if (bodies == null) return 0;

            foreach (object bodyObject in bodies)
            {
                var body = bodyObject as IBody2;
                object[] faces = body?.GetFaces() as object[];
                if (faces == null) continue;

                foreach (object faceObject in faces)
                {
                    var face = faceObject as IFace2;
                    var surface = face?.GetSurface() as ISurface;
                    if (surface != null && !surface.IsPlane()) count++;
                }
            }

            return count;
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

        private static ISldWorks ConnectSolidWorks()
        {
            try { return (ISldWorks)Marshal.GetActiveObject("SldWorks.Application"); }
            catch
            {
                Type type = Type.GetTypeFromProgID("SldWorks.Application", true);
                return (ISldWorks)Activator.CreateInstance(type);
            }
        }

        private static int Fail(string message)
        {
            Console.WriteLine("[FAIL] " + message);
            return 1;
        }
    }
}
