using DebugOpsMCP.Contracts;

namespace DebugOpsMCP.Core.Tools;

/// <summary>
/// Base interface for all debug tools
/// </summary>
public interface IDebugTool
{
    /// <summary>
    /// Handle a debug request and return the appropriate response
    /// </summary>
    Task<McpResponse> HandleAsync(McpRequest request);
}

/// <summary>
/// Tool for debug lifecycle operations (attach, launch, disconnect, terminate)
/// </summary>
public interface IDebugLifecycleTool : IDebugTool
{
}

/// <summary>
/// Tool for debug execution control (continue, pause, step)
/// </summary>
public interface IDebugExecutionTool : IDebugTool
{
}

/// <summary>
/// Tool for breakpoint management (set, remove, list)
/// </summary>
public interface IDebugBreakpointTool : IDebugTool
{
}

/// <summary>
/// Tool for runtime inspection (stack trace, variables, evaluation)
/// </summary>
public interface IDebugInspectionTool : IDebugTool
{
}

/// <summary>
/// Tool for thread management (list, select)
/// </summary>
public interface IDebugThreadTool : IDebugTool
{
}

/// <summary>
/// Tool for debug status queries
/// </summary>
public interface IDebugStatusTool : IDebugTool
{
}