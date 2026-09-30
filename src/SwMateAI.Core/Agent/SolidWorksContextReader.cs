using System;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SwMateAI.Core.Agent
{
    public class SolidWorksContextReader
    {
        private readonly ISldWorks _swApp;

        public SolidWorksContextReader(ISldWorks swApp)
        {
            _swApp = swApp;
        }

        public AgentContext Read()
        {
            var ctx = new AgentContext
            {
                IsConnected = _swApp != null,
                ObservedAt = DateTime.Now
            };

            if (_swApp == null)
                return ctx;

            try
            {
                ctx.SolidWorksVersion = _swApp.RevisionNumber();
            }
            catch
            {
                ctx.SolidWorksVersion = string.Empty;
            }

            var doc = _swApp.ActiveDoc as IModelDoc2;
            if (doc == null)
                doc = _swApp.IActiveDoc2;

            if (doc == null)
                return ctx;

            ctx.HasActiveDocument = true;

            try
            {
                ctx.DocumentName = doc.GetTitle() ?? string.Empty;
            }
            catch
            {
                ctx.DocumentName = string.Empty;
            }

            try
            {
                ctx.DocumentPath = doc.GetPathName() ?? string.Empty;
            }
            catch
            {
                ctx.DocumentPath = string.Empty;
            }

            try
            {
                switch ((swDocumentTypes_e)doc.GetType())
                {
                    case swDocumentTypes_e.swDocPART:
                        ctx.DocumentType = "Part";
                        break;
                    case swDocumentTypes_e.swDocASSEMBLY:
                        ctx.DocumentType = "Assembly";
                        break;
                    case swDocumentTypes_e.swDocDRAWING:
                        ctx.DocumentType = "Drawing";
                        break;
                    default:
                        ctx.DocumentType = "Unknown";
                        break;
                }
            }
            catch
            {
                ctx.DocumentType = "Unknown";
            }

            try
            {
                var cfg = doc.ConfigurationManager?.ActiveConfiguration;
                if (cfg != null)
                    ctx.ActiveConfiguration = cfg.Name;
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
