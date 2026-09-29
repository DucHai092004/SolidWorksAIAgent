using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SwMateAI.Core.Agent;
using SwMateAI.Core.Drawing;
using SwMateAI.Core.DrawingUnderstanding;
using SwMateAI.Core.Tools;

namespace SwMateAI.SolidWorks.IntegrationRunner
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
                "SW-MATE_AI_Integration_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(root);
            string partPath = Path.Combine(root, "Integration_Block.SLDPRT");
            string pdfPath = Path.Combine(root, "Integration_Drawing.pdf");
            string dxfPath = Path.Combine(root, "Integration_Drawing.dxf");
            TryDelete(partPath); TryDelete(pdfPath); TryDelete(dxfPath);

            ISldWorks sw = null;
            try
            {
                sw = ConnectSolidWorks();
                sw.Visible = true;
                var agent = new AgentCore(sw);
                Check("Agent registry", agent.RegisteredTools.Count() >= 50, "Registered tools < 50");

                Run(agent, "CreatePart");
                Track(sw);
                Run(agent, "CreateSketch");
                Run(agent, "CreateRectangle", new Dictionary<string, object> { ["Width"] = 60d, ["Height"] = 40d });
                Run(agent, "Extrude", new Dictionary<string, object> { ["Depth"] = 20d });

                var part = sw.ActiveDoc as IModelDoc2;
                int markedDimensions = MarkAllFeatureDimensionsForDrawing(part);
                Check("Model dimensions marked for Drawing", markedDimensions > 0, "No feature dimensions were found");
                Check("Part document created", part != null && part.GetType() == (int)swDocumentTypes_e.swDocPART, "Active doc is not Part");
                Save(part, partPath);
                Track(sw);
                Check("Part saved", File.Exists(partPath), partPath);

                Run(agent, "ReadFeatureTree");
                Run(agent, "ReadBoundingBox");
                Run(agent, "ReadMassProperties");

                Run(agent, "CreateDrawing");
                Track(sw);
                Run(agent, "InsertStandardViews");
                Run(agent, "InsertIsometricView");
                Run(agent, "CreateSection", new Dictionary<string, object> { ["Label"] = "A", ["Direction"] = "Vertical" });
                Run(agent, "CreateDetail", new Dictionary<string, object> { ["Label"] = "B", ["ScaleNumerator"] = 2d, ["ScaleDenominator"] = 1d });
                ToolResult dimensionResult = agent.ExecuteTool("InsertDimensions");
                Check("InsertDimensions", dimensionResult.IsSuccess, dimensionResult.ErrorMessage ?? string.Empty);
                var dimensionData = dimensionResult.Data as DrawingAnnotationResult;
                Check("Drawing dimensions inserted", dimensionData != null && dimensionData.InsertedCount > 0,
                    "InsertedCount=" + (dimensionData?.InsertedCount ?? 0));
                Run(agent, "FillTitleBlock", new Dictionary<string, object>
                {
                    ["Title"] = "SW-MATE AI Integration Test",
                    ["Drawing Number"] = "TEST-001",
                    ["Revision"] = "A"
                });

                Run(agent, "ExportPDF", new Dictionary<string, object> { ["OutputPath"] = pdfPath });
                Run(agent, "ExportDXF", new Dictionary<string, object> { ["OutputPath"] = dxfPath });
                Check("PDF file exists", File.Exists(pdfPath) && new FileInfo(pdfPath).Length > 0, pdfPath);
                Check("DXF file exists", File.Exists(dxfPath) && new FileInfo(dxfPath).Length > 0, dxfPath);

                CloseActive(sw);
                string assemblyTemplate = sw.GetUserPreferenceStringValue((int)swUserPreferenceStringValue_e.swDefaultTemplateAssembly);
                var assemblyModel = sw.NewDocument(assemblyTemplate, 0, 0, 0) as IModelDoc2;
                var assembly = assemblyModel as IAssemblyDoc;
                Check("Create integration Assembly", assembly != null, "Could not create Assembly");
                Track(sw);

                var first = assembly?.AddComponent4(partPath, string.Empty, 0, 0, 0);
                Check("Add first component", first != null, "AddComponent4 returned null");
                string assemblyPath = Path.Combine(root, "Integration_Assembly.SLDASM");
                TryDelete(assemblyPath);
                Save(assemblyModel, assemblyPath);
                Track(sw);

                Run(agent, "InsertComponent", new Dictionary<string, object>
                {
                    ["Path"] = partPath, ["X"] = 100d, ["Y"] = 0d, ["Z"] = 0d
                });
                Run(agent, "ReadAssembly");
                Run(agent, "ReadComponents");
                Run(agent, "ReadMates");
                Run(agent, "CheckInterference");

                string materialCsv = Path.Combine(root, "materials.csv");
                File.WriteAllLines(materialCsv, new[]
                {
                    "Part Number,Stock Material",
                    "Integration_Block,C45"
                });
                Run(agent, "BuildManufacturingBreakdown");
                string breakdownPath = Path.Combine(root, "Integration_Breakdown.xlsx");
                TryDelete(breakdownPath);
                Run(agent, "ExportManufacturingBreakdown", new Dictionary<string, object> { ["OutputPath"] = breakdownPath });
                Check("Breakdown Excel exists", File.Exists(breakdownPath) && new FileInfo(breakdownPath).Length > 0, breakdownPath);

                Run(agent, "CreateBOM", new Dictionary<string, object>
                {
                    ["ExportExcel"] = true, ["ExportCsv"] = true, ["OutputFolder"] = root
                });

                Run(agent, "CreateDrawing");
                Track(sw);
                Run(agent, "InsertStandardViews");
                string bomTemplate = @"E:\SolidWorksAIAgent\Template\BOM VT.sldbomtbt";
                var bomParameters = new Dictionary<string, object>();
                if (File.Exists(bomTemplate)) bomParameters["TemplatePath"] = bomTemplate;
                Run(agent, "InsertDrawingBOM", bomParameters);
                Run(agent, "InsertBalloon");

                ToolResult assemblyDrawingRead = agent.ExecuteTool("ReadDrawing");
                Check("Read assembly Drawing", assemblyDrawingRead.IsSuccess,
                    assemblyDrawingRead.ErrorMessage ?? string.Empty);
                var assemblyDrawingData = assemblyDrawingRead.Data as DrawingUnderstandingResult;
                Check("Drawing reader sees BOM table",
                    assemblyDrawingData != null && assemblyDrawingData.TableCount > 0,
                    "TableCount=" + (assemblyDrawingData?.TableCount ?? 0));
                Check("Semantic tables match table count",
                    assemblyDrawingData != null && assemblyDrawingData.Tables.Count == assemblyDrawingData.TableCount,
                    $"Semantic={assemblyDrawingData?.Tables.Count ?? 0}, Count={assemblyDrawingData?.TableCount ?? 0}");

                bool semanticTableFound = false;
                if (assemblyDrawingData != null)
                {
                    foreach (var table in assemblyDrawingData.Tables)
                    {
                        if (table.RowCount <= 0 || table.ColumnCount <= 0 || table.Rows.Count != table.RowCount)
                            continue;
                        bool hasText = false;
                        foreach (var row in table.Rows)
                        {
                            if (row.Count != table.ColumnCount) continue;
                            if (row.Any(cell => !string.IsNullOrWhiteSpace(cell))) hasText = true;
                        }
                        if (hasText)
                        {
                            semanticTableFound = true;
                            break;
                        }
                    }
                }
                Check("Reader returns semantic table contents",
                    semanticTableFound,
                    "No table had valid dimensions and displayed cell text");

                string assemblyPdf = Path.Combine(root, "Integration_Assembly_Drawing.pdf");
                TryDelete(assemblyPdf);
                Run(agent, "ExportPDF", new Dictionary<string, object> { ["OutputPath"] = assemblyPdf });
                Check("Assembly drawing PDF exists", File.Exists(assemblyPdf) && new FileInfo(assemblyPdf).Length > 0, assemblyPdf);
            }
            catch (Exception ex)
            {
                _failed++;
                Console.WriteLine("[FATAL] " + ex);
            }
            finally
            {
                if (sw != null)
                    foreach (string title in CreatedTitles.AsEnumerable().Reverse())
                        try { sw.CloseDoc(title); } catch { }
            }

            Console.WriteLine($"RESULT: PASS={_passed} FAIL={_failed}");
            return _failed == 0 ? 0 : 1;
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

        private static int MarkAllFeatureDimensionsForDrawing(IModelDoc2 model)
        {
            int count = 0;
            var feature = model?.FirstFeature() as IFeature;
            while (feature != null)
            {
                var display = feature.GetFirstDisplayDimension() as IDisplayDimension;
                while (display != null)
                {
                    display.MarkedForDrawing = true;
                    count++;
                    display = feature.GetNextDisplayDimension(display) as IDisplayDimension;
                }
                feature = feature.GetNextFeature() as IFeature;
            }
            return count;
        }

        private static void CloseActive(ISldWorks sw)
        {
            var model = sw.ActiveDoc as IModelDoc2;
            if (model == null) return;
            string title = model.GetTitle();
            try { sw.CloseDoc(title); } catch { }
        }

        private static void Track(ISldWorks sw)
        {
            var model = sw.ActiveDoc as IModelDoc2;
            if (model != null && !CreatedTitles.Contains(model.GetTitle()))
                CreatedTitles.Add(model.GetTitle());
        }

        private static void Run(AgentCore agent, string tool, Dictionary<string, object> p = null)
        {
            ToolResult result = agent.ExecuteTool(tool, p);
            Check(tool, result.IsSuccess, result.ErrorMessage ?? Convert.ToString(result.Data));
        }

        private static void Save(IModelDoc2 model, string path)
        {
            int errors = 0, warnings = 0;
            bool ok = model.Extension.SaveAs(path,
                (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                (int)swSaveAsOptions_e.swSaveAsOptions_Silent,
                null, ref errors, ref warnings);
            Check("Save " + Path.GetFileName(path), ok && errors == 0, $"Errors={errors}, Warnings={warnings}");
        }

        private static void Check(string name, bool ok, string detail)
        {
            if (ok) { _passed++; Console.WriteLine("[PASS] " + name); }
            else { _failed++; Console.WriteLine("[FAIL] " + name + " :: " + detail); }
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }
}
