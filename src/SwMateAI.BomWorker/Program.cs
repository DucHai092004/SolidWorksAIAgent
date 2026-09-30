using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SwMateAI.Core.BOM;
using SwMateAI.Core.Tools;
using SwMateAI.Core.Tools.BOM;

namespace SwMateAI.BomWorker
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            ISldWorks isolated = null;
            IModelDoc2 isolatedModel = null;
            try
            {
                string outputFolder = ParseOutputFolder(args);
                string assemblyPath;
                string configuration;
                if (!TryReadSourceAssembly(out assemblyPath, out configuration, out var sourceError))
                    return Fail(sourceError);

                Console.WriteLine("PROGRESS|SOURCE|" + Escape(assemblyPath));
                Console.WriteLine("PROGRESS|INSTANCE|STARTING");
                Console.Out.Flush();

                Type swType = Type.GetTypeFromProgID("SldWorks.Application");
                if (swType == null)
                    return Fail("SOLIDWORKS COM server is unavailable.");

                isolated = Activator.CreateInstance(swType) as ISldWorks;
                if (isolated == null)
                    return Fail("Could not start isolated SOLIDWORKS instance.");

                isolated.Visible = false;
                Console.WriteLine("PROGRESS|INSTANCE|READY");
                Console.Out.Flush();

                int openErrors = 0;
                int openWarnings = 0;
                int openOptions =
                    (int)swOpenDocOptions_e.swOpenDocOptions_Silent |
                    (int)swOpenDocOptions_e.swOpenDocOptions_ReadOnly;

                isolatedModel = isolated.OpenDoc6(
                    assemblyPath,
                    (int)swDocumentTypes_e.swDocASSEMBLY,
                    openOptions,
                    configuration ?? string.Empty,
                    ref openErrors,
                    ref openWarnings) as IModelDoc2;

                if (isolatedModel == null)
                    return Fail("Could not open Assembly in isolated SOLIDWORKS. Errors=" + openErrors + ", Warnings=" + openWarnings + ".");

                var parameters = new Dictionary<string, object>
                {
                    ["Mode"] = "LegacyFlat",
                    ["RespectChildDisplay"] = true,
                    ["ExportExcel"] = true,
                    ["ExportCsv"] = false,
                    ["CaptureImages"] = true,
                    ["ImageCaptureMode"] = "Render"
                };
                if (!string.IsNullOrWhiteSpace(outputFolder))
                    parameters["OutputFolder"] = outputFolder;

                Console.WriteLine("PROGRESS|BOM|RUNNING");
                Console.Out.Flush();

                var clock = Stopwatch.StartNew();
                ToolResult result = new CreateBomTool(isolated).Execute(parameters);
                clock.Stop();

                if (!result.IsSuccess)
                    return Fail(result.ErrorMessage ?? "BOM export failed.");

                var bom = result.Data as BomResult;
                if (bom == null)
                    return Fail("BOM export returned no result data.");

                Console.WriteLine(
                    "RESULT|OK|" + Escape(bom.ExcelPath) +
                    "|ITEMS=" + bom.Items.Count +
                    "|IMAGES=" + bom.CapturedImageCount +
                    "|MS=" + clock.ElapsedMilliseconds);
                return 0;
            }
            catch (Exception ex)
            {
                return Fail(ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                if (isolated != null)
                {
                    try
                    {
                        if (isolatedModel != null)
                            isolated.CloseDoc(isolatedModel.GetTitle());
                    }
                    catch { }
                    try { isolated.ExitApp(); } catch { }
                }

                ReleaseCom(isolatedModel);
                ReleaseCom(isolated);
            }
        }

        private static bool TryReadSourceAssembly(
            out string assemblyPath,
            out string configuration,
            out string error)
        {
            assemblyPath = string.Empty;
            configuration = string.Empty;
            error = string.Empty;
            object appObject = null;
            object modelObject = null;

            try
            {
                appObject = Marshal.GetActiveObject("SldWorks.Application");
                var sw = appObject as ISldWorks;
                var model = sw?.ActiveDoc as IModelDoc2;
                modelObject = model;

                if (model == null)
                {
                    error = "No active SOLIDWORKS document.";
                    return false;
                }
                if (model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
                {
                    error = "The active document must be an Assembly.";
                    return false;
                }

                assemblyPath = model.GetPathName() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(assemblyPath))
                {
                    error = "Save the active Assembly before exporting BOM with images.";
                    return false;
                }

                try
                {
                    var manager = model.ConfigurationManager;
                    configuration = manager?.ActiveConfiguration?.Name ?? string.Empty;
                }
                catch { configuration = string.Empty; }

                return true;
            }
            catch (Exception ex)
            {
                error = "Could not read active Assembly: " + ex.Message;
                return false;
            }
            finally
            {
                ReleaseCom(modelObject);
                ReleaseCom(appObject);
            }
        }

        private static string ParseOutputFolder(string[] args)
        {
            if (args == null) return string.Empty;
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], "--output", StringComparison.OrdinalIgnoreCase))
                    return args[i + 1]?.Trim() ?? string.Empty;
            }
            return string.Empty;
        }

        private static int Fail(string message)
        {
            Console.WriteLine("RESULT|ERROR|" + Escape(message));
            return 1;
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty)
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Replace("|", "/");
        }

        private static void ReleaseCom(object value)
        {
            if (value == null || !Marshal.IsComObject(value)) return;
            try { Marshal.FinalReleaseComObject(value); } catch { }
        }
    }
}
