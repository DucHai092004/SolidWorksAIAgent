using System;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;
using SwMateAI.Core.Models;

namespace SwMateAI.Core.Tools
{
    /// <summary>
    /// Phase 1 Tool: Retrieves information about the currently active SOLIDWORKS document.
    /// Reports connection status, document type, title, file path, and SW version.
    /// </summary>
    public class GetModelInfoTool : ISwTool
    {
        private readonly ISldWorks _swApp;

        /// <inheritdoc />
        public string Name => "GetModelInfo";

        /// <inheritdoc />
        public string Description =>
            "Returns information about the currently active SOLIDWORKS document " +
            "(type, title, file path) and the SOLIDWORKS connection status.";

        /// <summary>
        /// Initialises the tool with a reference to the SOLIDWORKS application.
        /// </summary>
        /// <param name="swApp">Live ISldWorks interface pointer. Must not be null.</param>
        public GetModelInfoTool(ISldWorks swApp)
        {
            _swApp = swApp ?? throw new ArgumentNullException(nameof(swApp));
        }

        /// <inheritdoc />
        public ToolResult Execute(Dictionary<string, object> parameters)
        {
            try
            {
                var info = new ModelInfo { IsConnected = true };

                // --- SW version ---
                info.SolidWorksVersion = _swApp.RevisionNumber();

                // --- Active document ---
                var doc = _swApp.IActiveDoc2;

                if (doc == null)
                {
                    info.HasActiveDocument = false;
                    info.DocumentType = "None";
                    return ToolResult.Success(info);
                }

                info.HasActiveDocument = true;
                info.DocumentTitle = doc.GetTitle();
                info.FilePath = doc.GetPathName();
                info.IsSaved = !string.IsNullOrEmpty(info.FilePath);

                // Determine document type via interface casting.
                // NOTE: IModelDoc2.GetType() cannot be called directly in C# because
                // it conflicts with object.GetType(). Interface casting is the correct approach.
                if (doc is IPartDoc)
                    info.DocumentType = "Part";
                else if (doc is IAssemblyDoc)
                    info.DocumentType = "Assembly";
                else if (doc is IDrawingDoc)
                    info.DocumentType = "Drawing";
                else
                    info.DocumentType = "Unknown";

                return ToolResult.Success(info);
            }
            catch (Exception ex)
            {
                return ToolResult.Error($"GetModelInfo failed: {ex.Message}");
            }
        }
    }
}
