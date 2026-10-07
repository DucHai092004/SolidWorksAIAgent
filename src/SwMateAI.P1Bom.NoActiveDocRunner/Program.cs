using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SwMateAI.Core.Agent;
using SwMateAI.Core.Tools;

namespace SwMateAI.P1Bom.NoActiveDocRunner
{
    internal static class Program
    {
        private static int Main()
        {
            ISldWorks sw = null;
            try
            {
                Type progId = Type.GetTypeFromProgID("SldWorks.Application", true);
                sw = (ISldWorks)Activator.CreateInstance(progId);
                sw.Visible = false;

                if (sw.ActiveDoc != null)
                {
                    Console.WriteLine("[FAIL] TC022 isolated SOLIDWORKS session unexpectedly has an active document.");
                    return 1;
                }

                var agent = new AgentCore(sw);
                ToolResult result = agent.ExecuteTool("CreateBOM", new Dictionary<string, object>());

                bool safeFailure = result != null &&
                                   !result.IsSuccess &&
                                   !string.IsNullOrWhiteSpace(result.ErrorMessage) &&
                                   result.ErrorMessage.IndexOf("No active SOLIDWORKS document", StringComparison.OrdinalIgnoreCase) >= 0;

                Console.WriteLine("IsSuccess=" + (result?.IsSuccess.ToString() ?? "<null>") +
                                  " Error=" + (result?.ErrorMessage ?? "<null>"));

                if (!safeFailure)
                {
                    Console.WriteLine("[FAIL] TC022 CreateBOM did not return the expected safe no-document error.");
                    return 1;
                }

                Console.WriteLine("[PASS] TC022 no active CAD returns a safe tool error without touching the user session.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[FAIL] TC022 fatal :: " + ex);
                return 1;
            }
            finally
            {
                if (sw != null)
                {
                    try { sw.ExitApp(); } catch { }
                    try { Marshal.FinalReleaseComObject(sw); } catch { }
                }
            }
        }
    }
}
