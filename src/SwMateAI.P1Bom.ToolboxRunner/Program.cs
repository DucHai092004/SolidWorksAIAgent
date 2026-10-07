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

namespace SwMateAI.P1Bom.ToolboxRunner
{
    internal static class Program
    {
        private static int Main()
        {
            ISldWorks sw = null;
            string originalTitle = string.Empty;
            string toolboxTitle = string.Empty;
            string assemblyTitle = string.Empty;

            try
            {
                sw = ConnectSolidWorks();
                sw.Visible = true;
                originalTitle = (sw.ActiveDoc as IModelDoc2)?.GetTitle() ?? string.Empty;

                string toolboxRoot = sw.GetUserPreferenceStringValue(
                    (int)swUserPreferenceStringValue_e.swHoleWizardToolBoxFolder);
                Console.WriteLine("Toolbox root: " + toolboxRoot);
                if (string.IsNullOrWhiteSpace(toolboxRoot) || !Directory.Exists(toolboxRoot))
                    return Fail("TC019 Toolbox folder is not configured or does not exist.");

                ToolboxFixture fixture = FindToolboxFixture(sw, toolboxRoot);
                if (fixture == null)
                    return Fail("TC019 could not find a real Toolbox fastener with configuration metadata.");

                toolboxTitle = fixture.Model.GetTitle();
                Console.WriteLine("Toolbox part: " + fixture.Path);
                Console.WriteLine("ToolboxPartType=" + fixture.ToolboxPartType +
                                  " Config=" + fixture.Configuration +
                                  " PartNumber=" + fixture.PartNumber +
                                  " Description=" + fixture.Description);

                string assemblyTemplate = sw.GetUserPreferenceStringValue(
                    (int)swUserPreferenceStringValue_e.swDefaultTemplateAssembly);
                var assemblyModel = sw.NewDocument(assemblyTemplate, 0, 0, 0) as IModelDoc2;
                var assembly = assemblyModel as IAssemblyDoc;
                if (assemblyModel == null || assembly == null)
                    return Fail("TC019 could not create test assembly.");
                assemblyTitle = assemblyModel.GetTitle();

                int activateErrors = 0;
                sw.ActivateDoc3(
                    assemblyTitle,
                    false,
                    (int)swRebuildOnActivation_e.swDontRebuildActiveDoc,
                    ref activateErrors);
                if (activateErrors != 0)
                    return Fail("TC019 could not activate test assembly. Errors=" + activateErrors);

                var component = assembly.AddComponent4(
                    fixture.Path,
                    fixture.Configuration,
                    0,
                    0,
                    0) as IComponent2;
                if (component == null)
                    return Fail("TC019 could not insert Toolbox component into test assembly.");

                string root = Path.Combine(
                    Path.GetTempPath(),
                    "SW_MATE_AI_TC019_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                Directory.CreateDirectory(root);
                string assemblyPath = Path.Combine(root, "TC019_Toolbox_Test.SLDASM");
                if (!Save(assemblyModel, assemblyPath))
                    return Fail("TC019 could not save test assembly.");

                var agent = new AgentCore(sw);
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
                    return Fail("TC019 CreateBOM failed: " + (result?.ErrorMessage ?? "No BomResult"));

                BomItem item = bom.Items.SingleOrDefault();
                if (item == null)
                    return Fail("TC019 expected one BOM row, got " + bom.Items.Count + ".");

                string actualConfiguration = component.ReferencedConfiguration ?? string.Empty;
                string expectedPartNumber = FirstNonEmpty(
                    ReadProperty(fixture.Model, actualConfiguration, "Part Number"),
                    ReadProperty(fixture.Model, actualConfiguration, "PartNumber"),
                    CleanCadName(Path.GetFileName(fixture.Path)));
                string expectedDescription = FirstNonEmpty(
                    ReadProperty(fixture.Model, actualConfiguration, "Description"),
                    ReadProperty(fixture.Model, actualConfiguration, "DESCRIPTION"));

                bool toolboxDetected = fixture.ToolboxPartType != (int)swToolBoxPartType_e.swNotAToolboxPart;
                bool configurationOk = string.Equals(
                    item.Configuration,
                    actualConfiguration,
                    StringComparison.OrdinalIgnoreCase);
                bool partNumberOk = string.Equals(
                    item.PartNumber,
                    expectedPartNumber,
                    StringComparison.OrdinalIgnoreCase);
                bool descriptionOk = string.IsNullOrWhiteSpace(expectedDescription) ||
                    string.Equals(item.Description, expectedDescription, StringComparison.OrdinalIgnoreCase);
                bool hasToolboxMetadata = !string.IsNullOrWhiteSpace(fixture.PartNumber) ||
                                          !string.IsNullOrWhiteSpace(fixture.Description);

                Console.WriteLine("BOM PartNumber=" + item.PartNumber +
                                  " Description=" + item.Description +
                                  " Configuration=" + item.Configuration);
                Console.WriteLine("Expected PartNumber=" + expectedPartNumber +
                                  " Description=" + expectedDescription +
                                  " Configuration=" + actualConfiguration);

                if (!toolboxDetected || !hasToolboxMetadata || !configurationOk || !partNumberOk || !descriptionOk)
                {
                    return Fail(
                        "TC019 Toolbox metadata mismatch. Detected=" + toolboxDetected +
                        " HasMetadata=" + hasToolboxMetadata +
                        " ConfigOk=" + configurationOk +
                        " PartNumberOk=" + partNumberOk +
                        " DescriptionOk=" + descriptionOk);
                }

                Console.WriteLine("[PASS] TC019 real Toolbox component metadata is preserved in BOM.");
                return 0;
            }
            catch (Exception ex)
            {
                return Fail("TC019 fatal :: " + ex);
            }
            finally
            {
                if (sw != null)
                {
                    try { if (!string.IsNullOrWhiteSpace(assemblyTitle)) sw.CloseDoc(assemblyTitle); } catch { }
                    try { if (!string.IsNullOrWhiteSpace(toolboxTitle)) sw.CloseDoc(toolboxTitle); } catch { }
                    if (!string.IsNullOrWhiteSpace(originalTitle))
                    {
                        int errors = 0;
                        try { sw.ActivateDoc3(originalTitle, false, 0, ref errors); } catch { }
                    }
                }
            }
        }

        private static ToolboxFixture FindToolboxFixture(ISldWorks sw, string toolboxRoot)
        {
            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(toolboxRoot, "*.sldprt", SearchOption.AllDirectories);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[FAIL] Enumerating Toolbox failed: " + ex.Message);
                return null;
            }

            string[] fastenerWords = { "bolt", "screw", "nut", "washer", "hex", "socket" };
            var ordered = files
                .OrderByDescending(path => fastenerWords.Any(word =>
                    path.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0))
                .Take(400)
                .ToList();

            foreach (string path in ordered)
            {
                int errors = 0, warnings = 0;
                var model = sw.OpenDoc6(
                    path,
                    (int)swDocumentTypes_e.swDocPART,
                    (int)swOpenDocOptions_e.swOpenDocOptions_Silent,
                    string.Empty,
                    ref errors,
                    ref warnings) as IModelDoc2;
                if (model == null) continue;

                int type = 0;
                try { type = model.Extension.ToolboxPartType; } catch { }
                string configuration = model.ConfigurationManager?.ActiveConfiguration?.Name ?? string.Empty;
                string partNumber = FirstNonEmpty(
                    ReadProperty(model, configuration, "Part Number"),
                    ReadProperty(model, configuration, "PartNumber"));
                string description = FirstNonEmpty(
                    ReadProperty(model, configuration, "Description"),
                    ReadProperty(model, configuration, "DESCRIPTION"));

                bool isToolbox = type != (int)swToolBoxPartType_e.swNotAToolboxPart;
                bool hasMetadata = !string.IsNullOrWhiteSpace(partNumber) || !string.IsNullOrWhiteSpace(description);
                if (isToolbox && hasMetadata)
                {
                    return new ToolboxFixture
                    {
                        Model = model,
                        Path = path,
                        ToolboxPartType = type,
                        Configuration = configuration,
                        PartNumber = partNumber,
                        Description = description
                    };
                }

                try { sw.CloseDoc(model.GetTitle()); } catch { }
            }

            return null;
        }

        private static string ReadProperty(IModelDoc2 model, string configuration, string name)
        {
            string value = ReadPropertyFrom(model, configuration, name);
            return string.IsNullOrWhiteSpace(value)
                ? ReadPropertyFrom(model, string.Empty, name)
                : value;
        }

        private static string ReadPropertyFrom(IModelDoc2 model, string configuration, string name)
        {
            var manager = model?.Extension?.get_CustomPropertyManager(configuration ?? string.Empty);
            if (manager == null) return string.Empty;
            string raw, resolved;
            bool wasResolved, linked;
            manager.Get6(name, false, out raw, out resolved, out wasResolved, out linked);
            return string.IsNullOrWhiteSpace(resolved) ? raw ?? string.Empty : resolved;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (string value in values)
                if (!string.IsNullOrWhiteSpace(value)) return value;
            return string.Empty;
        }

        private static string CleanCadName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            string name = Path.GetFileName(value.Trim());
            if (name.EndsWith(".sldprt", StringComparison.OrdinalIgnoreCase))
                name = name.Substring(0, name.Length - 7);
            return name;
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

        private sealed class ToolboxFixture
        {
            public IModelDoc2 Model { get; set; }
            public string Path { get; set; }
            public int ToolboxPartType { get; set; }
            public string Configuration { get; set; }
            public string PartNumber { get; set; }
            public string Description { get; set; }
        }
    }
}
