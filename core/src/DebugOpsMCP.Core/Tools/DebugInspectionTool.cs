using DebugOpsMCP.Contracts;
using DebugOpsMCP.Contracts.Debug;
using DebugOpsMCP.Core.Debug;
using DebugOpsMCP.Core.Hosting;
using Microsoft.Extensions.Logging;

namespace DebugOpsMCP.Core.Tools;

/// <summary>
/// Handles debug inspection operations (stack trace, variables, evaluation)
/// </summary>
[McpMethod("debug.getStackTrace", typeof(DebugGetStackTraceRequest), "Get current call stack", "debug", "inspection")]
[McpMethod("debug.getVariables", typeof(DebugGetVariablesRequest), "Get variables in scope", "debug", "inspection")]
[McpMethod("debug.evaluate", typeof(DebugEvaluateRequest), "Evaluate expressions", "debug", "inspection")]
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

    public bool CanHandle(string method)
    {
        return method.StartsWith("debug.getStackTrace") || method.StartsWith("debug.getVariables") || method.StartsWith("debug.evaluate");
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
                    Code = DebugErrorCodes.METHOD_NOT_FOUND,
                    Message = $"Method {request.Method} not supported by DebugInspectionTool"
                }
            }
        };
    }

    private async Task<McpResponse> HandleGetStackTraceAsync(DebugGetStackTraceRequest request)
    {
        try
        {
            _logger.LogInformation("Getting stack trace for thread {ThreadId}", request.ThreadId);

            if (!_debugBridge.IsConnected)
            {
                return new McpErrorResponse
                {
                    Error = new McpError
                    {
                        Code = DebugErrorCodes.NO_DEBUG_SESSION,
                        Message = "No active debug session. Use debug.attach() or debug.launch() first."
                    }
                };
            }

            // Send actual DAP stackTrace request
            var dapRequest = new DapStackTraceRequest
            {
                Arguments = new DapStackTraceArguments
                {
                    ThreadId = request.ThreadId ?? 1,
                    StartFrame = request.StartFrame,
                    Levels = request.Levels
                }
            };

            var dapResponse = await _debugBridge.SendRequestAsync<DapStackTraceRequest, DapStackTraceResponse>(dapRequest);

            // Convert DAP response to our internal format
            var stackTrace = new DebugStackTrace
            {
                Frames = dapResponse.Body?.StackFrames?.Select(f => new DebugStackFrame
                {
                    Id = f.Id.ToString(),
                    Name = f.Name,
                    Source = f.Source != null ? new DebugSource
                    {
                        Name = f.Source.Name,
                        Path = f.Source.Path,
                        SourceReference = f.Source.SourceReference
                    } : null,
                    Line = f.Line,
                    Column = f.Column
                }).ToArray() ?? Array.Empty<DebugStackFrame>()
            };

            _logger.LogInformation("Stack trace retrieved with {FrameCount} frames", stackTrace.Frames.Length);

            return new DebugStackTraceResponse
            {
                Success = true,
                Result = stackTrace
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get stack trace");
            return new McpErrorResponse
            {
                Error = new McpError
                {
                    Code = DebugErrorCodes.STACK_TRACE_FAILED,
                    Message = ex.Message
                }
            };
        }
    }

    private async Task<McpResponse> HandleGetVariablesAsync(DebugGetVariablesRequest request)
    {
        try
        {
            _logger.LogInformation("Getting variables for scope {ScopeId}, frame {FrameId}",
                request.ScopeId, request.FrameId);

            if (!_debugBridge.IsConnected)
            {
                return new McpErrorResponse
                {
                    Error = new McpError
                    {
                        Code = DebugErrorCodes.NO_DEBUG_SESSION,
                        Message = "No active debug session. Use debug.attach() or debug.launch() first."
                    }
                };
            }

            // Send actual DAP variables request
            // Note: ScopeId in our request maps to variablesReference in DAP
            var dapRequest = new DapVariablesRequest
            {
                Arguments = new DapVariablesArguments
                {
                    VariablesReference = request.ScopeId != null ? int.Parse(request.ScopeId) : 0,
                    Filter = request.Filter
                }
            };

            var dapResponse = await _debugBridge.SendRequestAsync<DapVariablesRequest, DapVariablesResponse>(dapRequest);

            // Convert DAP response to our internal format
            var variables = dapResponse.Body?.Variables?.Select(v => new DebugVariable
            {
                Name = v.Name,
                Value = v.Value,
                Type = v.Type,
                VariablesReference = v.VariablesReference.ToString(),
                IndexedVariables = v.IndexedVariables,
                NamedVariables = v.NamedVariables
            }).ToArray() ?? Array.Empty<DebugVariable>();

            _logger.LogInformation("Variables retrieved: {VariableCount} variables", variables.Length);

            return new DebugVariablesResponse
            {
                Success = true,
                Result = variables
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get variables");
            return new McpErrorResponse
            {
                Error = new McpError
                {
                    Code = DebugErrorCodes.VARIABLES_FAILED,
                    Message = ex.Message
                }
            };
        }
    }

    private async Task<McpResponse> HandleEvaluateAsync(DebugEvaluateRequest request)
    {
        try
        {
            _logger.LogInformation("Evaluating expression: {Expression}", request.Expression);

            if (!_debugBridge.IsConnected)
            {
                return new McpErrorResponse
                {
                    Error = new McpError
                    {
                        Code = DebugErrorCodes.NO_DEBUG_SESSION,
                        Message = "No active debug session. Use debug.attach() or debug.launch() first."
                    }
                };
            }

            if (string.IsNullOrWhiteSpace(request.Expression))
            {
                return new McpErrorResponse
                {
                    Error = new McpError
                    {
                        Code = DebugErrorCodes.INVALID_PARAMS,
                        Message = "Expression cannot be empty"
                    }
                };
            }

            // Send actual DAP evaluate request
            var dapRequest = new DapEvaluateRequest
            {
                Arguments = new DapEvaluateArguments
                {
                    Expression = request.Expression,
                    FrameId = request.FrameId != null ? int.Parse(request.FrameId) : null,
                    Context = request.Context ?? "repl"
                }
            };

            var dapResponse = await _debugBridge.SendRequestAsync<DapEvaluateRequest, DapEvaluateResponse>(dapRequest);

            // Convert DAP response to our internal format
            var result = new DebugEvaluationResult
            {
                Result = dapResponse.Body?.Result ?? "<no result>",
                Type = dapResponse.Body?.Type,
                VariablesReference = dapResponse.Body?.VariablesReference.ToString(),
                IndexedVariables = dapResponse.Body?.IndexedVariables,
                NamedVariables = dapResponse.Body?.NamedVariables
            };

            _logger.LogInformation("Expression evaluated: {Expression} = {Result}",
                request.Expression, result.Result);

            return new DebugEvaluateResponse
            {
                Success = true,
                Result = result
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to evaluate expression: {Expression}", request.Expression);
            return new McpErrorResponse
            {
                Error = new McpError
                {
                    Code = DebugErrorCodes.EVALUATION_FAILED,
                    Message = ex.Message
                }
            };
        }
    }
}