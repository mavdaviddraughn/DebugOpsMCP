using DebugOpsMCP.Contracts;
using DebugOpsMCP.Core.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace DebugOpsMCP.Core.Hosting;

/// <summary>
/// Core MCP host that handles message routing and protocol management
/// </summary>
public class McpHost
{
    private readonly ILogger<McpHost> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly JsonSerializerOptions _jsonOptions;

    public McpHost(ILogger<McpHost> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    /// <summary>
    /// Process an incoming MCP request and return the response
    /// </summary>
    public async Task<string> ProcessRequestAsync(string requestJson)
    {
        try
        {
            _logger.LogDebug("Processing MCP request: {Request}", requestJson);
            var requestDoc = JsonDocument.Parse(requestJson);
            var root = requestDoc.RootElement;

            // Extract method if present
            string? method = null;
            if (root.TryGetProperty("method", out var methodProp) && methodProp.ValueKind == JsonValueKind.String)
            {
                method = methodProp.GetString();
            }

            if (string.IsNullOrEmpty(method))
            {
                // Legacy behavior: return plain MCP error response for callers that expect it
                return CreateErrorResponse("INVALID_REQUEST", "Missing or empty method property");
            }

            var response = await RouteRequestAsync(method, requestJson);

            // If the incoming request is a JSON-RPC request, wrap the response in a JSON-RPC envelope
            if (root.TryGetProperty("jsonrpc", out var jsonrpcProp) && jsonrpcProp.ValueKind == JsonValueKind.String && jsonrpcProp.GetString() == "2.0")
            {
                // Extract id if present (can be string or number)
                object? idValue = null;
                var hasId = false;
                if (root.TryGetProperty("id", out var idProp))
                {
                    hasId = true;
                    switch (idProp.ValueKind)
                    {
                        case JsonValueKind.String:
                            idValue = idProp.GetString();
                            break;
                        case JsonValueKind.Number:
                            if (idProp.TryGetInt64(out var longVal)) idValue = longVal;
                            else if (idProp.TryGetDouble(out var dblVal)) idValue = dblVal;
                            else idValue = idProp.GetRawText();
                            break;
                        case JsonValueKind.Null:
                            idValue = null;
                            break;
                        default:
                            idValue = idProp.GetRawText();
                            break;
                    }
                }

                // If it's an error response, map to JSON-RPC error
                if (response is McpErrorResponse errResp)
                {
                    var jsonRpcCode = MapMcpErrorCodeToJsonRpcInt(errResp.Error?.Code);
                    var envelope = new
                    {
                        jsonrpc = "2.0",
                        id = hasId ? idValue : null,
                        error = new
                        {
                            code = jsonRpcCode,
                            message = errResp.Error?.Message ?? string.Empty,
                            data = new
                            {
                                mcpCode = errResp.Error?.Code,
                                details = errResp.Error?.Details
                            }
                        }
                    };

                    var envelopeJson = JsonSerializer.Serialize(envelope, _jsonOptions);
                    _logger.LogDebug("MCP response: {Response}", envelopeJson);
                    return envelopeJson;
                }

                // Otherwise return result envelope where `result` contains a standard
                // shape used by clients: { success, data, message } where `data` is
                // the inner payload (the typed Result property) if present.
                object? dataValue = null;
                string? message = null;
                bool success = false;

                success = response.Success;
                message = response.Message;

                // Try to extract a strongly-typed Result property via reflection so
                // we return its raw value as `data` rather than the full wrapper.
                var resultProp = response.GetType().GetProperty("Result");
                if (resultProp != null)
                {
                    dataValue = resultProp.GetValue(response);
                }

                var resultEnvelope = new
                {
                    jsonrpc = "2.0",
                    id = hasId ? idValue : null,
                    result = new
                    {
                        success,
                        data = dataValue,
                        message
                    }
                };

                var resultJson = JsonSerializer.Serialize(resultEnvelope, _jsonOptions);
                _logger.LogDebug("MCP response: {Response}", resultJson);
                return resultJson;
            }

            // Non-JSON-RPC callers expect raw MCP response JSON. Serialize using the
            // runtime type so derived properties are preserved when the declared
            // variable type is the base McpResponse.
            var rawResponseJson = JsonSerializer.Serialize(response, response.GetType(), _jsonOptions);
            _logger.LogDebug("MCP response: {Response}", rawResponseJson);
            return rawResponseJson;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse MCP request JSON");
            return CreateErrorResponse("JSON_PARSE_ERROR", ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing MCP request");
            return CreateErrorResponse("INTERNAL_ERROR", "An unexpected error occurred");
        }
    }

    private async Task<McpResponse> RouteRequestAsync(string method, string requestJson)
    {
        return method switch
        {
            "health" => await ProcessHealthRequestAsync(),

            // Debug lifecycle
            "debug.attach" => await ProcessDebugRequestAsync<Contracts.Debug.DebugAttachRequest>(requestJson),
            "debug.launch" => await ProcessDebugRequestAsync<Contracts.Debug.DebugLaunchRequest>(requestJson),
            "debug.disconnect" => await ProcessDebugDisconnectAsync(),
            "debug.terminate" => await ProcessDebugTerminateAsync(),

            // Debug execution control
            "debug.continue" => await ProcessDebugRequestAsync<Contracts.Debug.DebugContinueRequest>(requestJson),
            "debug.pause" => await ProcessDebugRequestAsync<Contracts.Debug.DebugPauseRequest>(requestJson),
            "debug.step" => await ProcessDebugRequestAsync<Contracts.Debug.DebugStepRequest>(requestJson),

            // Debug breakpoints
            "debug.setBreakpoint" => await ProcessDebugRequestAsync<Contracts.Debug.DebugSetBreakpointRequest>(requestJson),
            "debug.removeBreakpoint" => await ProcessDebugRequestAsync<Contracts.Debug.DebugRemoveBreakpointRequest>(requestJson),
            "debug.listBreakpoints" => await ProcessDebugRequestAsync<Contracts.Debug.DebugListBreakpointsRequest>(requestJson),

            // Debug inspection
            "debug.getStackTrace" => await ProcessDebugRequestAsync<Contracts.Debug.DebugGetStackTraceRequest>(requestJson),
            "debug.getVariables" => await ProcessDebugRequestAsync<Contracts.Debug.DebugGetVariablesRequest>(requestJson),
            "debug.evaluate" => await ProcessDebugRequestAsync<Contracts.Debug.DebugEvaluateRequest>(requestJson),
            "debug.getThreads" => await ProcessDebugRequestAsync<Contracts.Debug.DebugGetThreadsRequest>(requestJson),
            "debug.selectThread" => await ProcessDebugRequestAsync<Contracts.Debug.DebugSelectThreadRequest>(requestJson),
            "debug.getStatus" => await ProcessDebugRequestAsync<Contracts.Debug.DebugGetStatusRequest>(requestJson),

            _ => new McpErrorResponse
            {
                Error = new McpError
                {
                    Code = "METHOD_NOT_FOUND",
                    Message = $"Unknown method: {method}"
                }
            }
        };
    }

    private async Task<McpResponse> ProcessHealthRequestAsync()
    {
        _logger.LogInformation("Health check requested");
        return await Task.FromResult(new McpResponse<string>
        {
            Success = true,
            Result = "DebugOpsMCP server is running"
        });
    }

    private async Task<McpResponse> ProcessDebugRequestAsync<T>(string requestJson) where T : McpRequest
    {
        try
        {
            var request = JsonSerializer.Deserialize<T>(requestJson, _jsonOptions);
            if (request == null)
            {
                return new McpErrorResponse
                {
                    Error = new McpError
                    {
                        Code = "INVALID_REQUEST",
                        Message = "Failed to deserialize request"
                    }
                };
            }

            // Get the appropriate tool for this request type
            var tool = GetDebugTool(request.Method);
            if (tool == null)
            {
                return new McpErrorResponse
                {
                    Error = new McpError
                    {
                        Code = "TOOL_NOT_FOUND",
                        Message = $"No tool registered for method {request.Method}"
                    }
                };
            }

            // Delegate to the tool
            return await tool.HandleAsync(request);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize debug request");
            return new McpErrorResponse
            {
                Error = new McpError
                {
                    Code = "JSON_PARSE_ERROR",
                    Message = ex.Message
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing debug request");
            return new McpErrorResponse
            {
                Error = new McpError
                {
                    Code = "TOOL_ERROR",
                    Message = ex.Message
                }
            };
        }
    }

    private async Task<McpResponse> ProcessDebugDisconnectAsync()
    {
        return await Task.FromResult(new McpErrorResponse
        {
            Error = new McpError
            {
                Code = "NOT_IMPLEMENTED",
                Message = "Debug disconnect is not yet implemented"
            }
        });
    }

    private async Task<McpResponse> ProcessDebugTerminateAsync()
    {
        return await Task.FromResult(new McpErrorResponse
        {
            Error = new McpError
            {
                Code = "NOT_IMPLEMENTED",
                Message = "Debug terminate is not yet implemented"
            }
        });
    }

    private string CreateErrorResponse(string code, string message)
    {
        var errorResponse = new McpErrorResponse
        {
            Error = new McpError
            {
                Code = code,
                Message = message
            }
        };

        return JsonSerializer.Serialize(errorResponse, _jsonOptions);
    }

    private IDebugTool? GetDebugTool(string method)
    {
        return method switch
        {
            "debug.attach" or "debug.launch" or "debug.disconnect" or "debug.terminate" =>
                _serviceProvider.GetService<IDebugLifecycleTool>(),
            "debug.continue" or "debug.pause" or "debug.step" =>
                _serviceProvider.GetService<IDebugExecutionTool>(),
            "debug.setBreakpoint" or "debug.removeBreakpoint" or "debug.listBreakpoints" =>
                _serviceProvider.GetService<IDebugBreakpointTool>(),
            "debug.getStackTrace" or "debug.getVariables" or "debug.evaluate" =>
                _serviceProvider.GetService<IDebugInspectionTool>(),
            "debug.getThreads" or "debug.selectThread" =>
                _serviceProvider.GetService<IDebugThreadTool>(),
            "debug.getStatus" =>
                _serviceProvider.GetService<IDebugStatusTool>(),
            _ => null
        };
    }

    private int MapMcpErrorCodeToJsonRpcInt(string? mcpCode)
    {
        // Map string error codes used by MCP to standard JSON-RPC numeric codes where reasonable
        if (string.IsNullOrEmpty(mcpCode)) return -32000; // Server error

        return mcpCode switch
        {
            "INVALID_REQUEST" => -32600,
            "METHOD_NOT_FOUND" => -32601,
            "JSON_PARSE_ERROR" => -32700,
            "TOOL_NOT_FOUND" => -32001,
            "TOOL_ERROR" => -32002,
            "NOT_IMPLEMENTED" => -32003,
            _ => -32000
        };
    }
}