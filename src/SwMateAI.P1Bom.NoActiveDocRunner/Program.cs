using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using SolidWorks.Interop.sldworks;
using SwMateAI.Core.Agent;
using SwMateAI.Core.Tools;

namespace SwMateAI.P1Bom.NoActiveDocRunner
{
    internal static class Program
    {
        private static int Main()
        {
            try
            {
                ISldWorks sw = new NullSolidWorksProxy().Instance;
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

                Console.WriteLine("[PASS] TC022 automated contract: no active CAD returns a safe tool error and Execute is not entered.");
                Console.WriteLine("[INFO] Manual UI verification remains: run the button once with SOLIDWORKS open and no document loaded.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[FAIL] TC022 fatal :: " + ex);
                return 1;
            }
        }

        private sealed class NullSolidWorksProxy : RealProxy
        {
            public NullSolidWorksProxy() : base(typeof(ISldWorks)) { }

            public ISldWorks Instance => (ISldWorks)GetTransparentProxy();

            public override IMessage Invoke(IMessage message)
            {
                var call = (IMethodCallMessage)message;
                object returnValue = null;

                if (call.MethodBase is MethodInfo method)
                {
                    Type returnType = method.ReturnType;
                    if (returnType.IsValueType)
                        returnValue = Activator.CreateInstance(returnType);
                }

                return new ReturnMessage(
                    returnValue,
                    new object[0],
                    0,
                    call.LogicalCallContext,
                    call);
            }
        }
    }
}
