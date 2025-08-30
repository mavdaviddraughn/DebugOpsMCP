using DebugOpsMCP.Contracts;
using DebugOpsMCP.Contracts.Debug;
using DebugOpsMCP.Core.Debug;
using Microsoft.Extensions.Logging;

namespace DebugOpsMCP.Core.Tools;

/// <summary>
/// Handles debug lifecycle operations (attach, launch, disconnect, terminate)
/// </summary>
public class DebugLifecycleTool : IDebugLifecycleTool
{
    private readonly ILogger<DebugLifecycleTool> _logger;
    private readonly IDebugBridge _debugBridge;

    public DebugLifecycleTool(
        ILogger<DebugLifecycleTool> logger,
        IDebugBridge debugBridge)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _debugBridge = debugBridge ?? throw new ArgumentNullException(nameof(debugBridge));
    }

    public async Task<McpResponse> HandleAsync(McpRequest request)
    {
        return request.Method switch
        {
            "debug.attach" => await HandleAttachAsync((DebugAttachRequest)request),
            "debug.launch" => await HandleLaunchAsync((DebugLaunchRequest)request),
            "debug.disconnect" => await HandleDisconnectAsync(),
            "debug.terminate" => await HandleTerminateAsync(),
            _ => new McpErrorResponse
            {
                Error = new McpError
                {
                    Code = "METHOD_NOT_SUPPORTED",
                    Message = $"Method {request.Method} not supported by DebugLifecycleTool"
                }
            }
        };
    }

    private async Task<McpResponse> HandleAttachAsync(DebugAttachRequest request)
    {
        try
        {
            _logger.LogInformation("Attaching to process {ProcessId}", request.ProcessId);

            if (!_debugBridge.IsConnected)
            {
                var initialized = await _debugBridge.InitializeAsync();
                if (!initialized)
                {
                    return new McpErrorResponse
                    {
                        Error = new McpError
                        {
                            Code = "BRIDGE_INIT_FAILED",
                            Message = "Failed to initialize debug bridge"
                        }
                    };
                }
            }

            // TODO: Implement actual DAP attach request
            // For now, simulate a successful attach
            var session = new DebugSession
            {
                SessionId = Guid.NewGuid().ToString(),
                Status = "running",
                Capabilities = new DebugCapabilities
                {
                    SupportsBreakpoints = true,
                    SupportsConditionalBreakpoints = true,
                    SupportsEvaluateForHovers = true,
                    SupportsStepBack = false,
                    SupportsSetVariable = true
                }
            };

            _logger.LogInformation("Successfully attached to process {ProcessId}, session {SessionId}", 
                request.ProcessId, session.SessionId);

            return new DebugSessionResponse
            {
                Success = true,
                Result = session
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to attach to process {ProcessId}", request.ProcessId);
            return new McpErrorResponse
            {
                Error = new McpError
                {
                    Code = "ATTACH_FAILED",
                    Message = ex.Message
                }
            };
        }
    }

    private async Task<McpResponse> HandleLaunchAsync(DebugLaunchRequest request)
    {
        try
        {
            _logger.LogInformation("Launching program {Program}", request.Program);

            if (!_debugBridge.IsConnected)
            {
                var initialized = await _debugBridge.InitializeAsync();
                if (!initialized)
                {
                    return new McpErrorResponse
                    {
                        Error = new McpError
                        {
                            Code = "BRIDGE_INIT_FAILED",
                            Message = "Failed to initialize debug bridge"
                        }
                    };
                }
            }

            // TODO: Implement actual DAP launch request
            // For now, simulate a successful launch
            var session = new DebugSession
            {
                SessionId = Guid.NewGuid().ToString(),
                Status = "running",
                Capabilities = new DebugCapabilities
                {
                    SupportsBreakpoints = true,
                    SupportsConditionalBreakpoints = true,
                    SupportsEvaluateForHovers = true,
                    SupportsStepBack = false,
                    SupportsSetVariable = true
                }
            };

            _logger.LogInformation("Successfully launched program {Program}, session {SessionId}", 
                request.Program, session.SessionId);

            return new DebugSessionResponse
            {
                Success = true,
                Result = session
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to launch program {Program}", request.Program);
            return new McpErrorResponse
            {
                Error = new McpError
                {
                    Code = "LAUNCH_FAILED",
                    Message = ex.Message
                }
            };
        }
    }

    private async Task<McpResponse> HandleDisconnectAsync()
    {
        try
        {
            _logger.LogInformation("Disconnecting debug session");

            // TODO: Implement actual DAP disconnect request
            await Task.Delay(100); // Simulate disconnect time

            return new McpResponse
            {
                Success = true,
                Message = "Debug session disconnected"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to disconnect debug session");
            return new McpErrorResponse
            {
                Error = new McpError
                {
                    Code = "DISCONNECT_FAILED",
                    Message = ex.Message
                }
            };
        }
    }

    private async Task<McpResponse> HandleTerminateAsync()
    {
        try
        {
            _logger.LogInformation("Terminating debug session");

            // TODO: Implement actual DAP terminate request
            await Task.Delay(100); // Simulate terminate time

            return new McpResponse
            {
                Success = true,
                Message = "Debug session terminated"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to terminate debug session");
            return new McpErrorResponse
            {
                Error = new McpError
                {
                    Code = "TERMINATE_FAILED",
                    Message = ex.Message
                }
            };
        }
    }
}