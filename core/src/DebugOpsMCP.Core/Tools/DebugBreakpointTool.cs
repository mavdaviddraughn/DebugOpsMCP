using DebugOpsMCP.Contracts;
using DebugOpsMCP.Contracts.Debug;
using DebugOpsMCP.Core.Debug;
using Microsoft.Extensions.Logging;

namespace DebugOpsMCP.Core.Tools;

/// <summary>
/// Handles breakpoint operations (set, remove, list)
/// </summary>
public class DebugBreakpointTool : IDebugBreakpointTool
{
    private readonly ILogger<DebugBreakpointTool> _logger;
    private readonly IDebugBridge _debugBridge;
    private readonly Dictionary<string, DebugBreakpoint> _breakpoints = new();

    public DebugBreakpointTool(
        ILogger<DebugBreakpointTool> logger,
        IDebugBridge debugBridge)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _debugBridge = debugBridge ?? throw new ArgumentNullException(nameof(debugBridge));
    }

    public async Task<McpResponse> HandleAsync(McpRequest request)
    {
        return request.Method switch
        {
            "debug.setBreakpoint" => await HandleSetBreakpointAsync((DebugSetBreakpointRequest)request),
            "debug.removeBreakpoint" => await HandleRemoveBreakpointAsync((DebugRemoveBreakpointRequest)request),
            "debug.listBreakpoints" => await HandleListBreakpointsAsync(),
            _ => new McpErrorResponse
            {
                Error = new McpError
                {
                    Code = "METHOD_NOT_SUPPORTED",
                    Message = $"Method {request.Method} not supported by DebugBreakpointTool"
                }
            }
        };
    }

    private Task<McpResponse> HandleSetBreakpointAsync(DebugSetBreakpointRequest request)
    {
        try
        {
            _logger.LogInformation("Setting breakpoint at {File}:{Line}", request.File, request.Line);

            if (!_debugBridge.IsConnected)
            {
                return Task.FromResult<McpResponse>(new McpErrorResponse
                {
                    Error = new McpError
                    {
                        Code = "NO_DEBUG_SESSION",
                        Message = "No active debug session. Use debug.attach() or debug.launch() first."
                    }
                });
            }

            // TODO: Send actual DAP setBreakpoints request
            // For now, create a mock breakpoint
            var breakpointId = Guid.NewGuid().ToString();
            var breakpoint = new DebugBreakpoint
            {
                Id = breakpointId,
                File = request.File,
                Line = request.Line,
                Verified = true, // TODO: Get actual verification from DAP
                Condition = request.Condition,
                HitCondition = request.HitCondition
            };

            _breakpoints[breakpointId] = breakpoint;

            _logger.LogInformation("Breakpoint set successfully: {BreakpointId} at {File}:{Line}", 
                breakpointId, request.File, request.Line);

            return Task.FromResult<McpResponse>(new DebugBreakpointResponse
            {
                Success = true,
                Result = breakpoint
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set breakpoint at {File}:{Line}", request.File, request.Line);
            return Task.FromResult<McpResponse>(new McpErrorResponse
            {
                Error = new McpError
                {
                    Code = "BREAKPOINT_SET_FAILED",
                    Message = ex.Message
                }
            });
        }
    }

    private Task<McpResponse> HandleRemoveBreakpointAsync(DebugRemoveBreakpointRequest request)
    {
        try
        {
            _logger.LogInformation("Removing breakpoint {BreakpointId}", request.BreakpointId);

            if (!_debugBridge.IsConnected)
            {
                return Task.FromResult<McpResponse>(new McpErrorResponse
                {
                    Error = new McpError
                    {
                        Code = "NO_DEBUG_SESSION",
                        Message = "No active debug session. Use debug.attach() or debug.launch() first."
                    }
                });
            }

            if (!_breakpoints.TryGetValue(request.BreakpointId, out var breakpoint))
            {
                return Task.FromResult<McpResponse>(new McpErrorResponse
                {
                    Error = new McpError
                    {
                        Code = "BREAKPOINT_NOT_FOUND",
                        Message = $"Breakpoint {request.BreakpointId} not found"
                    }
                });
            }

            // TODO: Send actual DAP remove breakpoint request
            _breakpoints.Remove(request.BreakpointId);

            _logger.LogInformation("Breakpoint removed successfully: {BreakpointId}", request.BreakpointId);

            return Task.FromResult<McpResponse>(new McpResponse<string>
            {
                Success = true,
                Result = $"Breakpoint {request.BreakpointId} removed"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove breakpoint {BreakpointId}", request.BreakpointId);
            return Task.FromResult<McpResponse>(new McpErrorResponse
            {
                Error = new McpError
                {
                    Code = "BREAKPOINT_REMOVE_FAILED",
                    Message = ex.Message
                }
            });
        }
    }

    private Task<McpResponse> HandleListBreakpointsAsync()
    {
        try
        {
            _logger.LogDebug("Listing breakpoints");

            var breakpoints = _breakpoints.Values.ToArray();

            return Task.FromResult<McpResponse>(new DebugBreakpointsResponse
            {
                Success = true,
                Result = breakpoints
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list breakpoints");
            return Task.FromResult<McpResponse>(new McpErrorResponse
            {
                Error = new McpError
                {
                    Code = "BREAKPOINT_LIST_FAILED",
                    Message = ex.Message
                }
            });
        }
    }
}