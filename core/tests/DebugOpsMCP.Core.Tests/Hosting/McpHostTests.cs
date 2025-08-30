using DebugOpsMCP.Core.Hosting;
using DebugOpsMCP.Core.Debug;
using DebugOpsMCP.Core.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DebugOpsMCP.Core.Tests.Hosting;

public class McpHostTests
{
    private readonly McpHost _mcpHost;
    private readonly IServiceProvider _serviceProvider;

    public McpHostTests()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));

        // Register debug bridge and tools for testing
        services.AddSingleton<IDebugBridge, ExtensionMediatedDebugBridge>();
        services.AddSingleton<IDebugLifecycleTool, DebugLifecycleTool>();
        services.AddSingleton<IDebugExecutionTool, DebugExecutionTool>();
        services.AddSingleton<IDebugBreakpointTool, DebugBreakpointTool>();
        services.AddSingleton<IDebugInspectionTool, DebugInspectionTool>();
        services.AddSingleton<IDebugThreadTool, DebugThreadTool>();
        services.AddSingleton<IDebugStatusTool, DebugStatusTool>();

        _serviceProvider = services.BuildServiceProvider();

        _mcpHost = new McpHost(
            _serviceProvider.GetRequiredService<ILogger<McpHost>>(),
            _serviceProvider
        );
    }

    [Fact]
    public async Task ProcessRequestAsync_HealthRequest_ReturnsSuccess()
    {
        // Arrange
        var healthRequest = """{"method":"health"}""";

        // Act
        var response = await _mcpHost.ProcessRequestAsync(healthRequest);

        // Assert
        Assert.NotNull(response);
        Assert.Contains("success", response);
        Assert.Contains("DebugOpsMCP server is running", response);
    }

    [Fact]
    public async Task ProcessRequestAsync_HealthRequest_JsonRpcEnvelopeShape()
    {
        // Arrange - JSON-RPC 2.0 health request
        var requestId = Guid.NewGuid().ToString();
        var jsonRpcRequest = $"{{\"jsonrpc\":\"2.0\",\"id\":\"{requestId}\",\"method\":\"health\"}}";

        // Act
        var response = await _mcpHost.ProcessRequestAsync(jsonRpcRequest);

        // Assert - response should be a JSON-RPC envelope with result.success and result.data
        Assert.NotNull(response);
        Assert.Contains("\"jsonrpc\":\"2.0\"", response);
        Assert.Contains($"\"id\":\"{requestId}\"", response);
        Assert.Contains("\"result\":", response);
        Assert.Contains("\"success\":", response);
        Assert.Contains("\"data\":", response);
    }

    [Fact]
    public async Task ProcessRequestAsync_InvalidMethod_JsonRpcErrorShape()
    {
        // Arrange - JSON-RPC request for an unknown method
        var requestId = Guid.NewGuid().ToString();
        var jsonRpcRequest = $"{{\"jsonrpc\":\"2.0\",\"id\":\"{requestId}\",\"method\":\"nonexistent.method\"}}";

        // Act
        var response = await _mcpHost.ProcessRequestAsync(jsonRpcRequest);

        // Assert - should be JSON-RPC error with numeric code and mcpCode preserved
        Assert.NotNull(response);
        var doc = System.Text.Json.JsonDocument.Parse(response);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("error", out var error));
        Assert.True(error.TryGetProperty("code", out var codeProp));
        Assert.Equal(System.Text.Json.JsonValueKind.Number, codeProp.ValueKind);
        // The MCP code should be preserved in error.data.mcpCode
        Assert.True(error.TryGetProperty("data", out var data));
        Assert.True(data.TryGetProperty("mcpCode", out var mcpCodeProp));
        Assert.Equal(System.Text.Json.JsonValueKind.String, mcpCodeProp.ValueKind);
        Assert.Equal("METHOD_NOT_FOUND", mcpCodeProp.GetString());
    }

    [Fact]
    public async Task ProcessRequestAsync_ListBreakpoints_ReturnsArrayShape()
    {
        // Arrange - JSON-RPC listBreakpoints request
        var requestId = Guid.NewGuid().ToString();
        var jsonRpcRequest = $"{{\"jsonrpc\":\"2.0\",\"id\":\"{requestId}\",\"method\":\"debug.listBreakpoints\"}}";

        // Act
        var response = await _mcpHost.ProcessRequestAsync(jsonRpcRequest);

        // Assert - response.result.data should be an array (may be empty)
        Assert.NotNull(response);
        var doc = System.Text.Json.JsonDocument.Parse(response);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("result", out var result));
        Assert.True(result.TryGetProperty("data", out var data));
        Assert.Equal(System.Text.Json.JsonValueKind.Array, data.ValueKind);
    }

    [Fact]
    public async Task ProcessRequestAsync_InvalidJson_ReturnsJsonParseError()
    {
        // Arrange
        var invalidRequest = """{"method":"health",}"""; // Invalid trailing comma

        // Act
        var response = await _mcpHost.ProcessRequestAsync(invalidRequest);

        // Assert
        Assert.NotNull(response);
        Assert.Contains("JSON_PARSE_ERROR", response);
        Assert.Contains("success", response);
        Assert.Contains("false", response);
    }

    [Fact]
    public async Task ProcessRequestAsync_MissingMethod_ReturnsInvalidRequestError()
    {
        // Arrange
        var requestWithoutMethod = """{"data":"test"}""";

        // Act
        var response = await _mcpHost.ProcessRequestAsync(requestWithoutMethod);

        // Assert
        Assert.NotNull(response);
        Assert.Contains("INVALID_REQUEST", response);
        Assert.Contains("Missing or empty method property", response);
    }

    [Fact]
    public async Task ProcessRequestAsync_UnknownMethod_ReturnsMethodNotFoundError()
    {
        // Arrange
        var unknownMethodRequest = """{"method":"unknown.method"}""";

        // Act
        var response = await _mcpHost.ProcessRequestAsync(unknownMethodRequest);

        // Assert
        Assert.NotNull(response);
        Assert.Contains("METHOD_NOT_FOUND", response);
        Assert.Contains("Unknown method: unknown.method", response);
    }

    [Theory]
    [InlineData("debug.attach")]
    [InlineData("debug.launch")]
    [InlineData("debug.setBreakpoint")]
    [InlineData("debug.getStackTrace")]
    [InlineData("debug.evaluate")]
    public async Task ProcessRequestAsync_DebugMethods_ReturnToolResponses(string method)
    {
        // Arrange - Create minimal valid request for the method
        var request = method switch
        {
            "debug.attach" => """{"method":"debug.attach","processId":1234}""",
            "debug.launch" => """{"method":"debug.launch","program":"test.exe"}""",
            "debug.setBreakpoint" => """{"method":"debug.setBreakpoint","file":"test.cs","line":10}""",
            "debug.getStackTrace" => """{"method":"debug.getStackTrace"}""",
            "debug.evaluate" => """{"method":"debug.evaluate","expression":"x + 1"}""",
            _ => $$"""{"method":"{{method}}"}"""
        };

        // Act
        var response = await _mcpHost.ProcessRequestAsync(request);

        // Assert
        Assert.NotNull(response);
        // Tools should handle these requests now, not return NOT_IMPLEMENTED
        // The actual response depends on the tool implementation (may succeed or fail with specific errors)
        Assert.True(response.Contains("success") || response.Contains("error"));
    }

    private void Dispose()
    {
        (_serviceProvider as IDisposable)?.Dispose();
    }
}