using DebugOpsMCP.Core.Hosting;
using DebugOpsMCP.Core.Debug;
using DebugOpsMCP.Core.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text;

namespace DebugOpsMCP.Host;

/// <summary>
/// Main entry point for DebugOpsMCP server
/// Handles stdio communication with VS Code extension
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        // Configure host and services
        var host = CreateHostBuilder(args).Build();
        
        // Get the MCP host service
        var mcpHost = host.Services.GetRequiredService<McpHost>();
        var logger = host.Services.GetRequiredService<ILogger<Program>>();

        logger.LogInformation("DebugOpsMCP server starting...");

        try
        {
            // Start background services
            await host.StartAsync();

            // Main message loop - read from stdin, process, write to stdout
            await RunMessageLoopAsync(mcpHost, logger);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Fatal error in DebugOpsMCP server");
            throw;
        }
        finally
        {
            await host.StopAsync();
        }
    }

    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                // Register core services
                services.AddSingleton<McpHost>();
                services.AddSingleton<IDebugBridge, ExtensionMediatedDebugBridge>();
                
                // Register debug tools
                services.AddSingleton<IDebugLifecycleTool, DebugLifecycleTool>();
                services.AddSingleton<IDebugExecutionTool, DebugExecutionTool>();
                services.AddSingleton<IDebugBreakpointTool, DebugBreakpointTool>();
                services.AddSingleton<IDebugInspectionTool, DebugInspectionTool>();
                services.AddSingleton<IDebugThreadTool, DebugThreadTool>();
                services.AddSingleton<IDebugStatusTool, DebugStatusTool>();
                
                // Configure logging
                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Information);
                    
                    // In production, we might want to log to file instead of console
                    // to avoid interfering with MCP stdio communication
                });
            });

    private static async Task RunMessageLoopAsync(McpHost mcpHost, ILogger logger)
    {
        logger.LogInformation("Starting MCP message loop on stdio");

        using var stdin = Console.OpenStandardInput();
        using var stdout = Console.OpenStandardOutput();
        using var reader = new StreamReader(stdin, Encoding.UTF8);
        using var writer = new StreamWriter(stdout, Encoding.UTF8) { AutoFlush = true };

        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            try
            {
                // Process the MCP request
                var response = await mcpHost.ProcessRequestAsync(line);
                
                // Write response to stdout
                await writer.WriteLineAsync(response);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing MCP request: {Request}", line);
                
                // Send error response
                var errorResponse = """{"success":false,"error":{"code":"PROCESSING_ERROR","message":"Failed to process request"}}""";
                await writer.WriteLineAsync(errorResponse);
            }
        }

        logger.LogInformation("MCP message loop ended - stdin closed");
    }
}