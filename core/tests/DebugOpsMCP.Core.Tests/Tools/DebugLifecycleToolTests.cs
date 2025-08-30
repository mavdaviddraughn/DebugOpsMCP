using DebugOpsMCP.Contracts.Debug;
using DebugOpsMCP.Core.Debug;
using DebugOpsMCP.Core.Tools;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DebugOpsMCP.Core.Tests.Tools;

public class DebugLifecycleToolTests
{
    private readonly Mock<ILogger<DebugLifecycleTool>> _loggerMock;
    private readonly Mock<IDebugBridge> _debugBridgeMock;
    private readonly DebugLifecycleTool _tool;

    public DebugLifecycleToolTests()
    {
        _loggerMock = new Mock<ILogger<DebugLifecycleTool>>();
        _debugBridgeMock = new Mock<IDebugBridge>();
        _tool = new DebugLifecycleTool(_loggerMock.Object, _debugBridgeMock.Object);
    }

    [Fact]
    public async Task HandleAttachAsync_WhenBridgeNotConnected_InitializesBridge()
    {
        // Arrange
        var request = new DebugAttachRequest { ProcessId = 1234 };
        _debugBridgeMock.Setup(x => x.IsConnected).Returns(false);
        _debugBridgeMock.Setup(x => x.InitializeAsync()).ReturnsAsync(true);

        // Act
        var response = await _tool.HandleAsync(request);

        // Assert
        _debugBridgeMock.Verify(x => x.InitializeAsync(), Times.Once);
        Assert.NotNull(response);
        Assert.True(response.Success);
    }

    [Fact]
    public async Task HandleAttachAsync_WhenBridgeInitFails_ReturnsError()
    {
        // Arrange
        var request = new DebugAttachRequest { ProcessId = 1234 };
        _debugBridgeMock.Setup(x => x.IsConnected).Returns(false);
        _debugBridgeMock.Setup(x => x.InitializeAsync()).ReturnsAsync(false);

        // Act
        var response = await _tool.HandleAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.False(response.Success);
        Assert.Equal("BRIDGE_INIT_FAILED", ((DebugOpsMCP.Contracts.McpErrorResponse)response).Error.Code);
    }

    [Fact]
    public async Task HandleLaunchAsync_ValidProgram_ReturnsSession()
    {
        // Arrange
        var request = new DebugLaunchRequest { Program = "test.exe" };
        _debugBridgeMock.Setup(x => x.IsConnected).Returns(true);

        // Act
        var response = await _tool.HandleAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success);
        
        var sessionResponse = Assert.IsType<DebugSessionResponse>(response);
        Assert.NotNull(sessionResponse.Result);
        Assert.Equal("running", sessionResponse.Result.Status);
    }

    [Fact]
    public async Task HandleAsync_UnsupportedMethod_ReturnsError()
    {
        // Arrange
        var request = new DebugAttachRequest { ProcessId = 1234 };
        request.Method = "unsupported.method";

        // Act
        var response = await _tool.HandleAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.False(response.Success);
        var errorResponse = Assert.IsType<DebugOpsMCP.Contracts.McpErrorResponse>(response);
        Assert.Equal("METHOD_NOT_SUPPORTED", errorResponse.Error.Code);
    }
}