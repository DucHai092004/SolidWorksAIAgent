using System.Collections.Generic;

namespace SwMateAI.Core.Tools
{
    /// <summary>
    /// Defines the contract for all SW-MATE AI tools.
    /// Tools are discrete, parameterised operations that interact with SOLIDWORKS.
    /// Phase 1: Tools execute synchronously.
    /// Future phases: Tools will be dispatched by the AI agent/planner.
    /// </summary>
    public interface ISwTool
    {
        /// <summary>Unique tool name used for registration and dispatch.</summary>
        string Name { get; }

        /// <summary>Human-readable description of what this tool does.</summary>
        string Description { get; }

        /// <summary>
        /// Executes the tool with the provided parameters.
        /// </summary>
        /// <param name="parameters">Optional key/value parameters. May be empty but never null.</param>
        /// <returns>A <see cref="ToolResult"/> indicating success or failure, plus output data.</returns>
        ToolResult Execute(Dictionary<string, object> parameters);
    }
}
