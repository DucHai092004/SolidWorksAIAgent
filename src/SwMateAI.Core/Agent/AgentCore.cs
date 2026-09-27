using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using SolidWorks.Interop.sldworks;
using SwMateAI.Core.Tools;
using SwMateAI.Core.Tools.CAD;

namespace SwMateAI.Core.Agent
{
    /// <summary>
    /// Central coordinator for the SW-MATE AI agent.
    ///
    /// Phase 1: Maintains a registry of <see cref="ISwTool"/> instances and provides
    /// direct tool dispatch. No LLM integration yet.
    ///
    /// Phase 2A: Adds <see cref="CanExecute"/> precondition checking for tools that
    /// inherit <see cref="SwToolBase"/>, and exposes tool descriptions.
    ///
    /// Future phases: An LLM planner layer will sit above <see cref="ExecuteTool"/>,
    /// selecting tools and parameters based on natural-language user input.
    /// The tool registry and dispatch contract defined here will remain unchanged.
    /// </summary>
    public class AgentCore
    {
        private readonly ISldWorks _swApp;
        private readonly Dictionary<string, ISwTool> _tools;

        /// <summary>
        /// Read-only view of registered tool names, for display or future planner use.
        /// </summary>
        public IEnumerable<string> RegisteredTools => _tools.Keys;

        /// <summary>
        /// Read-only map of tool name → description, for display in UI or future planner.
        /// </summary>
        public IReadOnlyDictionary<string, string> ToolDescriptions
        {
            get
            {
                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var kvp in _tools)
                    dict[kvp.Key] = kvp.Value.Description;
                return new ReadOnlyDictionary<string, string>(dict);
            }
        }

        /// <summary>
        /// Initialises the AgentCore with an active SOLIDWORKS application reference
        /// and registers all tools.
        /// </summary>
        /// <param name="swApp">Live ISldWorks interface from the loaded add-in.</param>
        public AgentCore(ISldWorks swApp)
        {
            _swApp = swApp ?? throw new ArgumentNullException(nameof(swApp));
            _tools = new Dictionary<string, ISwTool>(StringComparer.OrdinalIgnoreCase);
            RegisterTools();
        }

        // ─── Tool Registration ────────────────────────────────────────────────

        private void RegisterTools()
        {
            // Phase 1 tools
            Register(new GetModelInfoTool(_swApp));

            // Phase 2B CAD tools
            Register(new CreatePartTool(_swApp));
        }

        private void Register(ISwTool tool)
        {
            if (tool == null) throw new ArgumentNullException(nameof(tool));
            _tools[tool.Name] = tool;
        }

        // ─── Tool Dispatch ────────────────────────────────────────────────────

        /// <summary>
        /// Executes the named tool with the provided parameters.
        ///
        /// If the tool inherits from <see cref="SwToolBase"/>, <see cref="SwToolBase.CanExecute"/>
        /// is checked first. A failed precondition returns <see cref="ToolResult.Error"/>
        /// without calling <see cref="ISwTool.Execute"/>.
        ///
        /// Phase 1/2A: Direct dispatch — no planner or LLM routing.
        /// </summary>
        /// <param name="toolName">Case-insensitive tool name (e.g. "GetModelInfo").</param>
        /// <param name="parameters">Optional parameters dictionary. Pass null for no parameters.</param>
        /// <returns>
        /// A <see cref="ToolResult"/> indicating success or failure.
        /// Never returns null.
        /// </returns>
        public ToolResult ExecuteTool(string toolName, Dictionary<string, object> parameters = null)
        {
            if (string.IsNullOrWhiteSpace(toolName))
                return ToolResult.Error("Tool name must not be empty.");

            if (!_tools.TryGetValue(toolName, out var tool))
                return ToolResult.Error($"Tool '{toolName}' is not registered. " +
                                        $"Available tools: {string.Join(", ", _tools.Keys)}");

            // Precondition check — only for tools that opt in via SwToolBase.
            if (tool is SwToolBase baseTool)
            {
                if (!baseTool.CanExecute(out var reason))
                    return ToolResult.Error($"Tool '{toolName}' cannot execute: {reason}");
            }

            return tool.Execute(parameters ?? new Dictionary<string, object>());
        }

        // ─── Future extension point ───────────────────────────────────────────
        // public async Task<string> ProcessNaturalLanguage(string userMessage) { ... }
    }
}
