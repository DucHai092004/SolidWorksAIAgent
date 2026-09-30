using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SwMateAI.Core.Drawing;

namespace SwMateAI.Core.Tools.Drawing
{
    public class InsertDrawingBomTool : SwToolBase
    {
        public InsertDrawingBomTool(ISldWorks swApp) : base(swApp) { }
        public override string Name => "InsertDrawingBOM";
        public override string Description => "Inserts a native SOLIDWORKS BOM table into the active Drawing.";

        public override bool CanExecute(out string reason)
        {
            reason = null;
            var model = SwApp.ActiveDoc as IModelDoc2;
            if (model == null || model.GetType() != (int)swDocumentTypes_e.swDocDRAWING)
            { reason = "Open a Drawing before inserting a BOM."; return false; }
            return true;
        }

        public override ToolResult Execute(Dictionary<string, object> parameters)
        {
            try
            {
                var model = SwApp.ActiveDoc as IModelDoc2;
                var drawing = model as IDrawingDoc;
                var view = (drawing?.GetFirstView() as IView)?.GetNextView() as IView;
                if (view == null) return ToolResult.Error("No model drawing view was found.");

                string template = Text(parameters, "TemplatePath");
                if (template.Length == 0) template = FindDefaultBomTemplate();
                if (template.Length == 0 || !File.Exists(template))
                    return ToolResult.Error("No BOM template was found. Provide TemplatePath to a .sldbomtbt file.");

                string config = view.ReferencedConfiguration ?? string.Empty;

                var annotation = view.InsertBomTable4(
                    true, 0, 0,
                    (int)swBOMConfigurationAnchorType_e.swBOMConfigurationAnchor_TopRight,
                    (int)swBomType_e.swBomType_TopLevelOnly,
                    config,
                    template,
                    false,
                    (int)swNumberingType_e.swIndentedBOMNotSet,
                    false);
                if (annotation == null) return ToolResult.Error("SOLIDWORKS did not create the BOM table.");
                model.EditRebuild3();

                return ToolResult.Success(new DrawingBomResult
                {
                    ViewName = view.GetName2(),
                    TemplatePath = template,
                    Configuration = config
                });
            }
            catch (Exception ex) { return ToolResult.Error("InsertDrawingBOM failed: " + ex.Message); }
        }

        private string FindDefaultBomTemplate()
        {
            string locations = SwApp.GetUserPreferenceStringValue(
                (int)swUserPreferenceStringValue_e.swFileLocationsBOMTemplates) ?? string.Empty;
            foreach (string folder in locations.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string f = folder.Trim().Trim('"');
                if (!Directory.Exists(f)) continue;
                string standard = Directory.GetFiles(f, "bom-standard.sldbomtbt").FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(standard)) return standard;
                string any = Directory.GetFiles(f, "*.sldbomtbt").FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(any)) return any;
            }
            return string.Empty;
        }

        private static string Text(Dictionary<string, object> p, string key) =>
            p != null && p.TryGetValue(key, out var v) ? Convert.ToString(v)?.Trim() ?? string.Empty : string.Empty;
    }
}
