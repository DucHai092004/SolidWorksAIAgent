using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SwMateAI.Core.Readers;

namespace SwMateAI.Core.Tools.ModelReader
{
    public abstract class ModelReaderToolBase : SwToolBase
    {
        protected readonly SolidWorksModelReader Reader;
        private readonly bool _requirePart;

        protected ModelReaderToolBase(ISldWorks swApp, bool requirePart = false) : base(swApp)
        {
            Reader = new SolidWorksModelReader(swApp);
            _requirePart = requirePart;
        }

        public override bool CanExecute(out string reason)
        {
            reason = null;
            var model = SwApp.ActiveDoc as IModelDoc2;
            if (model == null) { reason = "No active SOLIDWORKS document."; return false; }
            if (_requirePart && model.GetType() != (int)swDocumentTypes_e.swDocPART)
            { reason = "The active document must be a Part."; return false; }
            return true;
        }
    }
}
