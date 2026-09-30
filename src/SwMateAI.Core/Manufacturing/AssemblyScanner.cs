using System;
using System.Collections.Generic;
using System.IO;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SwMateAI.Core.Manufacturing
{
    public class AssemblyScanner
    {
        private readonly ISldWorks _swApp;
        public int SuppressedSkipped { get; private set; }
        public AssemblyScanner(ISldWorks swApp) { _swApp = swApp; }

        public List<ScannedPartOccurrence> Scan()
        {
            var result = new List<ScannedPartOccurrence>();
            var model = _swApp.ActiveDoc as IModelDoc2;
            var assembly = model as IAssemblyDoc;
            if (model == null || assembly == null || model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
                return result;

            SuppressedSkipped = 0;
            var raw = assembly.GetComponents(false) as object[];
            if (raw == null) return result;

            foreach (var obj in raw)
            {
                var c = obj as IComponent2;
                if (c == null) continue;
                if (c.IsSuppressed()) { SuppressedSkipped++; continue; }

                string path = c.GetPathName() ?? string.Empty;
                var componentModel = c.GetModelDoc2() as IModelDoc2;
                bool isPart = componentModel != null
                    ? componentModel.GetType() == (int)swDocumentTypes_e.swDocPART
                    : path.EndsWith(".SLDPRT", StringComparison.OrdinalIgnoreCase);
                if (!isPart) continue;

                string config = c.ReferencedConfiguration ?? string.Empty;
                string modelTitle = componentModel?.GetTitle() ?? Path.GetFileName(path) ?? string.Empty;
                bool isVirtual = c.IsVirtual;
                string identity = !string.IsNullOrWhiteSpace(path)
                    ? path.ToUpperInvariant() + "|" + config.ToUpperInvariant()
                    : "VIRTUAL::" + modelTitle.ToUpperInvariant() + "|" + config.ToUpperInvariant();

                result.Add(new ScannedPartOccurrence
                {
                    IdentityKey = identity,
                    ComponentName = c.Name2 ?? string.Empty,
                    SourcePath = path,
                    ReferencedConfiguration = config,
                    ModelTitle = modelTitle,
                    IsVirtual = isVirtual,
                    IsLoaded = componentModel != null
                });
            }
            return result;
        }
    }
}
