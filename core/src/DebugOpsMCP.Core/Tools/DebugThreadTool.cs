using DebugOpsMCP.Contracts;
using DebugOpsMCP.Contracts.Debug;
using DebugOpsMCP.Core.Debug;
using Microsoft.Extensions.Logging;

namespace DebugOpsMCP.Core.Tools;

/// <summary>
/// Handles thread management operations (list, select)
/// </summary>
public class DebugThreadTool : IDebugThreadTool
{
    private readonly ILogger<DebugThreadTool> _logger;
    private readonly IDebugBridge _debugBridge;

    public DebugThreadTool(
        ILogger<DebugThreadTool> logger,
        IDebugBridge debugBridge)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _debugBridge = debugBridge ?? throw new ArgumentNullException(nameof(debugBridge));
    }

    public async Task<McpResponse> HandleAsync(McpRequest request)
    {
        return request.Method switch
        {
            "debug.getThreads" => await HandleGetThreadsAsync((DebugGetThreadsRequest)request),
            "debug.selectThread" => await HandleSelectThreadAsync((DebugSelectThreadRequest)request),
            _ => new McpErrorResponse
            {
                Error = new McpError
                {
                    Code = "METHOD_NOT_SUPPORTED",
                    Message = $"Method {request.Method} not supported by DebugThreadTool"
                }
            }
        };
    }

    private Task<McpResponse> HandleGetThreadsAsync(DebugGetThreadsRequest request)
    {
        try
        {
            _logger.LogInformation("Getting threads");

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

            // TODO: Send actual DAP threads request
            // For now, return mock threads
            var mockThreads = new[]
            {
                new DebugThread
                {
                    Id = 1,
                    Name = "Main Thread",
                    Status = "paused"
                },
                new DebugThread
                {
                    Id = 2,
                    Name = "Background Worker",
                    Status = "running"
                },
                new DebugThread
                {
                    Id = 3,
                    Name = "UI Thread",
                    Status = "running"
                }
            };

            _logger.LogInformation("Threads retrieved: {ThreadCount} threads", mockThreads.Length);

            return new DebugThreadsResponse
            {
                Success = true,
                Result = mockThreads
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get threads");
            return new McpErrorResponse
            {
                Error = new McpError
                {
                    Code = "GET_THREADS_FAILED",
                    Message = ex.Message
                }
            };
        }
    }

    private Task<McpResponse> HandleSelectThreadAsync(DebugSelectThreadRequest request)
    {
        try
        {
            _logger.LogInformation("Selecting thread {ThreadId}", request.ThreadId);

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

            // TODO: Send actual DAP thread selection (this might be handled implicitly by other requests)
            await Task.Delay(50); // Simulate selection time

            _logger.LogInformation("Thread {ThreadId} selected successfully", request.ThreadId);

            return Task.FromResult<McpResponse>(new McpResponse<string>
            {
                Success = true,
                Result = $"Thread {request.ThreadId} selected"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to select thread {ThreadId}", request.ThreadId);
            return new McpErrorResponse
            {
                Error = new McpError
                {
                    Code = "SELECT_THREAD_FAILED",
                    Message = ex.Message
                }
            };
        }
    }
}

/// <summary>
/// Handles debug status queries
/// </summary>
public class DebugStatusTool : IDebugStatusTool
{
    private readonly ILogger<DebugStatusTool> _logger;
    private readonly IDebugBridge _debugBridge;

    public DebugStatusTool(
        ILogger<DebugStatusTool> logger,
        IDebugBridge debugBridge)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _debugBridge = debugBridge ?? throw new ArgumentNullException(nameof(debugBridge));
    }

    public async Task<McpResponse> HandleAsync(McpRequest request)
    {
        return request.Method switch
        {
            "debug.getStatus" => await HandleGetStatusAsync((DebugGetStatusRequest)request),
            _ => new McpErrorResponse
            {
                Error = new McpError
                {
                    Code = "METHOD_NOT_SUPPORTED",
                    Message = $"Method {request.Method} not supported by DebugStatusTool"
                }
            }
        };
    }

    private Task<McpResponse> HandleGetStatusAsync(DebugGetStatusRequest request)
    {
        try
        {
            _logger.LogDebug("Getting debug status");

            // TODO: Get actual status from debug bridge/session manager
            var status = new DebugStatus
            {
                IsDebugging = _debugBridge.IsConnected,
                IsPaused = false, // TODO: Get from actual debug state
                ActiveThreadId = _debugBridge.IsConnected ? 1 : null,
                SessionId = _debugBridge.IsConnected ? "session-123" : null
            };

            return new DebugStatusResponse
            {
                Success = true,
                Result = status
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get debug status");
            return new McpErrorResponse
            {
                Error = new McpError
                {
                    Code = "GET_STATUS_FAILED",
                    Message = ex.Message
                }
            };
        }
    }
}