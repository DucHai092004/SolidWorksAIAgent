using System;
using System.Collections.Generic;
using System.Globalization;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SwMateAI.Core.Tools.Assembly
{
    public abstract class AssemblyActionToolBase : SwToolBase
    {
        protected AssemblyActionToolBase(ISldWorks swApp) : base(swApp) { }

        public override bool CanExecute(out string reason)
        {
            reason = null;
            var model = SwApp.ActiveDoc as IModelDoc2;
            if (model == null) { reason = "No active SOLIDWORKS document."; return false; }
            if (model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
            { reason = "The active document must be an Assembly."; return false; }
            return true;
        }

        protected static string Text(Dictionary<string, object> input, string key)
        {
            return input != null && input.TryGetValue(key, out var value) ? Convert.ToString(value)?.Trim() ?? string.Empty : string.Empty;
        }

        protected static double Mm(Dictionary<string, object> input, string key)
        {
            string value = Text(input, key).Replace(',', '.');
            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
        }
    }
}
