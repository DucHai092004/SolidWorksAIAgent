namespace SwMateAI.Core.Tools
{
    /// <summary>
    /// Encapsulates the outcome of a tool execution.
    /// Use <see cref="Success"/> or <see cref="Error"/> factory methods to construct instances.
    /// </summary>
    public class ToolResult
    {
        /// <summary>Whether the tool completed without error.</summary>
        public bool IsSuccess { get; private set; }

        /// <summary>Error message if <see cref="IsSuccess"/> is false. Null otherwise.</summary>
        public string ErrorMessage { get; private set; }

        /// <summary>
        /// The output data produced by the tool.
        /// Cast to the expected type in the caller (e.g. <see cref="Models.ModelInfo"/>).
        /// </summary>
        public object Data { get; private set; }

        private ToolResult() { }

        /// <summary>Creates a successful result containing the provided data.</summary>
        public static ToolResult Success(object data) =>
            new ToolResult { IsSuccess = true, Data = data };

        /// <summary>Creates a failed result with the provided error message.</summary>
        public static ToolResult Error(string message) =>
            new ToolResult { IsSuccess = false, ErrorMessage = message };

        /// <inheritdoc />
        public override string ToString() =>
            IsSuccess
                ? $"[OK] {Data}"
                : $"[ERR] {ErrorMessage}";
    }
}
