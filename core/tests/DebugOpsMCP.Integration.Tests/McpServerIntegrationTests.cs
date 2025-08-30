using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Xunit;

namespace DebugOpsMCP.Integration.Tests;

public class McpServerIntegrationTests : IAsyncLifetime
{
    private Process? _serverProcess;
    private readonly string _serverPath;
    private readonly System.Threading.SemaphoreSlim _stdoutLock = new(1, 1);
    private readonly System.Threading.SemaphoreSlim _stdinLock = new(1, 1);

    public McpServerIntegrationTests()
    {
        // Determine the path to the server executable
        var currentDir = Directory.GetCurrentDirectory();
        var projectRoot = FindProjectRoot(currentDir);
        _serverPath = Path.Combine(projectRoot, "src", "DebugOpsMCP.Host", "bin", "Debug", "net8.0", "DebugOpsMCP.Host.dll");
    }

    public async Task InitializeAsync()
    {
        // Build the project first to ensure the executable exists
        await BuildServerAsync();

        // Start the server process
        _serverProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = _serverPath,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        _serverProcess.Start();

        // Give the server a moment to start
        await Task.Delay(1000);

        if (_serverProcess.HasExited)
        {
            var error = await _serverProcess.StandardError.ReadToEndAsync();
            throw new InvalidOperationException($"Server failed to start: {error}");
        }
    }

    public async Task DisposeAsync()
    {
        if (_serverProcess != null && !_serverProcess.HasExited)
        {
            _serverProcess.Kill();
            await _serverProcess.WaitForExitAsync();
        }
        _serverProcess?.Dispose();
    }

    [Fact]
    public async Task HealthCheck_ReturnsSuccess()
    {
        // Arrange
        var requestId = Guid.NewGuid().ToString();
        var request = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = requestId,
            method = "health"
        });

        // Act
        var response = await SendRequestAsync(request);

        // Assert
        Assert.NotNull(response);
        var jsonDoc = JsonDocument.Parse(response);
        var root = jsonDoc.RootElement;

        Assert.True(root.TryGetProperty("jsonrpc", out var jsonrpc));
        Assert.Equal("2.0", jsonrpc.GetString());

        Assert.True(root.TryGetProperty("id", out var id));
        Assert.Equal(requestId, id.GetString());

        Assert.True(root.TryGetProperty("result", out var result));
        Assert.True(result.TryGetProperty("success", out var success));
        Assert.True(success.GetBoolean());
    }

    [Fact]
    public async Task DebugGetStatus_ReturnsValidStatus()
    {
        // Arrange
        var requestId = Guid.NewGuid().ToString();
        var request = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = requestId,
            method = "debug.getStatus"
        });

        // Act
        var response = await SendRequestAsync(request);

        // Assert
        Assert.NotNull(response);
        var jsonDoc = JsonDocument.Parse(response);
        var root = jsonDoc.RootElement;

        Assert.True(root.TryGetProperty("jsonrpc", out var jsonrpc));
        Assert.Equal("2.0", jsonrpc.GetString());

        Assert.True(root.TryGetProperty("id", out var id));
        Assert.Equal(requestId, id.GetString());

        Assert.True(root.TryGetProperty("result", out var result));
        Assert.True(result.TryGetProperty("data", out var data));
        Assert.True(data.TryGetProperty("isDebugging", out _));
        Assert.True(data.TryGetProperty("isPaused", out _));
    }

    [Fact]
    public async Task DebugGetThreads_ReturnsThreadList()
    {
        // Arrange
        var requestId = Guid.NewGuid().ToString();
        var request = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = requestId,
            method = "debug.getThreads"
        });

        // Act
        var response = await SendRequestAsync(request);

        // Assert
        Assert.NotNull(response);
        var jsonDoc = JsonDocument.Parse(response);
        var root = jsonDoc.RootElement;

        Assert.True(root.TryGetProperty("jsonrpc", out var jsonrpc));
        Assert.Equal("2.0", jsonrpc.GetString());

        Assert.True(root.TryGetProperty("id", out var id));
        Assert.Equal(requestId, id.GetString());

        // Should return either result with data or error (since no debug session is active)
        Assert.True(root.TryGetProperty("result", out var result) || root.TryGetProperty("error", out _));

        if (result.ValueKind != JsonValueKind.Undefined)
        {
            Assert.True(result.TryGetProperty("data", out var data));
            Assert.Equal(JsonValueKind.Array, data.ValueKind);
        }
    }

    [Fact]
    public async Task DebugAttach_WithInvalidProcessId_ReturnsError()
    {
        // Arrange
        var requestId = Guid.NewGuid().ToString();
        var request = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = requestId,
            method = "debug.attach",
            @params = new
            {
                processId = 999999, // Non-existent process ID
                configuration = new Dictionary<string, object>()
            }
        });

        // Act
        var response = await SendRequestAsync(request);

        // Assert
        Assert.NotNull(response);
        var jsonDoc = JsonDocument.Parse(response);
        var root = jsonDoc.RootElement;

        Assert.True(root.TryGetProperty("jsonrpc", out var jsonrpc));
        Assert.Equal("2.0", jsonrpc.GetString());

        Assert.True(root.TryGetProperty("id", out var id));
        Assert.Equal(requestId, id.GetString());

        // Should return error for invalid process
        Assert.True(root.TryGetProperty("error", out var error));
        Assert.True(error.TryGetProperty("message", out var message));
        var errorMessage = message.GetString();
        Assert.NotNull(errorMessage);
        Assert.Contains("attach", errorMessage.ToLowerInvariant());
    }

    [Fact]
    public async Task DebugSetBreakpoint_ReturnsBreakpointInfo()
    {
        // Arrange
        var requestId = Guid.NewGuid().ToString();
        var request = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = requestId,
            method = "debug.setBreakpoint",
            @params = new
            {
                file = @"C:\test\program.cs",
                line = 10,
                condition = "variable > 5"
            }
        });

        // Act
        var response = await SendRequestAsync(request);

        // Assert
        Assert.NotNull(response);
        var jsonDoc = JsonDocument.Parse(response);
        var root = jsonDoc.RootElement;

        Assert.True(root.TryGetProperty("jsonrpc", out var jsonrpc));
        Assert.Equal("2.0", jsonrpc.GetString());

        Assert.True(root.TryGetProperty("id", out var id));
        Assert.Equal(requestId, id.GetString());

        // Should return result with breakpoint info or error
        if (root.TryGetProperty("result", out var result))
        {
            Assert.True(result.TryGetProperty("data", out var data));
            Assert.True(data.TryGetProperty("id", out _));
            Assert.True(data.TryGetProperty("file", out _));
            Assert.True(data.TryGetProperty("line", out _));
        }
        else
        {
            // If no active debug session, should return error
            Assert.True(root.TryGetProperty("error", out _));
        }
    }

    [Fact]
    public async Task DebugListBreakpoints_ReturnsBreakpointArray()
    {
        // Arrange
        var requestId = Guid.NewGuid().ToString();
        var request = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = requestId,
            method = "debug.listBreakpoints"
        });

        // Act
        var response = await SendRequestAsync(request);

        // Assert
        Assert.NotNull(response);
        var jsonDoc = JsonDocument.Parse(response);
        var root = jsonDoc.RootElement;

        Assert.True(root.TryGetProperty("jsonrpc", out var jsonrpc));
        Assert.Equal("2.0", jsonrpc.GetString());

        Assert.True(root.TryGetProperty("id", out var id));
        Assert.Equal(requestId, id.GetString());

        Assert.True(root.TryGetProperty("result", out var result));
        Assert.True(result.TryGetProperty("data", out var data));
        Assert.Equal(JsonValueKind.Array, data.ValueKind);
    }

    [Fact]
    public async Task InvalidMethod_ReturnsMethodNotFoundError()
    {
        // Arrange
        var requestId = Guid.NewGuid().ToString();
        var request = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = requestId,
            method = "nonexistent.method"
        });

        // Act
        var response = await SendRequestAsync(request);

        // Assert
        Assert.NotNull(response);
        var jsonDoc = JsonDocument.Parse(response);
        var root = jsonDoc.RootElement;

        Assert.True(root.TryGetProperty("jsonrpc", out var jsonrpc));
        Assert.Equal("2.0", jsonrpc.GetString());

        Assert.True(root.TryGetProperty("id", out var id));
        Assert.Equal(requestId, id.GetString());

        Assert.True(root.TryGetProperty("error", out var error));
        Assert.True(error.TryGetProperty("code", out var code));
        Assert.Equal(-32601, code.GetInt32()); // Method not found
    }

    [Fact]
    public async Task ConcurrentRequests_AllReceiveResponses()
    {
        // Arrange
        var requests = new[]
        {
            ("health", JsonSerializer.Serialize(new { jsonrpc = "2.0", id = "req1", method = "health" })),
            ("status", JsonSerializer.Serialize(new { jsonrpc = "2.0", id = "req2", method = "debug.getStatus" })),
            ("threads", JsonSerializer.Serialize(new { jsonrpc = "2.0", id = "req3", method = "debug.getThreads" })),
            ("breakpoints", JsonSerializer.Serialize(new { jsonrpc = "2.0", id = "req4", method = "debug.listBreakpoints" }))
        };

        // Act - Send all requests concurrently
        var responseTasks = requests.Select(async (req, index) =>
        {
            var response = await SendRequestAsync(req.Item2);
            return (Name: req.Item1, Response: response);
        }).ToArray();

        var results = await Task.WhenAll(responseTasks);

        // Assert - All requests should receive proper JSON-RPC responses
        Assert.Equal(requests.Length, results.Length);

        foreach (var result in results)
        {
            Assert.NotNull(result.Response);
            var jsonDoc = JsonDocument.Parse(result.Response);
            var root = jsonDoc.RootElement;

            Assert.True(root.TryGetProperty("jsonrpc", out var jsonrpc));
            Assert.Equal("2.0", jsonrpc.GetString());

            Assert.True(root.TryGetProperty("id", out var id));
            Assert.True(id.GetString()?.StartsWith("req"));

            // Should have either result or error
            Assert.True(root.TryGetProperty("result", out _) || root.TryGetProperty("error", out _));
        }
    }

    private async Task<string> SendRequestAsync(string request)
    {
        if (_serverProcess == null)
        {
            throw new InvalidOperationException("Server process is not running");
        }

        // Send the request - serialize writes to avoid concurrent writers on the stream
        await _stdinLock.WaitAsync();
        try
        {
            await _serverProcess.StandardInput.WriteLineAsync(request);
            await _serverProcess.StandardInput.FlushAsync();
        }
        finally
        {
            _stdinLock.Release();
        }

        // Read the response with timeout. Multiple concurrent callers must not call
        // ReadLineAsync on the same StreamReader concurrently, so serialize reads
        // using a semaphore.
        await _stdoutLock.WaitAsync();
        try
        {
            var responseTask = _serverProcess.StandardOutput.ReadLineAsync();
            var timeoutTask = Task.Delay(5000);

            var completedTask = await Task.WhenAny(responseTask, timeoutTask);

            if (completedTask == timeoutTask)
            {
                throw new TimeoutException("Request timed out");
            }

            var response = await responseTask;
            return response ?? throw new InvalidOperationException("Received null response");
        }
        finally
        {
            _stdoutLock.Release();
        }
    }

    private async Task BuildServerAsync()
    {
        var projectRoot = FindProjectRoot(Directory.GetCurrentDirectory());

        var buildProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "build",
                WorkingDirectory = projectRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        buildProcess.Start();
        await buildProcess.WaitForExitAsync();

        if (buildProcess.ExitCode != 0)
        {
            var error = await buildProcess.StandardError.ReadToEndAsync();
            throw new InvalidOperationException($"Build failed: {error}");
        }
    }

    private static string FindProjectRoot(string startPath)
    {
        var current = startPath;
        while (current != null)
        {
            if (File.Exists(Path.Combine(current, "DebugOpsMCP.sln")))
            {
                return current;
            }
            current = Directory.GetParent(current)?.FullName;
        }
        throw new DirectoryNotFoundException("Could not find project root containing DebugOpsMCP.sln");
    }
}