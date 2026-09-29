using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SwMateAI.Core.Agent;
using SwMateAI.Core.Drawing;
using SwMateAI.Core.DrawingUnderstanding;
using SwMateAI.Core.Tools;

namespace SwMateAI.DrawingUnderstanding.IntegrationRunner
{
    internal static class Program
    {
        private static int _passed;
        private static int _failed;

        private static int Main()
        {
            string root = Path.Combine(Path.GetTempPath(),
                "SW-MATE_AI_DrawingUnderstanding_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(root);
            string partPath = Path.Combine(root, "DrawingReader_Block.SLDPRT");

            ISldWorks sw = null;
            IModelDoc2 part = null;
            IModelDoc2 drawing = null;

            try
            {
                sw = ConnectSolidWorks();
                sw.Visible = true;
                var agent = new AgentCore(sw);
                Check("ReadDrawing registered",
                    Contains(agent.RegisteredTools, "ReadDrawing"),
                    "ReadDrawing is not registered");

                Run(agent, "CreatePart");
                Run(agent, "CreateSketch");
                Run(agent, "CreateRectangle", new Dictionary<string, object>
                {
                    ["Width"] = 60d,
                    ["Height"] = 40d
                });
                Run(agent, "Extrude", new Dictionary<string, object> { ["Depth"] = 20d });

                part = sw.ActiveDoc as IModelDoc2;
                Check("Part created",
                    part != null && part.GetType() == (int)swDocumentTypes_e.swDocPART,
                    "Active document is not Part");
                int marked = MarkAllFeatureDimensionsForDrawing(part);
                Check("Dimensions marked", marked > 0, "No feature dimensions found");
                Save(part, partPath);

                Run(agent, "CreateDrawing");
                drawing = sw.ActiveDoc as IModelDoc2;
                Check("Drawing created",
                    drawing != null && drawing.GetType() == (int)swDocumentTypes_e.swDocDRAWING,
                    "Active document is not Drawing");

                Run(agent, "InsertStandardViews");
                Run(agent, "InsertIsometricView");
                Run(agent, "CreateSection", new Dictionary<string, object>
                {
                    ["Label"] = "A",
                    ["Direction"] = "Vertical"
                });
                Run(agent, "CreateDetail", new Dictionary<string, object>
                {
                    ["Label"] = "B",
                    ["ScaleNumerator"] = 2d,
                    ["ScaleDenominator"] = 1d
                });

                ToolResult dimensions = agent.ExecuteTool("InsertDimensions");
                Check("InsertDimensions", dimensions.IsSuccess, dimensions.ErrorMessage ?? string.Empty);
                var inserted = dimensions.Data as DrawingAnnotationResult;
                Check("Drawing dimensions inserted",
                    inserted != null && inserted.InsertedCount > 0,
                    "InsertedCount=" + (inserted?.InsertedCount ?? 0));

                ToolResult read = agent.ExecuteTool("ReadDrawing");
                Check("ReadDrawing", read.IsSuccess, read.ErrorMessage ?? string.Empty);
                var data = read.Data as DrawingUnderstandingResult;
                Check("Reader returns data", data != null, "No DrawingUnderstandingResult");
                Check("Reader sees sheets", data != null && data.SheetCount >= 1,
                    "SheetCount=" + (data?.SheetCount ?? 0));
                Check("Reader sees model views", data != null && data.ModelViewCount >= 4,
                    "ModelViewCount=" + (data?.ModelViewCount ?? 0));
                Check("Reader sees dimensions", data != null && data.DimensionCount > 0,
                    "DimensionCount=" + (data?.DimensionCount ?? 0));
                Check("Reader preserves active sheet metadata",
                    data != null && !string.IsNullOrWhiteSpace(data.ActiveSheetName),
                    data?.ActiveSheetName ?? string.Empty);
                Check("Reader view details are populated",
                    data != null && data.Views.Count >= data.ModelViewCount,
                    "Views=" + (data?.Views.Count ?? 0));
                Check("Reader semantic dimensions match count",
                    data != null && data.Dimensions.Count == data.DimensionCount,
                    $"Semantic={data?.Dimensions.Count ?? 0}, Count={data?.DimensionCount ?? 0}");

                bool semanticDimensionFound = false;
                if (data != null)
                {
                    foreach (var dimension in data.Dimensions)
                    {
                        if (!string.IsNullOrWhiteSpace(dimension.Name)
                            && !string.IsNullOrWhiteSpace(dimension.ViewName)
                            && !string.IsNullOrWhiteSpace(dimension.Type)
                            && !string.IsNullOrWhiteSpace(dimension.UnitKind))
                        {
                            semanticDimensionFound = true;
                            break;
                        }
                    }
                }
                Check("Reader returns semantic dimension details",
                    semanticDimensionFound,
                    "No dimension had Name/View/Type/UnitKind metadata");
            }
            catch (Exception ex)
            {
                _failed++;
                Console.WriteLine("[FATAL] " + ex);
            }
            finally
            {
                try { if (drawing != null) sw?.CloseDoc(drawing.GetTitle()); } catch { }
                try { if (part != null) sw?.CloseDoc(part.GetTitle()); } catch { }
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

        private static bool Contains(IEnumerable<string> values, string expected)
        {
            foreach (string value in values)
                if (string.Equals(value, expected, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static void Run(AgentCore agent, string tool, Dictionary<string, object> parameters = null)
        {
            ToolResult result = agent.ExecuteTool(tool, parameters);
            Check(tool, result.IsSuccess, result.ErrorMessage ?? Convert.ToString(result.Data));
        }

        private static void Save(IModelDoc2 model, string path)
        {
            int errors = 0, warnings = 0;
            bool ok = model.Extension.SaveAs(path,
                (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                (int)swSaveAsOptions_e.swSaveAsOptions_Silent,
                null, ref errors, ref warnings);
            Check("Save Part", ok && errors == 0, $"Errors={errors}, Warnings={warnings}");
        }

        private static void Check(string name, bool ok, string detail)
        {
            if (ok) { _passed++; Console.WriteLine("[PASS] " + name); }
            else { _failed++; Console.WriteLine("[FAIL] " + name + " :: " + detail); }
        }
    }
}
