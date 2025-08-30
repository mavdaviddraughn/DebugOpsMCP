# DebugOpsMCP Extension Development Guide

## Table of Contents

1. [Getting Started](#getting-started)
2. [Development Environment Setup](#development-environment-setup)
3. [Architecture Deep Dive](#architecture-deep-dive)
4. [Building Custom Tools](#building-custom-tools)
5. [Extension Integration](#extension-integration)
6. [Testing and Debugging](#testing-and-debugging)
7. [Advanced Topics](#advanced-topics)
8. [Deployment](#deployment)

## Getting Started

This guide helps developers extend and customize the DebugOpsMCP framework for specific debugging scenarios.

### Prerequisites

- .NET 8.0 SDK or later
- Node.js 18+ and npm
- Visual Studio Code
- Basic understanding of MCP (Model Context Protocol)
- Familiarity with debugging concepts

### Quick Start

1. **Clone and Build**
```bash
git clone <repository>
cd DebugOpsMCP
dotnet build core/DebugOpsMCP.sln
cd vscode-extension && npm install && npm run compile
```

2. **Run Sample**
```bash
dotnet run --project core/src/DebugOpsMCP.Host
```

3. **Test Extension**
   - Open VS Code
   - Install extension from `vscode-extension` folder
   - Test debug operations

## Development Environment Setup

### Core Server Development

The core server is built with .NET 8 and uses Microsoft.Extensions for dependency injection, logging, and configuration.

**Project Structure:**
```
core/
├── src/
│   ├── DebugOpsMCP.Contracts/     # Shared contracts and DTOs
│   ├── DebugOpsMCP.Core/          # Core debugging logic
│   └── DebugOpsMCP.Host/          # MCP server host
└── tests/
    ├── DebugOpsMCP.Core.Tests/         # Unit tests
    └── DebugOpsMCP.Integration.Tests/  # Integration tests
```

**Dependencies:**
- Microsoft.Extensions.Hosting (9.0.0)
- Microsoft.Extensions.DependencyInjection (9.0.0) 
- Microsoft.Extensions.Logging (9.0.0)
- System.Text.Json (9.0.0)

### VS Code Extension Development

The extension is written in TypeScript and integrates with VS Code's Debug API.

**Project Structure:**
```
vscode-extension/
├── src/
│   ├── extension.ts              # Main extension entry point
│   └── debugOpsMcpClient.ts      # MCP client implementation
├── package.json                  # Extension manifest
└── tsconfig.json                 # TypeScript configuration
```

**Dependencies:**
- @types/vscode
- typescript
- @types/node

## Architecture Deep Dive

### Core Components

#### 1. MCP Host (`Program.cs`)

The host bootstraps the MCP server and handles JSON-RPC communication:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Configure services
builder.Services.AddScoped<IDebugBridge, ExtensionMediatedDebugBridge>();
builder.Services.AddScoped<IMcpToolRegistry, McpToolRegistry>();

// Register debug tools
builder.Services.AddScoped<IDebugTool, DebugLifecycleTool>();
builder.Services.AddScoped<IDebugTool, DebugBreakpointTool>();
builder.Services.AddScoped<IDebugTool, DebugExecutionTool>();
builder.Services.AddScoped<IDebugTool, DebugInspectionTool>();
builder.Services.AddScoped<IDebugTool, DebugThreadTool>();

var app = builder.Build();

// Configure MCP request handling
app.UseRouting();
app.Run();
```

#### 2. Debug Bridge (`ExtensionMediatedDebugBridge`)

The bridge handles communication between the server and VS Code extension:

```csharp
public class ExtensionMediatedDebugBridge : IDebugBridge
{
    private Process? _extensionProcess;
    private readonly ILogger<ExtensionMediatedDebugBridge> _logger;

    public async Task<bool> InitializeAsync()
    {
        try
        {
            // Initialize stdio communication with VS Code extension
            return await EstablishConnection();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize debug bridge");
            return false;
        }
    }

    public async Task<TResponse> SendRequestAsync<TRequest, TResponse>(TRequest request)
        where TRequest : class
        where TResponse : class
    {
        // Serialize request and send via stdio
        var json = JsonSerializer.Serialize(request);
        await WriteToExtensionAsync(json);
        
        // Read and deserialize response
        var responseJson = await ReadFromExtensionAsync();
        return JsonSerializer.Deserialize<TResponse>(responseJson);
    }
}
```

#### 3. Tool Registry (`McpToolRegistry`)

Central registry for discovering and routing requests to debug tools:

```csharp
public class McpToolRegistry : IMcpToolRegistry
{
    private readonly IEnumerable<IDebugTool> _tools;
    private readonly ILogger<McpToolRegistry> _logger;

    public async Task<McpResponse> RouteRequestAsync(McpRequest request)
    {
        var tool = _tools.FirstOrDefault(t => t.CanHandle(request.Method));
        
        if (tool == null)
        {
            return McpResponse.Error("METHOD_NOT_FOUND", 
                $"No handler found for method: {request.Method}");
        }

        return await tool.HandleAsync(request);
    }
}
```

### Communication Protocol

#### Message Flow

1. **Client Request**: AI assistant sends JSON-RPC request to MCP server
2. **Server Routing**: MCP server routes request to appropriate debug tool
3. **Bridge Communication**: Debug tool sends request to VS Code extension via bridge
4. **DAP Integration**: Extension executes request using Debug Adapter Protocol
5. **Response Chain**: Result flows back through the chain to the AI assistant

#### Message Types

**Bridge Request Format:**
```json
{
  "id": "unique-id",
  "type": "request",
  "method": "debug.attach",
  "data": {
    "processId": 1234
  },
  "timestamp": "2025-08-30T07:38:51.696Z"
}
```

**Bridge Response Format:**
```json
{
  "id": "unique-id",
  "type": "response", 
  "data": {
    "sessionId": "session-123",
    "success": true
  },
  "error": null,
  "timestamp": "2025-08-30T07:38:51.798Z"
}
```

## Building Custom Tools

### Creating a Debug Tool

Implement the `IDebugTool` interface:

```csharp
public interface IDebugTool
{
    Task<McpResponse> HandleAsync(McpRequest request);
    bool CanHandle(string method);
}
```

### Example: Custom Memory Tool

```csharp
[McpMethod("debug.memory")]
public class DebugMemoryTool : IDebugTool
{
    private readonly IDebugBridge _debugBridge;
    private readonly ILogger<DebugMemoryTool> _logger;

    public DebugMemoryTool(
        IDebugBridge debugBridge,
        ILogger<DebugMemoryTool> logger)
    {
        _debugBridge = debugBridge;
        _logger = logger;
    }

    public bool CanHandle(string method)
    {
        return method.StartsWith("debug.memory.");
    }

    public async Task<McpResponse> HandleAsync(McpRequest request)
    {
        try
        {
            return request.Method switch
            {
                "debug.memory.dump" => await HandleMemoryDump(request),
                "debug.memory.stats" => await HandleMemoryStats(request),
                _ => McpResponse.Error("METHOD_NOT_FOUND", 
                    $"Unknown memory method: {request.Method}")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Memory tool error for method {Method}", request.Method);
            return McpResponse.Error("MEMORY_OPERATION_FAILED", ex.Message);
        }
    }

    private async Task<McpResponse> HandleMemoryDump(McpRequest request)
    {
        var dumpRequest = request.Params?.ToObject<MemoryDumpRequest>();
        if (dumpRequest == null)
        {
            return McpResponse.Error("INVALID_REQUEST", "Invalid memory dump parameters");
        }

        // Send request to extension via bridge
        var response = await _debugBridge.SendRequestAsync<MemoryDumpRequest, MemoryDumpResponse>(
            dumpRequest);

        return McpResponse.Success(response);
    }

    private async Task<McpResponse> HandleMemoryStats(McpRequest request)
    {
        var statsRequest = new MemoryStatsRequest();
        var response = await _debugBridge.SendRequestAsync<MemoryStatsRequest, MemoryStatsResponse>(
            statsRequest);

        return McpResponse.Success(response);
    }
}

// Contract classes
public class MemoryDumpRequest
{
    public ulong Address { get; set; }
    public int Length { get; set; }
}

public class MemoryDumpResponse
{
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public ulong Address { get; set; }
}

public class MemoryStatsRequest { }

public class MemoryStatsResponse
{
    public long TotalMemory { get; set; }
    public long UsedMemory { get; set; }
    public long FreeMemory { get; set; }
}
```

### Tool Registration

Register your tool in `Program.cs`:

```csharp
builder.Services.AddScoped<IDebugTool, DebugMemoryTool>();
```

### Method Attributes

Use attributes to define tool capabilities:

```csharp
[McpMethod("debug.custom")]
[McpDescription("Custom debugging operations")]
public class CustomDebugTool : IDebugTool
{
    // Implementation
}
```

## Extension Integration

### Extending VS Code Extension

The VS Code extension can be extended to handle new debug operations:

```typescript
// debugOpsMcpClient.ts
export class DebugOpsMcpClient {
    // Handle custom memory operations
    private async handleMemoryRequest(message: any): Promise<any> {
        const { method, data } = message;
        
        switch (method) {
            case 'debug.memory.dump':
                return await this.performMemoryDump(data);
            case 'debug.memory.stats':
                return await this.getMemoryStats();
            default:
                throw new Error(`Unknown memory method: ${method}`);
        }
    }

    private async performMemoryDump(params: any): Promise<any> {
        // Use VS Code debug API to dump memory
        const session = vscode.debug.activeDebugSession;
        if (!session) {
            throw new Error('No active debug session');
        }

        // Custom memory dump implementation
        const result = await session.customRequest('memoryDump', {
            address: params.address,
            length: params.length
        });

        return {
            data: result.data,
            address: params.address
        };
    }

    private async getMemoryStats(): Promise<any> {
        // Get memory statistics from debug session
        const session = vscode.debug.activeDebugSession;
        if (!session) {
            throw new Error('No active debug session');
        }

        const stats = await session.customRequest('memoryStats');
        return stats;
    }
}
```

### Debug Adapter Integration

For advanced scenarios, you may need to implement custom debug adapters:

```typescript
// Custom debug adapter
class CustomDebugAdapter {
    protected initializeRequest(
        response: DebugProtocol.InitializeResponse,
        args: DebugProtocol.InitializeRequestArguments
    ): void {
        // Initialize custom capabilities
        response.body = response.body || {};
        response.body.supportsMemoryReferences = true;
        response.body.supportsCustomRequests = true;
        
        this.sendResponse(response);
    }

    protected customRequest(
        command: string,
        response: DebugProtocol.Response,
        args: any
    ): void {
        switch (command) {
            case 'memoryDump':
                this.handleMemoryDump(response, args);
                break;
            case 'memoryStats':
                this.handleMemoryStats(response, args);
                break;
            default:
                super.customRequest(command, response, args);
        }
    }

    private handleMemoryDump(
        response: DebugProtocol.Response,
        args: any
    ): void {
        // Implement memory dump logic
        const data = this.dumpMemory(args.address, args.length);
        response.body = { data };
        this.sendResponse(response);
    }
}
```

## Testing and Debugging

### Unit Testing Debug Tools

```csharp
[TestClass]
public class DebugMemoryToolTests
{
    private Mock<IDebugBridge> _mockBridge;
    private Mock<ILogger<DebugMemoryTool>> _mockLogger;
    private DebugMemoryTool _tool;

    [TestInitialize]
    public void Setup()
    {
        _mockBridge = new Mock<IDebugBridge>();
        _mockLogger = new Mock<ILogger<DebugMemoryTool>>();
        _tool = new DebugMemoryTool(_mockBridge.Object, _mockLogger.Object);
    }

    [TestMethod]
    public async Task HandleMemoryDump_Success()
    {
        // Arrange
        var request = new McpRequest
        {
            Method = "debug.memory.dump",
            Params = JObject.FromObject(new MemoryDumpRequest 
            { 
                Address = 0x1000, 
                Length = 256 
            })
        };

        var expectedResponse = new MemoryDumpResponse
        {
            Address = 0x1000,
            Data = new byte[256]
        };

        _mockBridge.Setup(b => b.SendRequestAsync<MemoryDumpRequest, MemoryDumpResponse>(
            It.IsAny<MemoryDumpRequest>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _tool.HandleAsync(request);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType(result.Data, typeof(MemoryDumpResponse));
    }
}
```

### Integration Testing

```csharp
[TestClass]
public class McpServerIntegrationTests
{
    [TestMethod]
    public async Task FullDebugSession_Integration()
    {
        // Start MCP server
        using var server = new McpTestServer();
        await server.StartAsync();

        // Test complete debug workflow
        var client = new McpTestClient(server.Port);
        
        // Attach to process
        var attachResponse = await client.SendAsync(new McpRequest
        {
            Method = "debug.attach",
            Params = JObject.FromObject(new { processId = 1234 })
        });
        Assert.IsTrue(attachResponse.Success);

        // Set breakpoint
        var bpResponse = await client.SendAsync(new McpRequest
        {
            Method = "debug.setBreakpoint",
            Params = JObject.FromObject(new 
            {
                file = "test.cs",
                line = 10
            })
        });
        Assert.IsTrue(bpResponse.Success);

        // Continue execution
        var continueResponse = await client.SendAsync(new McpRequest
        {
            Method = "debug.continue"
        });
        Assert.IsTrue(continueResponse.Success);
    }
}
```

### Extension Testing

```typescript
// Test VS Code extension functionality
describe('DebugOpsMcpClient', () => {
    let client: DebugOpsMcpClient;

    beforeEach(() => {
        client = new DebugOpsMcpClient();
    });

    test('should handle memory dump request', async () => {
        // Mock VS Code debug session
        const mockSession = {
            customRequest: jest.fn().mockResolvedValue({
                data: new Uint8Array(256)
            })
        };

        // Test memory dump
        const result = await client.handleMemoryRequest({
            method: 'debug.memory.dump',
            data: { address: 0x1000, length: 256 }
        });

        expect(result.data).toBeDefined();
        expect(result.address).toBe(0x1000);
    });
});
```

## Advanced Topics

### Custom Protocol Extensions

You can extend the MCP protocol with custom message types:

```csharp
// Custom protocol extension
public class CustomProtocolExtension
{
    public const string CUSTOM_METHOD_PREFIX = "custom.debug.";

    public static void RegisterExtension(IMcpToolRegistry registry)
    {
        // Register custom methods
        registry.RegisterTool(new CustomAnalysisTool());
        registry.RegisterTool(new CustomVisualizationTool());
    }
}

public class CustomAnalysisTool : IDebugTool
{
    public bool CanHandle(string method)
    {
        return method.StartsWith("custom.debug.analysis.");
    }

    public async Task<McpResponse> HandleAsync(McpRequest request)
    {
        // Handle custom analysis operations
        return request.Method switch
        {
            "custom.debug.analysis.performance" => await AnalyzePerformance(request),
            "custom.debug.analysis.memory" => await AnalyzeMemory(request),
            _ => McpResponse.Error("METHOD_NOT_FOUND", $"Unknown analysis method: {request.Method}")
        };
    }
}
```

### Asynchronous Events

Handle asynchronous debug events:

```csharp
public class DebugEventHandler
{
    private readonly IDebugBridge _bridge;
    
    public event EventHandler<DebugEventArgs>? BreakpointHit;
    public event EventHandler<DebugEventArgs>? ExceptionThrown;

    public async Task StartEventMonitoring()
    {
        await _bridge.SubscribeToEventsAsync(HandleDebugEvent);
    }

    private void HandleDebugEvent(DebugEvent debugEvent)
    {
        switch (debugEvent.Type)
        {
            case "breakpoint":
                BreakpointHit?.Invoke(this, new DebugEventArgs(debugEvent));
                break;
            case "exception":
                ExceptionThrown?.Invoke(this, new DebugEventArgs(debugEvent));
                break;
        }
    }
}
```

### Performance Optimization

#### Connection Pooling

```csharp
public class DebugBridgePool
{
    private readonly ConcurrentQueue<IDebugBridge> _bridges = new();
    private readonly SemaphoreSlim _semaphore;

    public async Task<IDebugBridge> AcquireAsync()
    {
        await _semaphore.WaitAsync();
        
        if (_bridges.TryDequeue(out var bridge))
        {
            return bridge;
        }

        return await CreateNewBridgeAsync();
    }

    public void Release(IDebugBridge bridge)
    {
        _bridges.Enqueue(bridge);
        _semaphore.Release();
    }
}
```

#### Caching

```csharp
public class CachedDebugInformation
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions
    {
        SizeLimit = 100
    });

    public async Task<StackFrame[]> GetStackTraceAsync(int threadId)
    {
        var cacheKey = $"stack_{threadId}";
        
        if (_cache.TryGetValue(cacheKey, out StackFrame[]? cached))
        {
            return cached!;
        }

        var frames = await FetchStackTraceAsync(threadId);
        _cache.Set(cacheKey, frames, TimeSpan.FromSeconds(30));
        return frames;
    }
}
```

## Deployment

### Server Deployment

#### Self-Contained Deployment

```xml
<!-- DebugOpsMCP.Host.csproj -->
<PropertyGroup>
    <PublishSingleFile>true</PublishSingleFile>
    <SelfContained>true</SelfContained>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <PublishTrimmed>true</PublishTrimmed>
</PropertyGroup>
```

```bash
dotnet publish -c Release -r win-x64 --self-contained true
```

#### Docker Deployment

```dockerfile
# Dockerfile
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["core/src/DebugOpsMCP.Host/DebugOpsMCP.Host.csproj", "DebugOpsMCP.Host/"]
RUN dotnet restore "DebugOpsMCP.Host/DebugOpsMCP.Host.csproj"

COPY core/src/ .
WORKDIR "/src/DebugOpsMCP.Host"
RUN dotnet build "DebugOpsMCP.Host.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "DebugOpsMCP.Host.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DebugOpsMCP.Host.dll"]
```

### Extension Deployment

#### VSIX Package

```bash
# Build extension package
cd vscode-extension
npm install
npm run compile
vsce package
```

#### VS Code Marketplace

```json
// package.json
{
  "publisher": "your-publisher",
  "repository": {
    "type": "git",
    "url": "https://github.com/your-org/debugops-mcp"
  },
  "bugs": {
    "url": "https://github.com/your-org/debugops-mcp/issues"
  },
  "homepage": "https://github.com/your-org/debugops-mcp#readme"
}
```

```bash
# Publish to marketplace
vsce publish
```

### Configuration Management

#### Production Configuration

```json
// appsettings.Production.json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    }
  },
  "DebugBridge": {
    "TimeoutMs": 30000,
    "RetryAttempts": 5,
    "RetryDelayMs": 2000
  },
  "Performance": {
    "EnableCaching": true,
    "CacheExpirySeconds": 300
  }
}
```

#### Environment Variables

```bash
# Set environment variables for deployment
ASPNETCORE_ENVIRONMENT=Production
DEBUGOPS_LOG_LEVEL=Information
DEBUGOPS_BRIDGE_TIMEOUT=30000
```

This guide provides comprehensive coverage for extending and deploying the DebugOpsMCP framework in production environments.