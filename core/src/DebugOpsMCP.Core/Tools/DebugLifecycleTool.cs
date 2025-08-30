using DebugOpsMCP.Contracts;
using DebugOpsMCP.Contracts.Debug;
using DebugOpsMCP.Core.Debug;
using DebugOpsMCP.Core.Hosting;
using Microsoft.Extensions.Logging;

namespace DebugOpsMCP.Core.Tools;

/// <summary>
/// Handles debug lifecycle operations (attach, launch, disconnect, terminate)
/// </summary>
[McpMethod("debug.attach", typeof(DebugAttachRequest), "Attach to a running process for debugging", "debug", "lifecycle")]
[McpMethod("debug.launch", typeof(DebugLaunchRequest), "Launch a program for debugging", "debug", "lifecycle")]
[McpMethod("debug.disconnect", typeof(McpRequest), "Disconnect from current debug session", "debug", "lifecycle")]
[McpMethod("debug.terminate", typeof(McpRequest), "Terminate current debug session", "debug", "lifecycle")]
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

            // Send attach request through debug bridge
            var bridgeResponse = await _debugBridge.SendRequestAsync<DebugAttachRequest, DebugSessionResponse>(request);
            
            if (bridgeResponse.Success && bridgeResponse.Result != null)
            {
                _logger.LogInformation("Successfully attached to process {ProcessId}, session {SessionId}", 
                    request.ProcessId, bridgeResponse.Result.SessionId);
                    
                return bridgeResponse;
            }
            else
            {
                return new McpErrorResponse
                {
                    Error = new McpError
                    {
                        Code = "ATTACH_FAILED", 
                        Message = "Debug bridge attach request failed"
                    }
                };
            }
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

            // Send launch request through debug bridge  
            var bridgeResponse = await _debugBridge.SendRequestAsync<DebugLaunchRequest, DebugSessionResponse>(request);
            
            if (bridgeResponse.Success && bridgeResponse.Result != null)
            {
                _logger.LogInformation("Successfully launched program {Program}, session {SessionId}", 
                    request.Program, bridgeResponse.Result.SessionId);
                    
                return bridgeResponse;
            }
            else
            {
                return new McpErrorResponse
                {
                    Error = new McpError
                    {
                        Code = "LAUNCH_FAILED",
                        Message = "Debug bridge launch request failed"
                    }
                };
            }
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

            // Send disconnect request through debug bridge
            var disconnectRequest = new { action = "disconnect" };
            var bridgeResponse = await _debugBridge.SendRequestAsync<object, McpResponse<string>>(disconnectRequest);
            
            if (bridgeResponse.Success)
            {
                _logger.LogInformation("Debug session disconnected successfully");
                return bridgeResponse;
            }
            else
            {
                return new McpErrorResponse
                {
                    Error = new McpError
                    {
                        Code = "DISCONNECT_FAILED",
                        Message = "Debug bridge disconnect request failed"
                    }
                };
            }
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

            // Send terminate request through debug bridge
            var terminateRequest = new { action = "terminate" };
            var bridgeResponse = await _debugBridge.SendRequestAsync<object, McpResponse<string>>(terminateRequest);
            
            if (bridgeResponse.Success)
            {
                _logger.LogInformation("Debug session terminated successfully");
                return bridgeResponse;
            }
            else
            {
                return new McpErrorResponse
                {
                    Error = new McpError
                    {
                        Code = "TERMINATE_FAILED",
                        Message = "Debug bridge terminate request failed"
                    }
                };
            }
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