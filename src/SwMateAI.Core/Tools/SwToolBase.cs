using System.Collections.Generic;
using SolidWorks.Interop.sldworks;

namespace SwMateAI.Core.Tools
{
    /// <summary>
    /// Optional abstract base class for SW-MATE AI tools that hold an
    /// <see cref="ISldWorks"/> reference and support precondition validation
    /// via <see cref="CanExecute"/>.
    ///
    /// Inheritance is NOT required — tools may implement <see cref="ISwTool"/>
    /// directly. This base class is provided as a convenience for CAD tools
    /// that need to validate context (e.g., "a Part must be active") before
    /// executing a potentially destructive operation.
    ///
    /// Phase 2A: Infrastructure only. No CAD tools yet.
    /// </summary>
    public abstract class SwToolBase : ISwTool
    {
        /// <summary>
        /// Live ISldWorks application reference. Available to all subclasses.
        /// </summary>
        protected readonly ISldWorks SwApp;

        /// <summary>
        /// Initialises the base with a SOLIDWORKS application reference.
        /// </summary>
        /// <param name="swApp">Live ISldWorks pointer from the loaded add-in.</param>
        protected SwToolBase(ISldWorks swApp)
        {
            SwApp = swApp;
        }

        /// <inheritdoc />
        public abstract string Name { get; }

        /// <inheritdoc />
        public abstract string Description { get; }

        /// <summary>
        /// Validates whether this tool can execute in the current SOLIDWORKS context.
        ///
        /// Default implementation always returns <c>true</c> (no preconditions).
        /// Override in subclasses to validate document type, selection state, etc.
        /// </summary>
        /// <param name="reason">
        /// When this method returns <c>false</c>, contains a human-readable explanation
        /// of why execution is not possible. <c>null</c> when returning <c>true</c>.
        /// </param>
        /// <returns><c>true</c> if the tool may proceed; <c>false</c> otherwise.</returns>
        public virtual bool CanExecute(out string reason)
        {
            reason = null;
            return true;
        }

        /// <inheritdoc />
        public abstract ToolResult Execute(Dictionary<string, object> parameters);
    }
}
