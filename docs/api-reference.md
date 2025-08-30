# DebugOpsMCP API Reference

## Table of Contents

1. [Core Interfaces](#core-interfaces)
2. [Debug Tools](#debug-tools)
3. [Bridge Communication](#bridge-communication)
4. [Contract Types](#contract-types)
5. [Error Handling](#error-handling)
6. [Extension Points](#extension-points)

## Core Interfaces

### IDebugBridge

Central communication interface between MCP server and VS Code extension.

```csharp
public interface IDebugBridge
{
    Task<bool> InitializeAsync();
    Task<TResponse> SendRequestAsync<TRequest, TResponse>(TRequest request)
        where TRequest : class
        where TResponse : class;
    Task SubscribeToEventsAsync(Func<DebugEvent, Task> eventHandler);
    Task UnsubscribeFromEventsAsync();
    void Dispose();
}
```

#### Methods

##### InitializeAsync()
**Returns:** `Task<bool>`  
**Description:** Initialize the bridge connection with VS Code extension.

**Example:**
```csharp
var bridge = serviceProvider.GetService<IDebugBridge>();
var success = await bridge.InitializeAsync();
if (!success)
{
    throw new DebugBridgeConnectionException("Failed to initialize bridge");
}
```

##### SendRequestAsync<TRequest, TResponse>(TRequest request)
**Returns:** `Task<TResponse>`  
**Description:** Send typed request to VS Code extension and await response.

**Parameters:**
- `request`: Strongly typed request object

**Throws:**
- `DebugTimeoutException`: Request timed out
- `DebugProtocolException`: Protocol error
- `DebugBridgeConnectionException`: Connection lost

**Example:**
```csharp
var attachRequest = new DebugAttachRequest { ProcessId = 1234 };
var response = await bridge.SendRequestAsync<DebugAttachRequest, DebugSessionResponse>(
    attachRequest);
```

### IDebugTool

Interface for all debug operation handlers.

```csharp
public interface IDebugTool
{
    Task<McpResponse> HandleAsync(McpRequest request);
    bool CanHandle(string method);
}
```

#### Methods

##### HandleAsync(McpRequest request)
**Returns:** `Task<McpResponse>`  
**Description:** Process debug request and return response.

**Parameters:**
- `request`: MCP request containing method and parameters

**Example:**
```csharp
public async Task<McpResponse> HandleAsync(McpRequest request)
{
    try
    {
        return request.Method switch
        {
            "debug.attach" => await HandleAttach(request),
            "debug.detach" => await HandleDetach(request),
            _ => McpResponse.Error("METHOD_NOT_FOUND", $"Unknown method: {request.Method}")
        };
    }
    catch (Exception ex)
    {
        return McpResponse.Error("INTERNAL_ERROR", ex.Message);
    }
}
```

##### CanHandle(string method)
**Returns:** `bool`  
**Description:** Check if tool can handle the specified method.

**Parameters:**
- `method`: MCP method name

**Example:**
```csharp
public bool CanHandle(string method)
{
    return method.StartsWith("debug.lifecycle.");
}
```

### IMcpToolRegistry

Registry for managing and routing debug tools.

```csharp
public interface IMcpToolRegistry
{
    Task<McpResponse> RouteRequestAsync(McpRequest request);
    void RegisterTool(IDebugTool tool);
    IEnumerable<IDebugTool> GetTools();
}
```

#### Methods

##### RouteRequestAsync(McpRequest request)
**Returns:** `Task<McpResponse>`  
**Description:** Route request to appropriate debug tool.

**Parameters:**
- `request`: MCP request to route

**Example:**
```csharp
var response = await registry.RouteRequestAsync(new McpRequest
{
    Method = "debug.attach",
    Params = JObject.FromObject(new { processId = 1234 })
});
```

## Debug Tools

### DebugLifecycleTool

Handles debug session lifecycle operations.

```csharp
public class DebugLifecycleTool : IDebugTool
{
    public bool CanHandle(string method) => method.StartsWith("debug.lifecycle.");
}
```

#### Supported Methods

- `debug.attach` - Attach to running process
- `debug.launch` - Launch program for debugging
- `debug.detach` - Detach from debug session
- `debug.terminate` - Terminate debug session

#### Example Usage

```csharp
// Attach to process
var attachResponse = await tool.HandleAsync(new McpRequest
{
    Method = "debug.attach",
    Params = JObject.FromObject(new DebugAttachRequest
    {
        ProcessId = 1234,
        Configuration = new DebugConfiguration
        {
            StopOnEntry = false,
            JustMyCode = true
        }
    })
});
```

### DebugBreakpointTool

Manages breakpoints in debug sessions.

```csharp
public class DebugBreakpointTool : IDebugTool
{
    public bool CanHandle(string method) => method.StartsWith("debug.breakpoint.");
}
```

#### Supported Methods

- `debug.setBreakpoint` - Set breakpoint at location
- `debug.removeBreakpoint` - Remove breakpoint
- `debug.listBreakpoints` - List all breakpoints
- `debug.toggleBreakpoint` - Toggle breakpoint state

#### Example Usage

```csharp
// Set conditional breakpoint
var setResponse = await tool.HandleAsync(new McpRequest
{
    Method = "debug.setBreakpoint",
    Params = JObject.FromObject(new SetBreakpointRequest
    {
        File = @"C:\source\program.cs",
        Line = 42,
        Condition = "variable > 10",
        HitCondition = "3"
    })
});
```

### DebugExecutionTool

Controls program execution flow.

```csharp
public class DebugExecutionTool : IDebugExecutionTool
{
    public bool CanHandle(string method) => method.StartsWith("debug.execution.");
}
```

#### Supported Methods

- `debug.continue` - Continue execution
- `debug.step` - Step through code (over/into/out)
- `debug.pause` - Pause execution
- `debug.restart` - Restart debug session

#### Example Usage

```csharp
// Step over current line
var stepResponse = await tool.HandleAsync(new McpRequest
{
    Method = "debug.step",
    Params = JObject.FromObject(new StepRequest
    {
        ThreadId = 12345,
        StepType = StepType.Over
    })
});
```

### DebugInspectionTool

Provides code and variable inspection capabilities.

```csharp
public class DebugInspectionTool : IDebugInspectionTool
{
    public bool CanHandle(string method) => method.StartsWith("debug.inspection.");
}
```

#### Supported Methods

- `debug.getStackTrace` - Get call stack
- `debug.getVariables` - Get variables in scope
- `debug.evaluate` - Evaluate expressions
- `debug.getSource` - Get source code

#### Example Usage

```csharp
// Get variables in current frame
var varsResponse = await tool.HandleAsync(new McpRequest
{
    Method = "debug.getVariables",
    Params = JObject.FromObject(new GetVariablesRequest
    {
        FrameId = "frame-1",
        Filter = VariableFilter.Locals
    })
});
```

### DebugThreadTool

Manages debugging threads and concurrency.

```csharp
public class DebugThreadTool : IDebugThreadTool
{
    public bool CanHandle(string method) => method.StartsWith("debug.thread.");
}
```

#### Supported Methods

- `debug.getThreads` - List all threads
- `debug.getStatus` - Get debug session status
- `debug.selectThread` - Switch active thread

#### Example Usage

```csharp
// Get all threads
var threadsResponse = await tool.HandleAsync(new McpRequest
{
    Method = "debug.getThreads"
});
```

## Bridge Communication

### ExtensionMediatedDebugBridge

Primary implementation of `IDebugBridge` using stdio communication.

```csharp
public class ExtensionMediatedDebugBridge : IDebugBridge
{
    private readonly ILogger<ExtensionMediatedDebugBridge> _logger;
    private Process? _extensionProcess;
    private readonly SemaphoreSlim _requestSemaphore = new(1, 1);
    private readonly Dictionary<string, TaskCompletionSource<string>> _pendingRequests = new();
}
```

#### Configuration

```csharp
public class DebugBridgeOptions
{
    public int TimeoutMs { get; set; } = 10000;
    public int RetryAttempts { get; set; } = 3;
    public int RetryDelayMs { get; set; } = 1000;
    public bool EnableLogging { get; set; } = true;
}
```

#### Error Handling

The bridge implements comprehensive error handling with custom exceptions:

```csharp
try
{
    var response = await bridge.SendRequestAsync<TRequest, TResponse>(request);
    return response;
}
catch (DebugTimeoutException ex)
{
    _logger.LogWarning("Request timed out: {Message}", ex.Message);
    throw;
}
catch (DebugBridgeConnectionException ex)
{
    _logger.LogError("Bridge connection failed: {Message}", ex.Message);
    throw;
}
```

## Contract Types

### Request Types

#### DebugAttachRequest
```csharp
public class DebugAttachRequest
{
    public int ProcessId { get; set; }
    public DebugConfiguration? Configuration { get; set; }
}
```

#### DebugLaunchRequest
```csharp
public class DebugLaunchRequest
{
    public string Program { get; set; } = string.Empty;
    public string[] Args { get; set; } = Array.Empty<string>();
    public DebugConfiguration? Configuration { get; set; }
    public string? WorkingDirectory { get; set; }
    public Dictionary<string, string>? Environment { get; set; }
}
```

#### SetBreakpointRequest
```csharp
public class SetBreakpointRequest
{
    public string File { get; set; } = string.Empty;
    public int Line { get; set; }
    public int Column { get; set; } = 0;
    public string? Condition { get; set; }
    public string? HitCondition { get; set; }
}
```

#### StepRequest
```csharp
public class StepRequest
{
    public int ThreadId { get; set; }
    public StepType StepType { get; set; }
}

public enum StepType
{
    Over,
    Into,
    Out,
    Back
}
```

#### GetVariablesRequest
```csharp
public class GetVariablesRequest
{
    public string FrameId { get; set; } = string.Empty;
    public VariableFilter Filter { get; set; } = VariableFilter.All;
    public int Start { get; set; } = 0;
    public int Count { get; set; } = 100;
}

public enum VariableFilter
{
    All,
    Locals,
    Arguments,
    Statics
}
```

#### EvaluateRequest
```csharp
public class EvaluateRequest
{
    public string Expression { get; set; } = string.Empty;
    public string? FrameId { get; set; }
    public EvaluateContext Context { get; set; } = EvaluateContext.Watch;
}

public enum EvaluateContext
{
    Watch,
    Repl,
    Hover
}
```

### Response Types

#### DebugSessionResponse
```csharp
public class DebugSessionResponse
{
    public string SessionId { get; set; } = string.Empty;
    public DebugCapabilities Capabilities { get; set; } = new();
    public string Status { get; set; } = string.Empty;
}

public class DebugCapabilities
{
    public bool SupportsBreakpoints { get; set; }
    public bool SupportsConditionalBreakpoints { get; set; }
    public bool SupportsHitConditionalBreakpoints { get; set; }
    public bool SupportsFunctionBreakpoints { get; set; }
    public bool SupportsEvaluateForHovers { get; set; }
    public bool SupportsStepBack { get; set; }
    public bool SupportsSetVariable { get; set; }
}
```

#### BreakpointResponse
```csharp
public class BreakpointResponse
{
    public string Id { get; set; } = string.Empty;
    public string File { get; set; } = string.Empty;
    public int Line { get; set; }
    public int Column { get; set; }
    public bool Verified { get; set; }
    public string? Condition { get; set; }
    public string? HitCondition { get; set; }
    public string? Message { get; set; }
}
```

#### StackTraceResponse
```csharp
public class StackTraceResponse
{
    public StackFrame[] Frames { get; set; } = Array.Empty<StackFrame>();
    public int TotalFrames { get; set; }
}

public class StackFrame
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Source? Source { get; set; }
    public int Line { get; set; }
    public int Column { get; set; }
}

public class Source
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string? SourceReference { get; set; }
}
```

#### VariablesResponse
```csharp
public class VariablesResponse
{
    public Variable[] Variables { get; set; } = Array.Empty<Variable>();
}

public class Variable
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? VariablesReference { get; set; }
    public bool Expensive { get; set; }
}
```

#### ThreadsResponse
```csharp
public class ThreadsResponse
{
    public DebugThread[] Threads { get; set; } = Array.Empty<DebugThread>();
}

public class DebugThread
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
```

### Configuration Types

#### DebugConfiguration
```csharp
public class DebugConfiguration
{
    public bool StopOnEntry { get; set; } = false;
    public bool JustMyCode { get; set; } = true;
    public bool SuppressJITOptimizations { get; set; } = false;
    public string[]? AdditionalSOLibSearchPath { get; set; }
    public Dictionary<string, object>? CustomProperties { get; set; }
}
```

## Error Handling

### Exception Hierarchy

```csharp
// Base exception for all debug operations
public abstract class DebugException : Exception
{
    protected DebugException(string message) : base(message) { }
    protected DebugException(string message, Exception innerException) : base(message, innerException) { }
}

// Bridge communication errors
public class DebugBridgeConnectionException : DebugException
{
    public DebugBridgeConnectionException(string message) : base(message) { }
}

// Process attachment errors
public class DebugAttachmentException : DebugException
{
    public int ProcessId { get; }
    
    public DebugAttachmentException(int processId, string message) : base(message)
    {
        ProcessId = processId;
    }
}

// Program launch errors
public class DebugLaunchException : DebugException
{
    public string Program { get; }
    
    public DebugLaunchException(string program, string message) : base(message)
    {
        Program = program;
    }
}

// Request timeout errors
public class DebugTimeoutException : DebugException
{
    public TimeSpan Timeout { get; }
    
    public DebugTimeoutException(TimeSpan timeout, string message) : base(message)
    {
        Timeout = timeout;
    }
}

// Protocol communication errors
public class DebugProtocolException : DebugException
{
    public string Protocol { get; }
    
    public DebugProtocolException(string protocol, string message) : base(message)
    {
        Protocol = protocol;
    }
}

// Debug session conflicts
public class DebugSessionConflictException : DebugException
{
    public string ExistingSessionId { get; }
    
    public DebugSessionConflictException(string existingSessionId, string message) : base(message)
    {
        ExistingSessionId = existingSessionId;
    }
}

// Session not found errors
public class DebugSessionNotFoundException : DebugException
{
    public string SessionId { get; }
    
    public DebugSessionNotFoundException(string sessionId) : base($"Debug session not found: {sessionId}")
    {
        SessionId = sessionId;
    }
}

// Breakpoint operation errors
public class DebugBreakpointException : DebugException
{
    public string File { get; }
    public int Line { get; }
    
    public DebugBreakpointException(string file, int line, string message) : base(message)
    {
        File = file;
        Line = line;
    }
}

// Expression evaluation errors
public class DebugEvaluationException : DebugException
{
    public string Expression { get; }
    
    public DebugEvaluationException(string expression, string message) : base(message)
    {
        Expression = expression;
    }
}

// Thread operation errors  
public class DebugThreadException : DebugException
{
    public int ThreadId { get; }
    
    public DebugThreadException(int threadId, string message) : base(message)
    {
        ThreadId = threadId;
    }
}
```

### Error Response Format

```csharp
public class McpErrorResponse : McpResponse
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public object? Data { get; set; }

    public static McpErrorResponse Create(string code, string message, object? data = null)
    {
        return new McpErrorResponse
        {
            Success = false,
            Code = code,
            Message = message,
            Data = data
        };
    }
}
```

### Standard Error Codes

```csharp
public static class DebugErrorCodes
{
    // Method and request errors
    public const string METHOD_NOT_FOUND = "METHOD_NOT_FOUND";
    public const string INVALID_REQUEST = "INVALID_REQUEST";
    public const string INVALID_PARAMS = "INVALID_PARAMS";
    
    // Bridge communication errors
    public const string BRIDGE_CONNECTION_FAILED = "DEBUG_BRIDGE_CONNECTION_FAILED";
    public const string BRIDGE_TIMEOUT = "DEBUG_BRIDGE_TIMEOUT";
    public const string BRIDGE_PROTOCOL_ERROR = "DEBUG_BRIDGE_PROTOCOL_ERROR";
    
    // Session management errors
    public const string ATTACHMENT_FAILED = "DEBUG_ATTACHMENT_FAILED";
    public const string LAUNCH_FAILED = "DEBUG_LAUNCH_FAILED"; 
    public const string SESSION_NOT_FOUND = "DEBUG_SESSION_NOT_FOUND";
    public const string SESSION_CONFLICT = "DEBUG_SESSION_CONFLICT";
    
    // Execution control errors
    public const string EXECUTION_FAILED = "DEBUG_EXECUTION_FAILED";
    public const string STEP_FAILED = "DEBUG_STEP_FAILED";
    public const string CONTINUE_FAILED = "DEBUG_CONTINUE_FAILED";
    
    // Breakpoint errors
    public const string BREAKPOINT_SET_FAILED = "BREAKPOINT_SET_FAILED";
    public const string BREAKPOINT_REMOVE_FAILED = "BREAKPOINT_REMOVE_FAILED";
    public const string BREAKPOINT_NOT_FOUND = "BREAKPOINT_NOT_FOUND";
    
    // Inspection errors
    public const string STACK_TRACE_FAILED = "STACK_TRACE_FAILED";
    public const string VARIABLES_FAILED = "GET_VARIABLES_FAILED";
    public const string EVALUATION_FAILED = "EVALUATION_FAILED";
    
    // Thread errors
    public const string THREAD_NOT_FOUND = "THREAD_NOT_FOUND";
    public const string THREAD_OPERATION_FAILED = "THREAD_OPERATION_FAILED";
    
    // General errors
    public const string INTERNAL_ERROR = "INTERNAL_ERROR";
    public const string TIMEOUT = "TIMEOUT";
    public const string ACCESS_DENIED = "ACCESS_DENIED";
}
```

## Extension Points

### Custom Tool Registration

```csharp
// Attribute-based registration
[McpMethod("debug.custom")]
[McpDescription("Custom debugging operations")]
public class CustomDebugTool : IDebugTool
{
    // Implementation
}

// Manual registration
public static class DebugToolsExtensions
{
    public static IServiceCollection AddCustomDebugTools(this IServiceCollection services)
    {
        services.AddScoped<IDebugTool, CustomAnalysisTool>();
        services.AddScoped<IDebugTool, CustomVisualizationTool>();
        services.AddScoped<IDebugTool, CustomProfilingTool>();
        
        return services;
    }
}
```

### Bridge Extensions

```csharp
public interface IDebugBridgeExtension
{
    Task<object> ProcessCustomRequestAsync(string method, object parameters);
    bool CanHandleCustomRequest(string method);
}

public class CustomDebugBridgeExtension : IDebugBridgeExtension
{
    public bool CanHandleCustomRequest(string method)
    {
        return method.StartsWith("custom.");
    }
    
    public async Task<object> ProcessCustomRequestAsync(string method, object parameters)
    {
        // Handle custom bridge operations
        return method switch
        {
            "custom.memory.scan" => await ScanMemory(parameters),
            "custom.performance.profile" => await StartProfiling(parameters),
            _ => throw new NotSupportedException($"Custom method not supported: {method}")
        };
    }
}
```

### Event System

```csharp
public interface IDebugEventPublisher
{
    event EventHandler<DebugEventArgs>? BreakpointHit;
    event EventHandler<DebugEventArgs>? ExceptionThrown;
    event EventHandler<DebugEventArgs>? SessionStarted;
    event EventHandler<DebugEventArgs>? SessionEnded;
    
    Task PublishAsync(DebugEvent debugEvent);
}

public class DebugEventArgs : EventArgs
{
    public DebugEvent Event { get; }
    
    public DebugEventArgs(DebugEvent debugEvent)
    {
        Event = debugEvent;
    }
}

public class DebugEvent
{
    public string Type { get; set; } = string.Empty;
    public object Data { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string SessionId { get; set; } = string.Empty;
}
```

This API reference provides comprehensive documentation for all public interfaces, types, and extension points in the DebugOpsMCP framework.