using DebugOpsMCP.Contracts.Debug;
using DebugOpsMCP.Core.Debug;
using DebugOpsMCP.Core.Tools;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DebugOpsMCP.Core.Tests.Tools;

public class DebugBreakpointToolTests
{
    private readonly Mock<ILogger<DebugBreakpointTool>> _loggerMock;
    private readonly Mock<IDebugBridge> _debugBridgeMock;
    private readonly DebugBreakpointTool _tool;

    public DebugBreakpointToolTests()
    {
        _loggerMock = new Mock<ILogger<DebugBreakpointTool>>();
        _debugBridgeMock = new Mock<IDebugBridge>();
        _tool = new DebugBreakpointTool(_loggerMock.Object, _debugBridgeMock.Object);
    }

    [Fact]
    public async Task HandleSetBreakpointAsync_WhenNotConnected_ReturnsNoSessionError()
    {
        // Arrange
        var request = new DebugSetBreakpointRequest 
        { 
            File = "test.cs", 
            Line = 10 
        };
        _debugBridgeMock.Setup(x => x.IsConnected).Returns(false);

        // Act
        var response = await _tool.HandleAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.False(response.Success);
        var errorResponse = Assert.IsType<DebugOpsMCP.Contracts.McpErrorResponse>(response);
        Assert.Equal("NO_DEBUG_SESSION", errorResponse.Error.Code);
    }

    [Fact]
    public async Task HandleSetBreakpointAsync_WhenConnected_ReturnsBreakpoint()
    {
        // Arrange
        var request = new DebugSetBreakpointRequest 
        { 
            File = "test.cs", 
            Line = 10,
            Condition = "x > 5"
        };
        _debugBridgeMock.Setup(x => x.IsConnected).Returns(true);

        // Act
        var response = await _tool.HandleAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success);
        
        var breakpointResponse = Assert.IsType<DebugBreakpointResponse>(response);
        Assert.NotNull(breakpointResponse.Result);
        Assert.Equal("test.cs", breakpointResponse.Result.File);
        Assert.Equal(10, breakpointResponse.Result.Line);
        Assert.Equal("x > 5", breakpointResponse.Result.Condition);
        Assert.True(breakpointResponse.Result.Verified);
    }

    [Fact]
    public async Task HandleRemoveBreakpointAsync_NonExistentBreakpoint_ReturnsNotFoundError()
    {
        // Arrange
        var request = new DebugRemoveBreakpointRequest 
        { 
            BreakpointId = "nonexistent" 
        };
        _debugBridgeMock.Setup(x => x.IsConnected).Returns(true);

        // Act
        var response = await _tool.HandleAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.False(response.Success);
        var errorResponse = Assert.IsType<DebugOpsMCP.Contracts.McpErrorResponse>(response);
        Assert.Equal("BREAKPOINT_NOT_FOUND", errorResponse.Error.Code);
    }

    [Fact]
    public async Task HandleListBreakpointsAsync_ReturnsAllBreakpoints()
    {
        // Arrange - First set a breakpoint
        var setRequest = new DebugSetBreakpointRequest 
        { 
            File = "test.cs", 
            Line = 10 
        };
        _debugBridgeMock.Setup(x => x.IsConnected).Returns(true);
        
        await _tool.HandleAsync(setRequest);
        
        var listRequest = new DebugListBreakpointsRequest();

        // Act
        var response = await _tool.HandleAsync(listRequest);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success);
        
        var breakpointsResponse = Assert.IsType<DebugBreakpointsResponse>(response);
        Assert.NotNull(breakpointsResponse.Result);
        Assert.Single(breakpointsResponse.Result);
        Assert.Equal("test.cs", breakpointsResponse.Result[0].File);
    }
}