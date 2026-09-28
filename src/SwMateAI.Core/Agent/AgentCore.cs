using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using SolidWorks.Interop.sldworks;
using SwMateAI.Core.Tools;
using SwMateAI.Core.Tools.CAD;
using SwMateAI.Core.Tools.ModelReader;
using SwMateAI.Core.Tools.Assembly;
using SwMateAI.Core.Tools.Manufacturing;
using SwMateAI.Core.Common;
using SwMateAI.Core.Planning;
using SwMateAI.Core.Skills;

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
        private readonly SkillRegistry _skills;
        private readonly InMemoryAgentLogger _logger;
        private readonly AgentOrchestrator _orchestrator;

        /// <summary>
        /// Read-only view of registered tool names, for display or future planner use.
        /// </summary>
        public IEnumerable<string> RegisteredTools => _tools.Keys;
        public IEnumerable<string> RegisteredSkills => _skills.Names;
        public AgentState State => _orchestrator.State;
        public IReadOnlyList<string> AgentLogs => _logger.Entries;
        public IReadOnlyList<SkillMetadata> RegisteredSkillMetadata => _skills.GetMetadata();

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
            _skills = new SkillRegistry();
            _logger = new InMemoryAgentLogger();
            RegisterTools();
            _orchestrator = new AgentOrchestrator(
                new SolidWorksContextReader(_swApp),
                _skills,
                _logger,
                new SolidWorksResultChecker(_swApp));
        }

        // ─── Tool Registration ────────────────────────────────────────────────

        private void RegisterTools()
        {
            // Phase 1 tools
            Register(new GetModelInfoTool(_swApp));

            // Phase 2B CAD tools
            Register(new CreatePartTool(_swApp));
            Register(new CreateSketchTool(_swApp));
            Register(new CreateRectangleTool(_swApp));
            Register(new ExtrudeTool(_swApp));

            // Phase 2C CAD tools
            Register(new CreateCircleTool(_swApp));
            Register(new CutExtrudeTool(_swApp));
            Register(new CreatePlateWithHoleTool(_swApp));
            Register(new CreatePlateTool(_swApp));
            Register(new FilletPlateCornersTool(_swApp));
            Register(new ChamferPlateCornersTool(_swApp));
            Register(new AddDimensionTool(_swApp));
            Register(new ModifyDimensionTool(_swApp));

            // Phase 2 Model Understanding skills
            Register(new ReadFeatureTreeTool(_swApp));
            Register(new ReadFeaturesTool(_swApp));
            Register(new ReadFeatureDependenciesTool(_swApp));
            Register(new AnalyzeFeatureImpactTool(_swApp));
            Register(new ReadSketchesTool(_swApp));
            Register(new ReadDimensionsTool(_swApp));
            Register(new ReadMaterialTool(_swApp));
            Register(new ReadMassPropertiesTool(_swApp));
            Register(new ReadCustomPropertiesTool(_swApp));
            Register(new ReadSelectedObjectTool(_swApp));
            Register(new ReadBoundingBoxTool(_swApp));

            // Phase 3 Assembly readers
            Register(new ReadAssemblyTool(_swApp));
            Register(new ReadComponentsTool(_swApp));
            Register(new ReadMatesTool(_swApp));
            Register(new CheckInterferenceTool(_swApp));
            Register(new InsertComponentTool(_swApp));
            Register(new MoveComponentTool(_swApp));
            Register(new AddMateTool(_swApp));
            Register(new DeleteMateTool(_swApp));
            Register(new ReplaceComponentTool(_swApp));

            // Phase 4 Manufacturing Breakdown
            Register(new BuildManufacturingBreakdownTool(_swApp));
        }

        private void Register(ISwTool tool)
        {
            if (tool == null) throw new ArgumentNullException(nameof(tool));
            _tools[tool.Name] = tool;
            var metadata = Skills.SkillCatalog.ForTool(tool);
            _skills.Register(new ToolSkillAdapter(tool, metadata), metadata.Aliases);
        }

        public AgentContext ObserveContext() => _orchestrator.Observe();

        public ExecutionResult ExecutePlan(TaskPlan plan, bool confirmed = false) =>
            _orchestrator.ExecutePlan(plan, confirmed);

        public bool PlanRequiresConfirmation(TaskPlan plan)
        {
            if (plan == null) return false;
            foreach (var step in plan.Steps)
                if (_skills.TryGet(step.SkillName, out var skill) && skill.RequiresConfirmation) return true;
            return false;
        }

        public bool CanUndoLastAction => _orchestrator.CanUndo;
        public SkillResult UndoLastAction() => _orchestrator.UndoLast();

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
