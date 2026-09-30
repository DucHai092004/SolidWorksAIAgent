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

namespace SwMateAI.BomHierarchy.IntegrationRunner
{
    internal static class Program
    {
        private static int _passed;
        private static int _failed;
        private static readonly List<string> CreatedTitles = new List<string>();

        private static int Main()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "SW-MATE_AI_BomHierarchy_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(root);

            string partPath = Path.Combine(root, "Hierarchy_Part.SLDPRT");
            string subPath = Path.Combine(root, "Hierarchy_SubAssembly.SLDASM");
            string topPath = Path.Combine(root, "Hierarchy_TopAssembly.SLDASM");

            ISldWorks sw = null;
            try
            {
                sw = ConnectSolidWorks();
                sw.Visible = true;
                var agent = new AgentCore(sw);

                Run(agent, "CreatePart");
                Track(sw);
                Run(agent, "CreateSketch");
                Run(agent, "CreateRectangle", new Dictionary<string, object>
                {
                    ["Width"] = 40d,
                    ["Height"] = 30d
                });
                Run(agent, "Extrude", new Dictionary<string, object> { ["Depth"] = 10d });

                var partModel = sw.ActiveDoc as IModelDoc2;
                Check("Hierarchy Part created",
                    partModel != null && partModel.GetType() == (int)swDocumentTypes_e.swDocPART,
                    "Active document is not a Part");
                Save(partModel, partPath);
                Track(sw);

                string assemblyTemplate = sw.GetUserPreferenceStringValue(
                    (int)swUserPreferenceStringValue_e.swDefaultTemplateAssembly);

                var subModel = sw.NewDocument(assemblyTemplate, 0, 0, 0) as IModelDoc2;
                var subAssembly = subModel as IAssemblyDoc;
                Check("Create hierarchy Subassembly", subAssembly != null, "Could not create Subassembly");
                Track(sw);

                var subPart1 = subAssembly?.AddComponent4(partPath, string.Empty, 0, 0, 0);
                Check("Add first Part occurrence to Subassembly",
                    subPart1 != null,
                    "First AddComponent4 returned null");
                Save(subModel, subPath);

                ToolResult secondInsert = agent.ExecuteTool("InsertComponent", new Dictionary<string, object>
                {
                    ["Path"] = partPath,
                    ["X"] = 80d,
                    ["Y"] = 0d,
                    ["Z"] = 0d
                });
                Check("Add second Part occurrence to Subassembly",
                    secondInsert.IsSuccess,
                    secondInsert.ErrorMessage ?? Convert.ToString(secondInsert.Data));
                Save(subModel, subPath);

                var subConfiguration = subModel?.ConfigurationManager?.ActiveConfiguration as IConfiguration;
                Check("Subassembly configuration available",
                    subConfiguration != null,
                    "Active Subassembly configuration is null");

                var topModel = sw.NewDocument(assemblyTemplate, 0, 0, 0) as IModelDoc2;
                var topAssembly = topModel as IAssemblyDoc;
                Check("Create hierarchy Top assembly", topAssembly != null, "Could not create Top assembly");
                Track(sw);

                var topSub = topAssembly?.AddComponent4(subPath, string.Empty, 0, 0, 0);
                Check("Add Subassembly to Top assembly", topSub != null, "Could not add Subassembly");
                Save(topModel, topPath);

                RunCase(agent, topModel, subConfiguration,
                    BomMode.TopLevel, BomChildDisplay.Show,
                    1, 0, 0, 0);
                RunCase(agent, topModel, subConfiguration,
                    BomMode.TopLevel, BomChildDisplay.Hide,
                    1, 0, 0, 0);
                RunCase(agent, topModel, subConfiguration,
                    BomMode.TopLevel, BomChildDisplay.Promote,
                    0, 1, 2, 0);

                RunCase(agent, topModel, subConfiguration,
                    BomMode.PartsOnly, BomChildDisplay.Show,
                    0, 1, 2, 0);
                RunCase(agent, topModel, subConfiguration,
                    BomMode.PartsOnly, BomChildDisplay.Hide,
                    0, 0, 0, 0);
                RunCase(agent, topModel, subConfiguration,
                    BomMode.PartsOnly, BomChildDisplay.Promote,
                    0, 1, 2, 0);

                RunCase(agent, topModel, subConfiguration,
                    BomMode.Indented, BomChildDisplay.Show,
                    1, 1, 2, 1);
                RunCase(agent, topModel, subConfiguration,
                    BomMode.Indented, BomChildDisplay.Hide,
                    1, 0, 0, 0);
                RunCase(agent, topModel, subConfiguration,
                    BomMode.Indented, BomChildDisplay.Promote,
                    0, 1, 2, 0);

                ToolResult legacy = agent.ExecuteTool("CreateBOM", new Dictionary<string, object>());
                Check("Legacy BOM remains available",
                    legacy.IsSuccess && legacy.Data is BomResult,
                    legacy.ErrorMessage ?? "Legacy result missing");

                // Regression for real large assemblies: Lightweight components are valid BOM
                // occurrences and must not be counted as suppressed.
                if (subModel != null)
                {
                    try { sw.CloseDoc(subModel.GetTitle()); } catch { }
                }
                topModel?.ClearSelection2(true);
                bool lightweightSelected = topSub != null && topSub.Select4(false, null, false);
                if (lightweightSelected && topAssembly != null)
                    topAssembly.MakeLightWeight();

                int actualSuppression = topSub == null ? -1 : topSub.GetSuppression();
                bool isLightweight =
                    actualSuppression == (int)swComponentSuppressionState_e.swComponentLightweight ||
                    actualSuppression == (int)swComponentSuppressionState_e.swComponentFullyLightweight;
                Check("Set top Subassembly Lightweight",
                    topSub != null && lightweightSelected && isLightweight,
                    "Selected=" + lightweightSelected + ", State=" + actualSuppression);

                ToolResult lightweightExport = agent.ExecuteTool("CreateBOM", new Dictionary<string, object>
                {
                    ["Mode"] = BomMode.LegacyFlat.ToString(),
                    ["ExportExcel"] = true,
                    ["OutputFolder"] = root
                });
                var lightweightBom = lightweightExport.Data as BomResult;
                Check("Legacy BOM includes Lightweight component",
                    lightweightExport.IsSuccess && lightweightBom != null &&
                    lightweightBom.TotalOccurrences > 0 &&
                    lightweightBom.Items.Count > 0 &&
                    lightweightBom.SuppressedSkipped == 0,
                    lightweightExport.ErrorMessage ??
                    (lightweightBom == null
                        ? "BOM result missing"
                        : "Occurrences=" + lightweightBom.TotalOccurrences +
                          ", Items=" + lightweightBom.Items.Count +
                          ", Suppressed=" + lightweightBom.SuppressedSkipped));
                Check("Lightweight BOM Excel exists",
                    lightweightBom != null &&
                    !string.IsNullOrWhiteSpace(lightweightBom.ExcelPath) &&
                    File.Exists(lightweightBom.ExcelPath),
                    lightweightBom?.ExcelPath ?? "Excel path missing");

                string imageDetail = lightweightBom == null
                    ? "BOM result missing"
                    : string.Join(" | ", lightweightBom.Items.Select(x =>
                        "#" + x.ItemNumber +
                        " " + x.ComponentType +
                        " " + x.PartNumber +
                        " Loaded=" + x.IsLoaded +
                        " Image=" + (!string.IsNullOrWhiteSpace(x.ImagePath))));
                Check("Lightweight BOM image captured",
                    lightweightBom != null &&
                    lightweightBom.Items.Count > 0 &&
                    lightweightBom.CapturedImageCount == lightweightBom.Items.Count,
                    lightweightBom == null
                        ? "BOM result missing"
                        : "Captured=" + lightweightBom.CapturedImageCount +
                          "/" + lightweightBom.Items.Count + " :: " + imageDetail);
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
                }
            }

            Console.WriteLine("RESULT: PASS=" + _passed + " FAIL=" + _failed);
            return _failed == 0 ? 0 : 1;
        }

        private static void RunCase(
            AgentCore agent,
            IModelDoc2 topModel,
            IConfiguration subConfiguration,
            BomMode mode,
            BomChildDisplay display,
            int expectedAssemblyRows,
            int expectedPartRows,
            int expectedPartQuantity,
            int expectedPartLevel)
        {
            subConfiguration.ChildComponentDisplayInBOM = ToSolidWorksDisplay(display);
            topModel?.ForceRebuild3(false);

            ToolResult toolResult = agent.ExecuteTool("CreateBOM", new Dictionary<string, object>
            {
                ["Mode"] = mode.ToString(),
                ["RespectChildDisplay"] = true
            });

            string caseName = mode + " / " + display;
            if (!toolResult.IsSuccess)
            {
                Check(caseName, false, toolResult.ErrorMessage ?? "CreateBOM failed");
                return;
            }

            var bom = toolResult.Data as BomResult;
            if (bom == null)
            {
                Check(caseName, false, "CreateBOM did not return BomResult");
                return;
            }

            int assemblyRows = bom.Items.Count(x =>
                string.Equals(x.ComponentType, "Assembly", StringComparison.OrdinalIgnoreCase));
            var partRows = bom.Items.Where(x =>
                string.Equals(x.ComponentType, "Part", StringComparison.OrdinalIgnoreCase)).ToList();
            int partQuantity = partRows.Sum(x => x.Quantity);
            bool levelsOk = partRows.All(x => x.Level == expectedPartLevel);

            bool ok = bom.Mode == mode &&
                      assemblyRows == expectedAssemblyRows &&
                      partRows.Count == expectedPartRows &&
                      partQuantity == expectedPartQuantity &&
                      levelsOk;

            string detail =
                "Mode=" + bom.Mode +
                ", AssemblyRows=" + assemblyRows +
                ", PartRows=" + partRows.Count +
                ", PartQty=" + partQuantity +
                ", Levels=" + string.Join(",", partRows.Select(x => x.Level));
            Check(caseName, ok, detail);
        }

        private static int ToSolidWorksDisplay(BomChildDisplay display)
        {
            switch (display)
            {
                case BomChildDisplay.Hide:
                    return (int)swChildComponentInBOMOption_e.swChildComponent_Hide;
                case BomChildDisplay.Promote:
                    return (int)swChildComponentInBOMOption_e.swChildComponent_Promote;
                default:
                    return (int)swChildComponentInBOMOption_e.swChildComponent_Show;
            }
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

        private static void Run(AgentCore agent, string tool, Dictionary<string, object> parameters = null)
        {
            ToolResult result = agent.ExecuteTool(tool, parameters);
            Check(tool, result.IsSuccess, result.ErrorMessage ?? Convert.ToString(result.Data));
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

        private static void Track(ISldWorks sw)
        {
            var model = sw.ActiveDoc as IModelDoc2;
            if (model != null && !CreatedTitles.Contains(model.GetTitle()))
                CreatedTitles.Add(model.GetTitle());
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
    }
}
