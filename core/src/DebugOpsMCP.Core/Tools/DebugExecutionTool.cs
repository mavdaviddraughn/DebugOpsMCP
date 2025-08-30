using DebugOpsMCP.Contracts;
using DebugOpsMCP.Contracts.Debug;
using DebugOpsMCP.Core.Debug;
using DebugOpsMCP.Core.Hosting;
using Microsoft.Extensions.Logging;

namespace DebugOpsMCP.Core.Tools;

/// <summary>
/// Handles debug execution control (continue, pause, step)
/// </summary>
[McpMethod("debug.continue", typeof(DebugContinueRequest), "Continue execution from current breakpoint", "debug", "execution")]
[McpMethod("debug.pause", typeof(DebugPauseRequest), "Pause execution", "debug", "execution")]
[McpMethod("debug.step", typeof(DebugStepRequest), "Step through code (over/into/out)", "debug", "execution")]
public class DebugExecutionTool : IDebugExecutionTool
{
    private readonly ILogger<DebugExecutionTool> _logger;
    private readonly IDebugBridge _debugBridge;

    public DebugExecutionTool(
        ILogger<DebugExecutionTool> logger,
        IDebugBridge debugBridge)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _debugBridge = debugBridge ?? throw new ArgumentNullException(nameof(debugBridge));
    }

    public bool CanHandle(string method)
    {
        return method.StartsWith("debug.continue") || method.StartsWith("debug.pause") || method.StartsWith("debug.step");
    }

    public async Task<McpResponse> HandleAsync(McpRequest request)
    {
        return request.Method switch
        {
            "debug.continue" => await HandleContinueAsync((DebugContinueRequest)request),
            "debug.pause" => await HandlePauseAsync((DebugPauseRequest)request),
            "debug.step" => await HandleStepAsync((DebugStepRequest)request),
            _ => new McpErrorResponse
            {
                Error = new McpError
                {
                    Code = DebugErrorCodes.METHOD_NOT_FOUND,
                    Message = $"Method {request.Method} not supported by DebugExecutionTool"
                }
            }
        };
    }

    private async Task<McpResponse> HandleContinueAsync(DebugContinueRequest request)
    {
        try
        {
            _logger.LogInformation("Continuing execution for thread {ThreadId}", request.ThreadId);

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

            // Send actual DAP continue request
            var dapRequest = new DapContinueRequest
            {
                Arguments = new DapContinueArguments
                {
                    ThreadId = request.ThreadId ?? 1
                }
            };

            var dapResponse = await _debugBridge.SendRequestAsync<DapContinueRequest, DapContinueResponse>(dapRequest);

            _logger.LogInformation("Execution continued successfully on thread {ThreadId}", request.ThreadId);

            return new McpResponse<string>
            {
                Success = true,
                Result = dapResponse.Body?.AllThreadsContinued == true 
                    ? "All threads continued" 
                    : $"Thread {request.ThreadId} continued"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to continue execution");
            return new McpErrorResponse
            {
                Error = new McpError
                {
                    Code = DebugErrorCodes.CONTINUE_FAILED,
                    Message = ex.Message
                }
            };
        }
    }

    private async Task<McpResponse> HandlePauseAsync(DebugPauseRequest request)
    {
        try
        {
            _logger.LogInformation("Pausing execution");

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

            // Send actual DAP pause request
            var dapRequest = new DapPauseRequest
            {
                Arguments = new DapPauseArguments
                {
                    ThreadId = request.ThreadId ?? 1
                }
            };

            var dapResponse = await _debugBridge.SendRequestAsync<DapPauseRequest, DapPauseResponse>(dapRequest);

            _logger.LogInformation("Execution paused successfully on thread {ThreadId}", request.ThreadId);

            return new McpResponse<string>
            {
                Success = true,
                Result = $"Thread {request.ThreadId} paused"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to pause execution");
            return new McpErrorResponse
            {
                Error = new McpError
                {
                    Code = DebugErrorCodes.EXECUTION_FAILED,
                    Message = ex.Message
                }
            };
        }
    }

    private async Task<McpResponse> HandleStepAsync(DebugStepRequest request)
    {
        try
        {
            _logger.LogInformation("Stepping {StepType} for thread {ThreadId}",
                request.StepType, request.ThreadId);

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

            // Validate step type
            if (!IsValidStepType(request.StepType))
            {
                return new McpErrorResponse
                {
                    Error = new McpError
                    {
                        Code = DebugErrorCodes.INVALID_PARAMS,
                        Message = $"Invalid step type: {request.StepType}. Valid types: over, into, out, back"
                    }
                };
            }

            // Send appropriate DAP step request based on stepType
            DapRequest dapRequest = request.StepType?.ToLowerInvariant() switch
            {
                "into" => new DapStepInRequest
                {
                    Arguments = new DapStepInArguments { ThreadId = request.ThreadId ?? 1 }
                },
                "out" => new DapStepOutRequest
                {
                    Arguments = new DapStepOutArguments { ThreadId = request.ThreadId ?? 1 }
                },
                "over" or _ => new DapNextRequest
                {
                    Arguments = new DapNextArguments { ThreadId = request.ThreadId ?? 1 }
                }
            };

            // Send the appropriate request and await response
            DapResponse dapResponse = request.StepType?.ToLowerInvariant() switch
            {
                "into" => await _debugBridge.SendRequestAsync<DapStepInRequest, DapStepInResponse>((DapStepInRequest)dapRequest),
                "out" => await _debugBridge.SendRequestAsync<DapStepOutRequest, DapStepOutResponse>((DapStepOutRequest)dapRequest),
                "over" or _ => await _debugBridge.SendRequestAsync<DapNextRequest, DapNextResponse>((DapNextRequest)dapRequest)
            };

            _logger.LogInformation("Step {StepType} completed successfully on thread {ThreadId}", request.StepType, request.ThreadId);

            return new McpResponse<string>
            {
                Success = true,
                Result = $"Step {request.StepType} completed on thread {request.ThreadId}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to step {StepType}", request.StepType);
            return new McpErrorResponse
            {
                Error = new McpError
                {
                    Code = DebugErrorCodes.STEP_FAILED,
                    Message = ex.Message
                }
            };
        }
    }

    private static bool IsValidStepType(string stepType)
    {
        return stepType switch
        {
            "over" or "into" or "out" or "back" => true,
            _ => false
        };
    }
}