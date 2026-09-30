namespace SwMateAI.Core.Tools
{
    /// <summary>
    /// Optional contract for tools that can undo only their own last action.
    /// This is safer than invoking SOLIDWORKS global Undo because unrelated
    /// user operations are never reverted by the Agent.
    /// </summary>
    public interface IUndoableSwTool
    {
        ToolResult Undo();
    }
}
