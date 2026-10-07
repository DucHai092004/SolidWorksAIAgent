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
        private static int Main()
        {
            const int occurrenceCount = 510;
            string root = Path.Combine(
                Path.GetTempPath(),
                "SW_MATE_AI_P1_STRESS_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(root);
            Console.WriteLine("Stress temp: " + root);

            ISldWorks sw = null;
            string partTitle = string.Empty;
            string assemblyTitle = string.Empty;

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

                var insertWatch = Stopwatch.StartNew();
                int inserted = 0;
                for (int i = 0; i < occurrenceCount; i++)
                {
                    double x = (i % 30) * 0.03;
                    double y = (i / 30) * 0.03;
                    var component = assembly.AddComponent4(partPath, string.Empty, x, y, 0) as IComponent2;
                    if (component != null) inserted++;

                    if ((i + 1) % 100 == 0)
                        Console.WriteLine("Inserted progress=" + (i + 1) + "/" + occurrenceCount);
                }
                insertWatch.Stop();
                Console.WriteLine("Inserted=" + inserted + " in " + insertWatch.ElapsedMilliseconds + " ms");
                if (inserted != occurrenceCount)
                {
                    Console.WriteLine("[FAIL] TC012 fixture insertion: " + inserted + "/" + occurrenceCount);
                    return 1;
                }
                Console.WriteLine("[PASS] TC012 fixture has " + occurrenceCount + " component occurrences");

                string assemblyPath = Path.Combine(root, "TC012_StressAssembly.SLDASM");
                if (!Save(assemblyModel, assemblyPath)) return 1;

                var exportWatch = Stopwatch.StartNew();
                ToolResult export = agent.ExecuteTool("CreateBOM", new Dictionary<string, object>
                {
                    ["Mode"] = "Indented",
                    ["RespectChildDisplay"] = true,
                    ["IncludeHidden"] = true,
                    ["ExportExcel"] = true,
                    ["ExportCsv"] = false,
                    ["OutputFolder"] = root
                });
                exportWatch.Stop();

                var bom = export.Data as BomResult;
                bool ok = export.IsSuccess && bom != null &&
                          bom.TotalOccurrences == occurrenceCount &&
                          bom.Items.Count == 1 &&
                          bom.Items[0].Quantity == occurrenceCount &&
                          bom.CapturedImageCount == 1 &&
                          File.Exists(bom.ExcelPath) &&
                          new FileInfo(bom.ExcelPath).Length > 0;

                Console.WriteLine("BOM export elapsed=" + exportWatch.ElapsedMilliseconds + " ms");
                Console.WriteLine(bom == null
                    ? "BomResult missing"
                    : "Occurrences=" + bom.TotalOccurrences +
                      " Rows=" + bom.Items.Count +
                      " Quantity=" + (bom.Items.Count == 0 ? 0 : bom.Items[0].Quantity) +
                      " Images=" + bom.CapturedImageCount +
                      " Warnings=" + bom.Warnings.Count);

                if (!ok)
                {
                    Console.WriteLine("[FAIL] TC012 >500-component BOM stress :: " +
                        (export.ErrorMessage ?? "Result mismatch"));
                    return 1;
                }

                Console.WriteLine("[PASS] TC012 >500-component BOM stress completed without abort/crash");
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

        private static ISldWorks CreateDedicatedSolidWorks()
        {
            Type type = Type.GetTypeFromProgID("SldWorks.Application", true);
            return (ISldWorks)Activator.CreateInstance(type);
        }
    }
}
