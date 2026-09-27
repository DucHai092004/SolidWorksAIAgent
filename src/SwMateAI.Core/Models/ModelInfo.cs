namespace SwMateAI.Core.Models
{
    /// <summary>
    /// Represents information about the currently active SOLIDWORKS document
    /// and the SOLIDWORKS application connection state.
    /// </summary>
    public class ModelInfo
    {
        /// <summary>Whether the SOLIDWORKS application is connected.</summary>
        public bool IsConnected { get; set; }

        /// <summary>The SOLIDWORKS revision/version string (e.g. "29.0.5028" for SW2021).</summary>
        public string SolidWorksVersion { get; set; } = string.Empty;

        /// <summary>Whether there is an active (open) document in SOLIDWORKS.</summary>
        public bool HasActiveDocument { get; set; }

        /// <summary>
        /// The type of the active document: "Part", "Assembly", "Drawing", or "None".
        /// </summary>
        public string DocumentType { get; set; } = "None";

        /// <summary>The title of the active document (filename without path).</summary>
        public string DocumentTitle { get; set; } = string.Empty;

        /// <summary>The full file path of the active document. Empty if not yet saved.</summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>Whether the active document has been saved to disk.</summary>
        public bool IsSaved { get; set; }

        /// <summary>Human-readable summary of the model info state.</summary>
        public string Summary
        {
            get
            {
                if (!IsConnected)
                    return "SOLIDWORKS not connected.";
                if (!HasActiveDocument)
                    return $"Connected to SOLIDWORKS {SolidWorksVersion}. No active document.";
                return $"{DocumentType}: {DocumentTitle}" +
                       (IsSaved ? $"\n{FilePath}" : "\n(Unsaved document)");
            }
        }
    }
}
