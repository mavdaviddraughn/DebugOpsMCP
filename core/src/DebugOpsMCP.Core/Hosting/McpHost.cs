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
    private readonly IMcpToolRegistry _toolRegistry;
    private readonly JsonSerializerOptions _jsonOptions;

    public McpHost(ILogger<McpHost> logger, IServiceProvider serviceProvider, IMcpToolRegistry toolRegistry)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _toolRegistry = toolRegistry;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
        
        // Initialize the tool registry
        _toolRegistry.RegisterTools();
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
        if (method == "health")
        {
            return await ProcessHealthRequestAsync();
        }

        // Use tool registry to route debug requests
        var tool = _toolRegistry.GetTool(method);
        if (tool == null)
        {
            return new McpErrorResponse
            {
                Error = new McpError
                {
                    Code = DebugErrorCodes.METHOD_NOT_FOUND,
                    Message = $"Unknown method: {method}"
                }
            };
        }

        try
        {
            // Parse the request using the method's expected type
            var requestDoc = JsonDocument.Parse(requestJson);
            var request = CreateRequestObject(method, requestDoc);
            
            if (request == null)
            {
                return new McpErrorResponse
                {
                    Error = new McpError
                    {
                        Code = DebugErrorCodes.INVALID_REQUEST,
                        Message = "Failed to parse request for method: " + method
                    }
                };
            }

            return await tool.HandleAsync(request);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse request JSON for method {Method}", method);
            return new McpErrorResponse
            {
                Error = new McpError
                {
                    Code = DebugErrorCodes.INVALID_REQUEST,
                    Message = ex.Message
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing request for method {Method}", method);
            return new McpErrorResponse
            {
                Error = new McpError
                {
                    Code = DebugErrorCodes.INTERNAL_ERROR,
                    Message = ex.Message
                }
            };
        }
    }

    private McpRequest? CreateRequestObject(string method, JsonDocument requestDoc)
    {
        try
        {
            var json = requestDoc.RootElement.GetRawText();
            
            return method switch
            {
                // Debug lifecycle
                "debug.attach" => JsonSerializer.Deserialize<Contracts.Debug.DebugAttachRequest>(json, _jsonOptions),
                "debug.launch" => JsonSerializer.Deserialize<Contracts.Debug.DebugLaunchRequest>(json, _jsonOptions),
                "debug.disconnect" => JsonSerializer.Deserialize<Contracts.Debug.DebugDisconnectRequest>(json, _jsonOptions),
                "debug.terminate" => JsonSerializer.Deserialize<Contracts.Debug.DebugTerminateRequest>(json, _jsonOptions),
                
                // Debug execution control
                "debug.continue" => JsonSerializer.Deserialize<Contracts.Debug.DebugContinueRequest>(json, _jsonOptions),
                "debug.pause" => JsonSerializer.Deserialize<Contracts.Debug.DebugPauseRequest>(json, _jsonOptions),
                "debug.step" => JsonSerializer.Deserialize<Contracts.Debug.DebugStepRequest>(json, _jsonOptions),
                
                // Debug breakpoints
                "debug.setBreakpoint" => JsonSerializer.Deserialize<Contracts.Debug.DebugSetBreakpointRequest>(json, _jsonOptions),
                "debug.removeBreakpoint" => JsonSerializer.Deserialize<Contracts.Debug.DebugRemoveBreakpointRequest>(json, _jsonOptions),
                "debug.listBreakpoints" => JsonSerializer.Deserialize<Contracts.Debug.DebugListBreakpointsRequest>(json, _jsonOptions),
                
                // Debug inspection
                "debug.getStackTrace" => JsonSerializer.Deserialize<Contracts.Debug.DebugGetStackTraceRequest>(json, _jsonOptions),
                "debug.getVariables" => JsonSerializer.Deserialize<Contracts.Debug.DebugGetVariablesRequest>(json, _jsonOptions),
                "debug.evaluate" => JsonSerializer.Deserialize<Contracts.Debug.DebugEvaluateRequest>(json, _jsonOptions),
                "debug.getThreads" => JsonSerializer.Deserialize<Contracts.Debug.DebugGetThreadsRequest>(json, _jsonOptions),
                "debug.selectThread" => JsonSerializer.Deserialize<Contracts.Debug.DebugSelectThreadRequest>(json, _jsonOptions),
                "debug.getStatus" => JsonSerializer.Deserialize<Contracts.Debug.DebugGetStatusRequest>(json, _jsonOptions),
                
                _ => null
            };
        }
        catch
        {
            return null;
        }
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