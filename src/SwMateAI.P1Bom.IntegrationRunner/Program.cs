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

namespace SwMateAI.P1Bom.IntegrationRunner
{
    internal static class Program
    {
        private static int _passed;
        private static int _failed;
        private static int _skipped;
        private static readonly List<string> CreatedTitles = new List<string>();

        private static int Main()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "SW_MATE_AI_P1_BOM_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(root);
            Console.WriteLine("P1 temp: " + root);

            ISldWorks sw = null;
            string originalTitle = string.Empty;
            try
            {
                sw = ConnectSolidWorks();
                sw.Visible = true;
                var original = sw.ActiveDoc as IModelDoc2;
                originalTitle = original?.GetTitle() ?? string.Empty;
                var agent = new AgentCore(sw);

                string sourcePart = Path.Combine(root, "P1_Source.SLDPRT");
                BuildSourcePart(agent, sw, sourcePart);

                string[] partPaths = BuildPartCopies(sourcePart, root);
                string assemblyPath = Path.Combine(root, "P1_BOM_Test.SLDASM");
                var assemblyModel = CreateAssembly(sw, partPaths, assemblyPath, out List<IComponent2> components);
                Check("TC011 fixture has 10 unique components", components.Count == 10,
                    "Components=" + components.Count);

                ToolResult firstExport = RunBom(agent, root, includeHidden: true, exportExcel: true);
                var firstBom = firstExport.Data as BomResult;
                Check("TC011 BOM Excel contains 10 rows",
                    firstExport.IsSuccess && firstBom != null && firstBom.Items.Count == 10,
                    Describe(firstExport, firstBom));
                Check("TC011 thumbnail captured for every BOM row",
                    firstBom != null && firstBom.CapturedImageCount == firstBom.Items.Count && firstBom.Items.Count == 10,
                    Describe(firstExport, firstBom));
                Check("TC011 Excel workbook exists",
                    firstBom != null && File.Exists(firstBom.ExcelPath) && new FileInfo(firstBom.ExcelPath).Length > 0,
                    firstBom?.ExcelPath ?? "No Excel path");

                Check("TC014 Vietnamese/special filename preserved",
                    firstBom != null && firstBom.Items.Any(x =>
                        (x.PartNumber ?? string.Empty).IndexOf("Trục_Đỡ_Ø25", StringComparison.OrdinalIgnoreCase) >= 0),
                    firstBom == null ? "No BOM" : string.Join(" | ", firstBom.Items.Select(x => x.PartNumber)));
                Check("TC020 empty Custom Properties do not abort export",
                    firstExport.IsSuccess && firstBom != null,
                    Describe(firstExport, firstBom));

                ToolResult secondExport = RunBom(agent, root, includeHidden: true, exportExcel: true);
                var secondBom = secondExport.Data as BomResult;
                Check("TC021 existing BOM gets unique suffix instead of overwrite",
                    secondExport.IsSuccess && firstBom != null && secondBom != null &&
                    !string.Equals(firstBom.ExcelPath, secondBom.ExcelPath, StringComparison.OrdinalIgnoreCase) &&
                    File.Exists(firstBom.ExcelPath) && File.Exists(secondBom.ExcelPath),
                    "First=" + firstBom?.ExcelPath + " Second=" + secondBom?.ExcelPath);

                if (components.Count > 0)
                {
                    components[0].Visible = (int)swComponentVisibilityState_e.swComponentHidden;
                    assemblyModel.ForceRebuild3(false);
                    ToolResult excludeHidden = RunBom(agent, root, includeHidden: false, exportExcel: false);
                    var hiddenExcludedBom = excludeHidden.Data as BomResult;
                    Check("TC015 hidden component can be excluded by configuration",
                        excludeHidden.IsSuccess && hiddenExcludedBom != null &&
                        hiddenExcludedBom.HiddenSkipped >= 1 && hiddenExcludedBom.Items.Count == 9,
                        Describe(excludeHidden, hiddenExcludedBom));

                    ToolResult includeHidden = RunBom(agent, root, includeHidden: true, exportExcel: false);
                    var hiddenIncludedBom = includeHidden.Data as BomResult;
                    Check("TC015 hidden component can remain in BOM when enabled",
                        includeHidden.IsSuccess && hiddenIncludedBom != null &&
                        hiddenIncludedBom.HiddenSkipped == 0 && hiddenIncludedBom.Items.Count == 10,
                        Describe(includeHidden, hiddenIncludedBom));
                    components[0].Visible = (int)swComponentVisibilityState_e.swComponentVisible;
                }

                if (components.Count > 1)
                {
                    int suppressStatus = components[1].SetSuppression2(
                        (int)swComponentSuppressionState_e.swComponentSuppressed);
                    assemblyModel.ForceRebuild3(false);
                    ToolResult suppressed = RunBom(agent, root, includeHidden: true, exportExcel: false);
                    var suppressedBom = suppressed.Data as BomResult;
                    Check("TC016 suppressed component is skipped safely",
                        suppressStatus == (int)swSuppressionError_e.swSuppressionChangeOk &&
                        components[1].GetSuppression() == (int)swComponentSuppressionState_e.swComponentSuppressed &&
                        suppressed.IsSuccess && suppressedBom != null &&
                        suppressedBom.SuppressedSkipped >= 1 && suppressedBom.Items.Count == 9,
                        "SetStatus=" + suppressStatus + " State=" + components[1].GetSuppression() +
                        " :: " + Describe(suppressed, suppressedBom));

                    components[1].SetSuppression2(
                        (int)swComponentSuppressionState_e.swComponentFullyResolved);
                    assemblyModel.ForceRebuild3(false);
                }

                Save(assemblyModel, assemblyPath);
                CloseTracked(sw, assemblyModel.GetTitle());
                foreach (string partPath in partPaths)
                    CloseByPathOrTitle(sw, partPath);

                string missingPath = partPaths[2];
                File.Delete(missingPath);
                int openErrors = 0, openWarnings = 0;
                var reopened = sw.OpenDoc6(
                    assemblyPath,
                    (int)swDocumentTypes_e.swDocASSEMBLY,
                    (int)swOpenDocOptions_e.swOpenDocOptions_Silent,
                    string.Empty,
                    ref openErrors,
                    ref openWarnings) as IModelDoc2;
                if (reopened != null) Track(reopened);

                ToolResult missing = RunBom(agent, root, includeHidden: true, exportExcel: false);
                var missingBom = missing.Data as BomResult;
                Check("TC017 missing linked component is skipped with warning",
                    reopened != null && missing.IsSuccess && missingBom != null &&
                    missingBom.MissingSkipped >= 1 && missingBom.Warnings.Count >= 1,
                    "OpenErrors=" + openErrors + " OpenWarnings=" + openWarnings +
                    " :: " + Describe(missing, missingBom));

                // TC013 hierarchy is covered by SwMateAI.BomHierarchy.IntegrationRunner.
                // TC012 full 500+ component image stress, TC018 complex freeform geometry and
                // TC019 Toolbox extraction require dedicated real-world fixtures/environments.
                Skip("TC012 full 500+ component SolidWorks image stress", "OpenXML 600-row layer is covered by Core tests; full CAD fixture not synthesized here.");
                Skip("TC018 complex freeform image capture", "Requires representative complex/freeform CAD fixture.");
                Skip("TC019 SolidWorks Toolbox metadata", "Requires Toolbox installation and standard hardware fixture.");

                if (string.IsNullOrWhiteSpace(originalTitle))
                {
                    if (reopened != null) CloseTracked(sw, reopened.GetTitle());
                    ToolResult noActive = agent.ExecuteTool("CreateBOM", new Dictionary<string, object>());
                    Check("TC022 no active CAD returns a safe tool error",
                        !noActive.IsSuccess,
                        noActive.ErrorMessage ?? Convert.ToString(noActive.Data));
                }
                else
                {
                    Skip("TC022 no active CAD", "An existing user document was active; runner will not close user work to manufacture this state.");
                }
            }
            catch (Exception ex)
            {
                _failed++;
                Console.WriteLine("[FATAL] " + ex);
            }
            finally
            {
                if (sw != null)
                {
                    foreach (string title in CreatedTitles.AsEnumerable().Reverse())
                    {
                        try { sw.CloseDoc(title); } catch { }
                    }
                    if (!string.IsNullOrWhiteSpace(originalTitle))
                    {
                        int errors = 0;
                        try { sw.ActivateDoc3(originalTitle, false, 0, ref errors); } catch { }
                    }
                }
            }

            Console.WriteLine("RESULT: PASS=" + _passed + " FAIL=" + _failed + " SKIP=" + _skipped);
            return _failed == 0 ? 0 : 1;
        }

        private static void BuildSourcePart(AgentCore agent, ISldWorks sw, string sourcePart)
        {
            Require(agent, "CreatePart");
            Require(agent, "CreateSketch");
            Require(agent, "CreateRectangle", new Dictionary<string, object>
            {
                ["Width"] = 45d,
                ["Height"] = 30d
            });
            Require(agent, "Extrude", new Dictionary<string, object> { ["Depth"] = 12d });
            var model = sw.ActiveDoc as IModelDoc2;
            Check("P1 source Part created", model != null && model.GetType() == (int)swDocumentTypes_e.swDocPART,
                "Active document is not a Part");
            Save(model, sourcePart);
            string title = model?.GetTitle() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(title)) sw.CloseDoc(title);
        }

        private static string[] BuildPartCopies(string sourcePart, string root)
        {
            string[] names =
            {
                "P1_Part_01.SLDPRT",
                "Trục_Đỡ_Ø25.SLDPRT",
                "P1_Part_03.SLDPRT",
                "P1 Part #04 (Rev_02).SLDPRT",
                "P1_Part_05.SLDPRT",
                "P1_Part_06.SLDPRT",
                "P1_Part_07.SLDPRT",
                "P1_Part_08.SLDPRT",
                "P1_Part_09.SLDPRT",
                "P1_Part_10.SLDPRT"
            };
            var paths = new string[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                paths[i] = Path.Combine(root, names[i]);
                File.Copy(sourcePart, paths[i], true);
            }
            return paths;
        }

        private static IModelDoc2 CreateAssembly(
            ISldWorks sw,
            string[] partPaths,
            string assemblyPath,
            out List<IComponent2> components)
        {
            components = new List<IComponent2>();
            string template = sw.GetUserPreferenceStringValue(
                (int)swUserPreferenceStringValue_e.swDefaultTemplateAssembly);
            var model = sw.NewDocument(template, 0, 0, 0) as IModelDoc2;
            var assembly = model as IAssemblyDoc;
            Check("P1 assembly created", assembly != null, "Could not create Assembly");
            Track(model);

            for (int i = 0; i < partPaths.Length; i++)
            {
                var component = assembly?.AddComponent4(
                    partPaths[i], string.Empty, i * 0.08, 0, 0) as IComponent2;
                if (component != null) components.Add(component);
            }
            Save(model, assemblyPath);
            return model;
        }

        private static ToolResult RunBom(AgentCore agent, string root, bool includeHidden, bool exportExcel)
        {
            return agent.ExecuteTool("CreateBOM", new Dictionary<string, object>
            {
                ["Mode"] = "Indented",
                ["RespectChildDisplay"] = true,
                ["IncludeHidden"] = includeHidden,
                ["ExportExcel"] = exportExcel,
                ["ExportCsv"] = false,
                ["OutputFolder"] = root
            });
        }

        private static string Describe(ToolResult result, BomResult bom)
        {
            if (result == null) return "No ToolResult";
            if (!result.IsSuccess) return result.ErrorMessage ?? "Tool failed";
            if (bom == null) return "No BomResult";
            return "Items=" + bom.Items.Count +
                   " Images=" + bom.CapturedImageCount +
                   " Suppressed=" + bom.SuppressedSkipped +
                   " Hidden=" + bom.HiddenSkipped +
                   " Missing=" + bom.MissingSkipped +
                   " Warnings=" + bom.Warnings.Count;
        }

        private static ISldWorks ConnectSolidWorks()
        {
            try { return (ISldWorks)Marshal.GetActiveObject("SldWorks.Application"); }
            catch
            {
                var type = Type.GetTypeFromProgID("SldWorks.Application", true);
                return (ISldWorks)Activator.CreateInstance(type);
            }
        }

        private static void Require(AgentCore agent, string tool, Dictionary<string, object> parameters = null)
        {
            ToolResult result = agent.ExecuteTool(tool, parameters);
            Check(tool, result.IsSuccess, result.ErrorMessage ?? Convert.ToString(result.Data));
            if (!result.IsSuccess) throw new InvalidOperationException(tool + " failed: " + result.ErrorMessage);
        }

        private static void Save(IModelDoc2 model, string path)
        {
            int errors = 0, warnings = 0;
            bool ok = model != null && model.Extension.SaveAs(
                path,
                (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                (int)swSaveAsOptions_e.swSaveAsOptions_Silent,
                null,
                ref errors,
                ref warnings);
            Check("Save " + Path.GetFileName(path), ok && errors == 0,
                "Errors=" + errors + ", Warnings=" + warnings);
        }

        private static void Track(IModelDoc2 model)
        {
            if (model == null) return;
            string title = model.GetTitle();
            if (!string.IsNullOrWhiteSpace(title) && !CreatedTitles.Contains(title))
                CreatedTitles.Add(title);
        }

        private static void CloseTracked(ISldWorks sw, string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return;
            try { sw.CloseDoc(title); } catch { }
            CreatedTitles.Remove(title);
        }

        private static void CloseByPathOrTitle(ISldWorks sw, string path)
        {
            try
            {
                var model = sw.GetOpenDocumentByName(path) as IModelDoc2;
                if (model != null) sw.CloseDoc(model.GetTitle());
            }
            catch { }
        }

        private static void Check(string name, bool ok, string detail)
        {
            if (ok)
            {
                _passed++;
                Console.WriteLine("[PASS] " + name);
            }
            else
            {
                _failed++;
                Console.WriteLine("[FAIL] " + name + " :: " + detail);
            }
        }

        private static void Skip(string name, string reason)
        {
            _skipped++;
            Console.WriteLine("[SKIP] " + name + " :: " + reason);
        }
    }
}
