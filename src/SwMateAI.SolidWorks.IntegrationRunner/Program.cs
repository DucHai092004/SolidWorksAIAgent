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
using SwMateAI.Core.Manufacturing;
using SwMateAI.Core.Models.Understanding;
using SwMateAI.Core.Tools;
using SwMateAI.Core.Tools.Manufacturing;

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

                Run(agent, "GetModelInfo");
                Run(agent, "ReadFeatureTree");

                ToolResult readFeatures = agent.ExecuteTool("ReadFeatures");
                Check("ReadFeatures", readFeatures.IsSuccess, readFeatures.ErrorMessage ?? string.Empty);
                var features = readFeatures.Data as List<FeatureInfo> ?? new List<FeatureInfo>();
                Check("Reader sees model features", features.Count > 0, "Feature count=0");

                ToolResult readSketches = agent.ExecuteTool("ReadSketches");
                Check("ReadSketches", readSketches.IsSuccess, readSketches.ErrorMessage ?? string.Empty);
                var sketches = readSketches.Data as List<FeatureInfo> ?? new List<FeatureInfo>();
                Check("Reader sees sketches", sketches.Count > 0, "Sketch count=0");

                ToolResult readDimensions = agent.ExecuteTool("ReadDimensions");
                Check("ReadDimensions", readDimensions.IsSuccess, readDimensions.ErrorMessage ?? string.Empty);
                var dimensions = readDimensions.Data as List<DimensionInfo> ?? new List<DimensionInfo>();
                Check("Reader sees dimensions", dimensions.Count > 0, "Dimension count=0");

                ToolResult readDependencies = agent.ExecuteTool("ReadFeatureDependencies");
                Check("ReadFeatureDependencies", readDependencies.IsSuccess, readDependencies.ErrorMessage ?? string.Empty);
                var dependencies = readDependencies.Data as List<FeatureDependencyInfo> ?? new List<FeatureDependencyInfo>();
                Check("Reader sees feature dependency graph", dependencies.Count > 0, "Dependency count=0");

                var impactTarget = features.FirstOrDefault(f =>
                    f.Name.IndexOf("Extrude", StringComparison.OrdinalIgnoreCase) >= 0) ??
                    features.FirstOrDefault(f => !f.IsSketch);
                if (impactTarget != null)
                {
                    ToolResult impactResult = agent.ExecuteTool("AnalyzeFeatureImpact", new Dictionary<string, object>
                    {
                        ["FeatureName"] = impactTarget.Name
                    });
                    Check("AnalyzeFeatureImpact", impactResult.IsSuccess, impactResult.ErrorMessage ?? string.Empty);
                    var impact = impactResult.Data as FeatureImpactInfo;
                    Check("Feature impact target matches", impact != null && impact.TargetFeature == impactTarget.Name,
                        impact?.TargetFeature ?? "No impact result");

                    var selectedFeature = FindFeature(part, impactTarget.Name);
                    if (selectedFeature != null && selectedFeature.Select2(false, 0))
                    {
                        Run(agent, "ReadSelectedObject");
                        part.ClearSelection2(true);
                    }
                    else
                    {
                        Check("Select feature for ReadSelectedObject", false, impactTarget.Name);
                    }
                }
                else
                {
                    Check("Find feature for AnalyzeFeatureImpact", false, "No suitable feature found");
                }

                Run(agent, "ReadMaterial");
                Run(agent, "ReadCustomProperties");
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

                ToolResult readDrawing = agent.ExecuteTool("ReadDrawing");
                Check("ReadDrawing", readDrawing.IsSuccess, readDrawing.ErrorMessage ?? string.Empty);
                var drawingData = readDrawing.Data as DrawingUnderstandingResult;
                Check("Drawing reader sees sheets", drawingData != null && drawingData.SheetCount >= 1, "No sheets");
                Check("Drawing reader sees model views", drawingData != null && drawingData.ModelViewCount >= 4, "ModelViewCount=" + (drawingData?.ModelViewCount ?? 0));
                Check("Drawing reader sees dimensions", drawingData != null && drawingData.DimensionCount > 0, "DimensionCount=" + (drawingData?.DimensionCount ?? 0));

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

                // Assembly + BOM + manufacturing regression on disposable files.
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

                string materialFolder = Path.Combine(root, "material_inputs");
                Directory.CreateDirectory(materialFolder);
                string materialCsv = Path.Combine(materialFolder, "materials.csv");
                File.WriteAllLines(materialCsv, new[]
                {
                    "Part Number,Stock Material",
                    "Integration_Block,C45"
                });
                ToolResult breakdownResult = agent.ExecuteTool("BuildManufacturingBreakdown");
                Check("BuildManufacturingBreakdown", breakdownResult.IsSuccess, breakdownResult.ErrorMessage ?? string.Empty);
                var breakdownData = breakdownResult.Data as BreakdownResult;
                Check("Breakdown contains manufactured Part", breakdownData != null && breakdownData.Items.Count > 0,
                    "Breakdown item count=" + (breakdownData?.Items.Count ?? 0));
                string breakdownPath = Path.Combine(root, "Integration_Breakdown.xlsx");
                TryDelete(breakdownPath);
                Run(agent, "ExportManufacturingBreakdown", new Dictionary<string, object> { ["OutputPath"] = breakdownPath });
                Check("Breakdown Excel exists", File.Exists(breakdownPath) && new FileInfo(breakdownPath).Length > 0, breakdownPath);

                // Stock material write-back is exercised only on the disposable Part above.
                ToolResult stockApply = agent.ExecuteTool("ApplyStockMaterials", new Dictionary<string, object> { ["DocumentRoot"] = materialFolder });
                Check("ApplyStockMaterials", stockApply.IsSuccess, stockApply.ErrorMessage ?? string.Empty);
                var stockApplyData = stockApply.Data as ApplyStockMaterialsResult;
                Check("Stock material applied count", stockApplyData != null && stockApplyData.Applied > 0,
                    "Applied=" + (stockApplyData?.Applied ?? 0));

                var stockItem = breakdownData?.Items.FirstOrDefault(i =>
                    string.Equals(i.SourcePath, partPath, StringComparison.OrdinalIgnoreCase))
                    ?? breakdownData?.Items.FirstOrDefault();
                var stockComponent = stockItem == null ? null : assembly?.GetComponentByName(stockItem.RepresentativeComponentName);
                string referencedConfiguration = stockItem?.Configuration ?? string.Empty;
                var stockModel = stockComponent?.GetModelDoc2() as IModelDoc2;
                Check("Stock component model available", stockModel != null,
                    stockItem?.RepresentativeComponentName ?? partPath);
                var stockProperties = stockModel?.Extension.get_CustomPropertyManager(referencedConfiguration);
                string stockRaw, stockResolved;
                bool stockWasResolved, stockLinked;
                int stockMaterialStatus = stockProperties.Get6("Stock Material", false, out stockRaw, out stockResolved, out stockWasResolved, out stockLinked);
                Check("Stock Material read-back", stockMaterialStatus != 0 && stockResolved == "C45",
                    $"Config={referencedConfiguration}, Value={stockResolved}");
                int stockSizeStatus = stockProperties.Get6("StockSize", false, out stockRaw, out stockResolved, out stockWasResolved, out stockLinked);
                Check("StockSize read-back", stockSizeStatus != 0 && !string.IsNullOrWhiteSpace(stockResolved),
                    $"Config={referencedConfiguration}, Value={stockResolved}");

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
                Check("Read assembly Drawing", assemblyDrawingRead.IsSuccess, assemblyDrawingRead.ErrorMessage ?? string.Empty);
                var assemblyDrawingData = assemblyDrawingRead.Data as DrawingUnderstandingResult;
                Check("Drawing reader sees BOM table", assemblyDrawingData != null && assemblyDrawingData.TableCount > 0,
                    "TableCount=" + (assemblyDrawingData?.TableCount ?? 0));
                Check("Drawing reader sees annotations", assemblyDrawingData != null && assemblyDrawingData.NoteCount > 0,
                    "NoteCount=" + (assemblyDrawingData?.NoteCount ?? 0));
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

        private static IFeature FindFeature(IModelDoc2 model, string name)
        {
            var feature = model?.FirstFeature() as IFeature;
            while (feature != null)
            {
                if (string.Equals(feature.Name, name, StringComparison.OrdinalIgnoreCase))
                    return feature;
                feature = feature.GetNextFeature() as IFeature;
            }
            return null;
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
