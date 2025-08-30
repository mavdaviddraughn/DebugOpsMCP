using DebugOpsMCP.Contracts;
using DebugOpsMCP.Contracts.Debug;
using DebugOpsMCP.Core.Debug;
using Microsoft.Extensions.Logging;

namespace DebugOpsMCP.Core.Tools;

/// <summary>
/// Handles debug inspection operations (stack trace, variables, evaluation)
/// </summary>
public class DebugInspectionTool : IDebugInspectionTool
{
    private readonly ILogger<DebugInspectionTool> _logger;
    private readonly IDebugBridge _debugBridge;

    public DebugInspectionTool(
        ILogger<DebugInspectionTool> logger,
        IDebugBridge debugBridge)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _debugBridge = debugBridge ?? throw new ArgumentNullException(nameof(debugBridge));
    }

    public async Task<McpResponse> HandleAsync(McpRequest request)
    {
        return request.Method switch
        {
            "debug.getStackTrace" => await HandleGetStackTraceAsync((DebugGetStackTraceRequest)request),
            "debug.getVariables" => await HandleGetVariablesAsync((DebugGetVariablesRequest)request),
            "debug.evaluate" => await HandleEvaluateAsync((DebugEvaluateRequest)request),
            _ => new McpErrorResponse
            {
                Error = new McpError
                {
                    Code = "METHOD_NOT_SUPPORTED",
                    Message = $"Method {request.Method} not supported by DebugInspectionTool"
                }
            }
        };
    }

    private Task<McpResponse> HandleGetStackTraceAsync(DebugGetStackTraceRequest request)
    {
        try
        {
            _logger.LogInformation("Getting stack trace for thread {ThreadId}", request.ThreadId);

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

            // TODO: Send actual DAP stackTrace request
            // For now, return mock stack trace
            var mockStackTrace = new DebugStackTrace
            {
                Frames = new[]
                {
                    new DebugStackFrame
                    {
                        Id = "frame1",
                        Name = "OrderProcessor.CalculateTotal()",
                        Source = new DebugSource
                        {
                            Name = "OrderProcessor.cs",
                            Path = "C:\\MyApp\\OrderProcessor.cs"
                        },
                        Line = 42,
                        Column = 16
                    },
                    new DebugStackFrame
                    {
                        Id = "frame2",
                        Name = "OrderService.ProcessOrder(Order order)",
                        Source = new DebugSource
                        {
                            Name = "OrderService.cs",
                            Path = "C:\\MyApp\\OrderService.cs"
                        },
                        Line = 28,
                        Column = 12
                    }
                },
                TotalFrames = 2
            };

            _logger.LogInformation("Stack trace retrieved with {FrameCount} frames", mockStackTrace.Frames.Length);

            return Task.FromResult<McpResponse>(new DebugStackTraceResponse
            {
                Success = true,
                Result = mockStackTrace
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get stack trace");
            return Task.FromResult<McpResponse>(new McpErrorResponse
            {
                Error = new McpError
                {
                    Code = "STACK_TRACE_FAILED",
                    Message = ex.Message
                }
            });
        }
    }

    private Task<McpResponse> HandleGetVariablesAsync(DebugGetVariablesRequest request)
    {
        try
        {
            _logger.LogInformation("Getting variables for scope {ScopeId}, frame {FrameId}",
                request.ScopeId, request.FrameId);

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

            // TODO: Send actual DAP variables request
            // For now, return mock variables
            var mockVariables = new[]
            {
                new DebugVariable
                {
                    Name = "order",
                    Value = "{OrderId: 123, CustomerId: 456}",
                    Type = "Order",
                    VariablesReference = "var1"
                },
                new DebugVariable
                {
                    Name = "total",
                    Value = "null",
                    Type = "decimal?"
                },
                new DebugVariable
                {
                    Name = "items",
                    Value = "Count = 3",
                    Type = "List<OrderItem>",
                    VariablesReference = "var2",
                    IndexedVariables = 3
                }
            };

            _logger.LogInformation("Variables retrieved: {VariableCount} variables", mockVariables.Length);

            return Task.FromResult<McpResponse>(new DebugVariablesResponse
            {
                Success = true,
                Result = mockVariables
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get variables");
            return Task.FromResult<McpResponse>(new McpErrorResponse
            {
                Error = new McpError
                {
                    Code = "VARIABLES_FAILED",
                    Message = ex.Message
                }
            });
        }
    }

    private Task<McpResponse> HandleEvaluateAsync(DebugEvaluateRequest request)
    {
        try
        {
            _logger.LogInformation("Evaluating expression: {Expression}", request.Expression);

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

            if (string.IsNullOrWhiteSpace(request.Expression))
            {
                return Task.FromResult<McpResponse>(new McpErrorResponse
                {
                    Error = new McpError
                    {
                        Code = "INVALID_EXPRESSION",
                        Message = "Expression cannot be empty"
                    }
                });
            }

            // TODO: Send actual DAP evaluate request
            // For now, return mock evaluation result
            var mockResult = new DebugEvaluationResult
            {
                Result = request.Expression switch
                {
                    "order" => "{OrderId: 123, CustomerId: 456}",
                    "order.Items" => "null",
                    "order.Items?.Count" => "null",
                    "total" => "null",
                    _ => $"<evaluation of '{request.Expression}'>"
                },
                Type = "object"
            };

            _logger.LogInformation("Expression evaluated: {Expression} = {Result}",
                request.Expression, mockResult.Result);

            return Task.FromResult<McpResponse>(new DebugEvaluateResponse
            {
                Success = true,
                Result = mockResult
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to evaluate expression: {Expression}", request.Expression);
            return Task.FromResult<McpResponse>(new McpErrorResponse
            {
                Error = new McpError
                {
                    Code = "EVALUATE_FAILED",
                    Message = ex.Message
                }
            });
        }
    }
}