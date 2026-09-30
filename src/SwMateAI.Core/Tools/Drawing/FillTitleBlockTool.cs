using System;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SwMateAI.Core.Drawing;

namespace SwMateAI.Core.Tools.Drawing
{
    /// <summary>
    /// Fills drawing custom properties commonly linked by sheet-format title blocks.
    /// It does not rewrite arbitrary sketch text; templates should link notes to these properties.
    /// </summary>
    public class FillTitleBlockTool : SwToolBase
    {
        private static readonly string[] Allowed =
        {
            "Title", "Drawing Number", "Revision", "Drawn By", "Checked By",
            "Approved By", "Material", "Description", "Project"
        };

        public FillTitleBlockTool(ISldWorks swApp) : base(swApp) { }
        public override string Name => "FillTitleBlock";
        public override string Description => "Writes standard Drawing custom properties used by linked title-block notes.";

        public override bool CanExecute(out string reason)
        {
            reason = null;
            var model = SwApp.ActiveDoc as IModelDoc2;
            if (model == null || model.GetType() != (int)swDocumentTypes_e.swDocDRAWING)
            { reason = "Open a Drawing before filling the title block."; return false; }
            return true;
        }

        public override ToolResult Execute(Dictionary<string, object> parameters)
        {
            try
            {
                var model = SwApp.ActiveDoc as IModelDoc2;
                var manager = model?.Extension?.get_CustomPropertyManager(string.Empty);
                if (manager == null) return ToolResult.Error("Could not access Drawing custom properties.");

                var result = new DrawingTitleBlockResult();
                foreach (string name in Allowed)
                {
                    if (!TryValue(parameters, name, out string value)) continue;
                    manager.Add3(name, (int)swCustomInfoType_e.swCustomInfoText, value,
                        (int)swCustomPropertyAddOption_e.swCustomPropertyReplaceValue);
                    result.PropertiesWritten++;
                    result.PropertyNames.Add(name);
                }

                if (result.PropertiesWritten == 0)
                    return ToolResult.Error("No supported title-block property values were supplied.");

                model.SetSaveFlag();
                model.EditRebuild3();
                return ToolResult.Success(result);
            }
            catch (Exception ex) { return ToolResult.Error("FillTitleBlock failed: " + ex.Message); }
        }

        private static bool TryValue(Dictionary<string, object> p, string name, out string value)
        {
            value = string.Empty;
            if (p == null) return false;
            if (!p.TryGetValue(name, out var raw))
            {
                string compact = name.Replace(" ", string.Empty);
                if (!p.TryGetValue(compact, out raw)) return false;
            }
            value = Convert.ToString(raw)?.Trim() ?? string.Empty;
            return value.Length > 0;
        }
    }
}
