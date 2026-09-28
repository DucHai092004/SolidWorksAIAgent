using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SwMateAI.Core.Readers;

namespace SwMateAI.Core.Tools.Assembly
{
    public abstract class AssemblyReaderToolBase : SwToolBase
    {
        protected readonly SolidWorksAssemblyReader Reader;

        protected AssemblyReaderToolBase(ISldWorks swApp) : base(swApp)
        {
            Reader = new SolidWorksAssemblyReader(swApp);
        }

        public override bool CanExecute(out string reason)
        {
            reason = null;
            var model = SwApp.ActiveDoc as IModelDoc2;
            if (model == null) { reason = "No active SOLIDWORKS document."; return false; }
            if (model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
            { reason = "The active document must be an Assembly."; return false; }
            return true;
        }
    }
}
