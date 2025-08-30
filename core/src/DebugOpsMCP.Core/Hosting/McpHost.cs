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
            var method = requestDoc.RootElement.GetProperty("method").GetString();

            if (string.IsNullOrEmpty(method))
            {
                return CreateErrorResponse("INVALID_REQUEST", "Missing or empty method property");
            }

            var response = await RouteRequestAsync(method, requestJson);
            var responseJson = JsonSerializer.Serialize(response, _jsonOptions);

            _logger.LogDebug("MCP response: {Response}", responseJson);
            return responseJson;
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
}