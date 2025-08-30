using System.Diagnostics;
using System.Text;
using Xunit;

namespace DebugOpsMCP.Integration.Tests;

public class McpServerIntegrationTests : IAsyncLifetime
{
    private Process? _serverProcess;
    private readonly string _serverPath;

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
        var request = """{"method":"health"}""";

        // Act
        var response = await SendRequestAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.Contains("success", response);
        Assert.Contains("true", response);
    }

    [Fact]
    public async Task DebugAttach_ReturnsNotImplemented()
    {
        // Arrange
        var request = """{"method":"debug.attach","processId":1234}""";

        // Act
        var response = await SendRequestAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.Contains("NOT_IMPLEMENTED", response);
        Assert.Contains("debug.attach", response);
    }

    [Fact]
    public async Task MultipleRequests_AllGetResponses()
    {
        // Arrange
        var requests = new[]
        {
            """{"method":"health"}""",
            """{"method":"debug.getStatus"}""",
            """{"method":"debug.getThreads"}"""
        };

        // Act & Assert
        foreach (var request in requests)
        {
            var response = await SendRequestAsync(request);
            Assert.NotNull(response);
            // All should at least have a success field or error
            Assert.True(response.Contains("success") || response.Contains("error"));
        }
    }

    private async Task<string> SendRequestAsync(string request)
    {
        if (_serverProcess == null)
        {
            throw new InvalidOperationException("Server process is not running");
        }

        // Send the request
        await _serverProcess.StandardInput.WriteLineAsync(request);
        await _serverProcess.StandardInput.FlushAsync();

        // Read the response with timeout
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