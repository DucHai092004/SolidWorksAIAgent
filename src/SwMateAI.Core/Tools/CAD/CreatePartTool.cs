using System;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SwMateAI.Core.Tools.CAD
{
    /// <summary>
    /// Phase 2B CAD Tool: Creates a new SOLIDWORKS Part document
    /// using the user's configured default Part template.
    /// </summary>
    public class CreatePartTool : SwToolBase
    {
        public override string Name => "CreatePart";

        public override string Description =>
            "Creates a new SOLIDWORKS Part document using the configured default Part template.";

        public CreatePartTool(ISldWorks swApp) : base(swApp)
        {
        }

        public override bool CanExecute(out string reason)
        {
            reason = null;

            try
            {
                var templatePath = SwApp.GetUserPreferenceStringValue(
                    (int)swUserPreferenceStringValue_e.swDefaultTemplatePart);

                if (string.IsNullOrWhiteSpace(templatePath))
                {
                    reason = "No default Part template is configured in SOLIDWORKS.";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                reason = $"Unable to read the default Part template: {ex.Message}";
                return false;
            }
        }

        public override ToolResult Execute(Dictionary<string, object> parameters)
        {
            try
            {
                var templatePath = SwApp.GetUserPreferenceStringValue(
                    (int)swUserPreferenceStringValue_e.swDefaultTemplatePart);

                if (string.IsNullOrWhiteSpace(templatePath))
                    return ToolResult.Error("No default Part template is configured in SOLIDWORKS.");

                var document = SwApp.NewDocument(templatePath, 0, 0, 0) as IModelDoc2;

                if (document == null)
                    return ToolResult.Error("SOLIDWORKS did not create the new Part document.");

                return ToolResult.Success(
                    $"Part created successfully: {document.GetTitle()}");
            }
            catch (Exception ex)
            {
                return ToolResult.Error($"CreatePart failed: {ex.Message}");
            }
        }
    }
}
