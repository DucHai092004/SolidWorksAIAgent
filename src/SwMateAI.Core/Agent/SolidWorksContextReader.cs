using System;
using SolidWorks.Interop.sldworks;

namespace SwMateAI.Core.Agent
{
    public class SolidWorksContextReader
    {
        private readonly ISldWorks _swApp;
        public SolidWorksContextReader(ISldWorks swApp) { _swApp = swApp; }

        public AgentContext Read()
        {
            var ctx = new AgentContext { IsConnected = _swApp != null, ObservedAt = DateTime.Now };
            if (_swApp == null) return ctx;

            ctx.SolidWorksVersion = _swApp.RevisionNumber();
            var doc = _swApp.IActiveDoc2;
            if (doc == null) return ctx;

            ctx.HasActiveDocument = true;
            ctx.DocumentName = doc.GetTitle();
            ctx.DocumentType = doc is IPartDoc ? "Part" : doc is IAssemblyDoc ? "Assembly" : doc is IDrawingDoc ? "Drawing" : "Unknown";

            try
            {
                var cfg = doc.ConfigurationManager?.ActiveConfiguration;
                if (cfg != null) ctx.ActiveConfiguration = cfg.Name;
            }
            catch { }

            try
            {
                var sel = doc.SelectionManager as ISelectionMgr;
                if (sel != null)
                {
                    ctx.SelectedObjectCount = sel.GetSelectedObjectCount2(-1);
                    for (int i = 1; i <= ctx.SelectedObjectCount; i++)
                        ctx.SelectedObjectTypes.Add(sel.GetSelectedObjectType3(i, -1));
                }
            }
            catch { }

            return ctx;
        }
    }
}
