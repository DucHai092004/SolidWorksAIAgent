using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SwMateAI.Core.Agent;
using SwMateAI.Core.BOM;
using SwMateAI.Core.Tools;

namespace SwMateAI.P1Bom.HierarchyRunner
{
    internal static class Program
    {
        private static readonly List<string> FixturePaths = new List<string>();

        private static int Main()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "SW_MATE_AI_TC013_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(root);
            Console.WriteLine("TC013 temp: " + root);

            ISldWorks sw = null;
            string originalTitle = string.Empty;

            try
            {
                sw = ConnectSolidWorks();
                sw.Visible = true;
                originalTitle = (sw.ActiveDoc as IModelDoc2)?.GetTitle() ?? string.Empty;
                var agent = new AgentCore(sw);

                string childA = Path.Combine(root, "TC013_Child_A.SLDPRT");
                string childB = Path.Combine(root, "TC013_Child_B.SLDPRT");
                string subAssembly = Path.Combine(root, "TC013_SubAssembly.SLDASM");
                string topAssembly = Path.Combine(root, "TC013_TopAssembly.SLDASM");
                FixturePaths.AddRange(new[] { childA, childB, subAssembly, topAssembly });

                if (!BuildPart(agent, sw, childA)) return Fail("Could not build TC013 child Part A.");
                File.Copy(childA, childB, true);

                var sub = CreateAssembly(sw, new[] { childA, childB }, subAssembly);
                if (sub == null) return Fail("Could not build TC013 sub-assembly.");

                var top = CreateAssembly(sw, new[] { subAssembly }, topAssembly);
                if (top == null) return Fail("Could not build TC013 top assembly.");

                int activateErrors = 0;
                sw.ActivateDoc3(
                    top.GetTitle(),
                    false,
                    (int)swRebuildOnActivation_e.swDontRebuildActiveDoc,
                    ref activateErrors);
                if (activateErrors != 0)
                    return Fail("Could not activate TC013 top assembly. Errors=" + activateErrors);

                ToolResult result = agent.ExecuteTool("CreateBOM", new Dictionary<string, object>
                {
                    ["Mode"] = "Indented",
                    ["RespectChildDisplay"] = true,
                    ["IncludeHidden"] = true,
                    ["ExportExcel"] = false,
                    ["ExportCsv"] = false,
                    ["OutputFolder"] = root
                });

                var bom = result?.Data as BomResult;
                if (result == null || !result.IsSuccess || bom == null)
                    return Fail("CreateBOM failed: " + (result?.ErrorMessage ?? "No BomResult"));

                int level0Assemblies = bom.Items.Count(x =>
                    x.Level == 0 &&
                    string.Equals(x.ComponentType, "Assembly", StringComparison.OrdinalIgnoreCase));
                int level1Parts = bom.Items.Count(x =>
                    x.Level == 1 &&
                    string.Equals(x.ComponentType, "Part", StringComparison.OrdinalIgnoreCase));
                bool uniqueChildren = bom.Items
                    .Where(x => x.Level == 1 && string.Equals(x.ComponentType, "Part", StringComparison.OrdinalIgnoreCase))
                    .Select(x => x.PartNumber)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count() == 2;

                Console.WriteLine(
                    "Items=" + bom.Items.Count +
                    " Occurrences=" + bom.TotalOccurrences +
                    " L0Assembly=" + level0Assemblies +
                    " L1Parts=" + level1Parts);
                foreach (BomItem item in bom.Items)
                {
                    Console.WriteLine(
                        "  Level=" + item.Level +
                        " Type=" + item.ComponentType +
                        " Part=" + item.PartNumber +
                        " Qty=" + item.Quantity);
                }

                bool ok = bom.Items.Count == 3 &&
                          bom.TotalOccurrences == 3 &&
                          level0Assemblies == 1 &&
                          level1Parts == 2 &&
                          uniqueChildren &&
                          bom.Items.All(x => x.Quantity == 1);

                if (!ok)
                    return Fail("TC013 hierarchy mismatch.");

                Console.WriteLine("[PASS] TC013 multi-level sub-assembly BOM preserves hierarchy.");
                return 0;
            }
            catch (Exception ex)
            {
                return Fail("TC013 fatal :: " + ex);
            }
            finally
            {
                if (sw != null)
                {
                    foreach (string path in FixturePaths.AsEnumerable().Reverse())
                    {
                        try
                        {
                            var model = sw.GetOpenDocumentByName(path) as IModelDoc2;
                            if (model != null) sw.CloseDoc(model.GetTitle());
                        }
                        catch { }
                    }

                    if (!string.IsNullOrWhiteSpace(originalTitle))
                    {
                        int errors = 0;
                        try { sw.ActivateDoc3(originalTitle, false, 0, ref errors); } catch { }
                    }
                }
            }
        }

        private static bool BuildPart(AgentCore agent, ISldWorks sw, string path)
        {
            if (!Require(agent.ExecuteTool("CreatePart", null), "CreatePart")) return false;
            if (!Require(agent.ExecuteTool("CreateSketch", null), "CreateSketch")) return false;
            if (!Require(agent.ExecuteTool("CreateRectangle", new Dictionary<string, object>
            {
                ["Width"] = 32d,
                ["Height"] = 20d
            }), "CreateRectangle")) return false;
            if (!Require(agent.ExecuteTool("Extrude", new Dictionary<string, object>
            {
                ["Depth"] = 8d
            }), "Extrude")) return false;

            var model = sw.ActiveDoc as IModelDoc2;
            return Save(model, path);
        }

        private static IModelDoc2 CreateAssembly(ISldWorks sw, string[] componentPaths, string assemblyPath)
        {
            string template = sw.GetUserPreferenceStringValue(
                (int)swUserPreferenceStringValue_e.swDefaultTemplateAssembly);
            var model = sw.NewDocument(template, 0, 0, 0) as IModelDoc2;
            var assembly = model as IAssemblyDoc;
            if (model == null || assembly == null)
            {
                Console.WriteLine("[FAIL] New assembly could not be created.");
                return null;
            }

            string assemblyTitle = model.GetTitle();
            int inserted = 0;

            for (int i = 0; i < componentPaths.Length; i++)
            {
                string path = componentPaths[i];
                int docType = path.EndsWith(".SLDASM", StringComparison.OrdinalIgnoreCase)
                    ? (int)swDocumentTypes_e.swDocASSEMBLY
                    : (int)swDocumentTypes_e.swDocPART;
                int openErrors = 0, openWarnings = 0;
                var loaded = sw.OpenDoc6(
                    path,
                    docType,
                    (int)swOpenDocOptions_e.swOpenDocOptions_Silent,
                    string.Empty,
                    ref openErrors,
                    ref openWarnings) as IModelDoc2;
                if (loaded == null)
                {
                    Console.WriteLine("[FAIL] Open component: " + path + " Errors=" + openErrors);
                    return null;
                }

                int activateErrors = 0;
                sw.ActivateDoc3(
                    assemblyTitle,
                    false,
                    (int)swRebuildOnActivation_e.swDontRebuildActiveDoc,
                    ref activateErrors);
                if (activateErrors != 0)
                {
                    Console.WriteLine("[FAIL] Reactivate assembly: " + assemblyTitle + " Errors=" + activateErrors);
                    return null;
                }

                var component = assembly.AddComponent4(path, string.Empty, i * 0.06, 0, 0) as IComponent2;
                if (component == null)
                {
                    Console.WriteLine("[FAIL] Add component: " + path);
                    return null;
                }
                inserted++;
            }

            if (inserted != componentPaths.Length)
            {
                Console.WriteLine("[FAIL] Inserted=" + inserted + "/" + componentPaths.Length);
                return null;
            }

            model.ForceRebuild3(false);
            return Save(model, assemblyPath) ? model : null;
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
