using DebugOpsMCP.Contracts;
using DebugOpsMCP.Contracts.Debug;
using DebugOpsMCP.Core.Debug;
using DebugOpsMCP.Core.Hosting;
using Microsoft.Extensions.Logging;

namespace DebugOpsMCP.Core.Tools;

/// <summary>
/// Handles breakpoint operations (set, remove, list)
/// </summary>
[McpMethod("debug.setBreakpoint", typeof(DebugSetBreakpointRequest), "Set a breakpoint at specified location", "debug", "breakpoint")]
[McpMethod("debug.removeBreakpoint", typeof(DebugRemoveBreakpointRequest), "Remove a breakpoint by ID", "debug", "breakpoint")]
[McpMethod("debug.listBreakpoints", typeof(DebugListBreakpointsRequest), "List all active breakpoints", "debug", "breakpoint")]
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

    private async Task<McpResponse> HandleSetBreakpointAsync(DebugSetBreakpointRequest request)
    {
        try
        {
            _logger.LogInformation("Setting breakpoint at {File}:{Line}", request.File, request.Line);

            if (!_debugBridge.IsConnected)
            {
                return new McpErrorResponse
                {
                    Error = new McpError
                    {
                        Code = "NO_DEBUG_SESSION",
                        Message = "No active debug session. Use debug.attach() or debug.launch() first."
                    }
                };
            }

            // Send setBreakpoint request through debug bridge
            var bridgeResponse = await _debugBridge.SendRequestAsync<DebugSetBreakpointRequest, DebugBreakpointResponse>(request);
            
            if (bridgeResponse.Success && bridgeResponse.Result != null)
            {
                // Store the breakpoint locally for tracking
                _breakpoints[bridgeResponse.Result.Id] = bridgeResponse.Result;
                
                _logger.LogInformation("Breakpoint set successfully: {BreakpointId} at {File}:{Line}", 
                    bridgeResponse.Result.Id, request.File, request.Line);
                    
                return bridgeResponse;
            }
            else
            {
                return new McpErrorResponse
                {
                    Error = new McpError
                    {
                        Code = "BREAKPOINT_SET_FAILED",
                        Message = "Debug bridge setBreakpoint request failed"
                    }
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set breakpoint at {File}:{Line}", request.File, request.Line);
            return new McpErrorResponse
            {
                Error = new McpError
                {
                    Code = "BREAKPOINT_SET_FAILED",
                    Message = ex.Message
                }
            };
        }
    }

    private async Task<McpResponse> HandleRemoveBreakpointAsync(DebugRemoveBreakpointRequest request)
    {
        try
        {
            _logger.LogInformation("Removing breakpoint {BreakpointId}", request.BreakpointId);

            if (!_debugBridge.IsConnected)
            {
                return new McpErrorResponse
                {
                    Error = new McpError
                    {
                        Code = "NO_DEBUG_SESSION",
                        Message = "No active debug session. Use debug.attach() or debug.launch() first."
                    }
                };
            }

            if (!_breakpoints.TryGetValue(request.BreakpointId, out var breakpoint))
            {
                return new McpErrorResponse
                {
                    Error = new McpError
                    {
                        Code = "BREAKPOINT_NOT_FOUND",
                        Message = $"Breakpoint {request.BreakpointId} not found"
                    }
                };
            }

            // Send removeBreakpoint request through debug bridge
            var bridgeResponse = await _debugBridge.SendRequestAsync<DebugRemoveBreakpointRequest, McpResponse<string>>(request);
            
            if (bridgeResponse.Success)
            {
                // Remove from local tracking
                _breakpoints.Remove(request.BreakpointId);
                
                _logger.LogInformation("Breakpoint removed successfully: {BreakpointId}", request.BreakpointId);
                return bridgeResponse;
            }
            else
            {
                return new McpErrorResponse
                {
                    Error = new McpError
                    {
                        Code = "BREAKPOINT_REMOVE_FAILED",
                        Message = "Debug bridge removeBreakpoint request failed"
                    }
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove breakpoint {BreakpointId}", request.BreakpointId);
            return new McpErrorResponse
            {
                Error = new McpError
                {
                    Code = "BREAKPOINT_REMOVE_FAILED",
                    Message = ex.Message
                }
            };
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