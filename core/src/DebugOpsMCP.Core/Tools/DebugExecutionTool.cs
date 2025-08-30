using DebugOpsMCP.Contracts;
using DebugOpsMCP.Contracts.Debug;
using DebugOpsMCP.Core.Debug;
using Microsoft.Extensions.Logging;

namespace DebugOpsMCP.Core.Tools;

/// <summary>
/// Handles debug execution control (continue, pause, step)
/// </summary>
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
                    Code = "METHOD_NOT_SUPPORTED",
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

            // TODO: Send actual DAP continue request
            await Task.Delay(50); // Simulate DAP request time

            _logger.LogInformation("Execution continued successfully");

            return new McpResponse<string>
            {
                Success = true,
                Result = "Execution continued"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to continue execution");
            return new McpErrorResponse
            {
                Error = new McpError
                {
                    Code = "CONTINUE_FAILED",
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

            // TODO: Send actual DAP pause request
            await Task.Delay(50); // Simulate DAP request time

            _logger.LogInformation("Execution paused successfully");

            return new McpResponse<string>
            {
                Success = true,
                Result = "Execution paused"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to pause execution");
            return new McpErrorResponse
            {
                Error = new McpError
                {
                    Code = "PAUSE_FAILED",
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
                        Code = "INVALID_STEP_TYPE",
                        Message = $"Invalid step type: {request.StepType}. Valid types: over, into, out"
                    }
                };
            }

            // TODO: Send appropriate DAP step request based on stepType
            await Task.Delay(50); // Simulate DAP request time

            _logger.LogInformation("Step {StepType} completed successfully", request.StepType);

            return new McpResponse<string>
            {
                Success = true,
                Result = $"Step {request.StepType} completed"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to step {StepType}", request.StepType);
            return new McpErrorResponse
            {
                Error = new McpError
                {
                    Code = "STEP_FAILED",
                    Message = ex.Message
                }
            };
        }
    }

    private static bool IsValidStepType(string stepType)
    {
        return stepType switch
        {
            "over" or "into" or "out" => true,
            _ => false
        };
    }
}