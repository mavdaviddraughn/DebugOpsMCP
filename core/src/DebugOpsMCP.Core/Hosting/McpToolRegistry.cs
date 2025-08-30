using DebugOpsMCP.Contracts;
using DebugOpsMCP.Core.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Reflection;

namespace DebugOpsMCP.Core.Hosting;

/// <summary>
/// Registry for MCP tools that handles automatic discovery and routing
/// </summary>
public interface IMcpToolRegistry
{
    /// <summary>
    /// Register all available tools
    /// </summary>
    void RegisterTools();

    /// <summary>
    /// Get a tool handler for the specified method
    /// </summary>
    IDebugTool? GetTool(string method);

    /// <summary>
    /// Get all registered methods
    /// </summary>
    IEnumerable<string> GetRegisteredMethods();

    /// <summary>
    /// Check if a method is registered
    /// </summary>
    bool IsMethodRegistered(string method);
}

/// <summary>
/// Tool registration information
/// </summary>
public class ToolRegistration
{
    public required string Method { get; set; }
    public required Type ToolType { get; set; }
    public required Type RequestType { get; set; }
    public string? Description { get; set; }
    public string[] Tags { get; set; } = [];
    public bool IsEnabled { get; set; } = true;
}

/// <summary>
/// Attribute to mark methods that tools can handle
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class McpMethodAttribute : Attribute
{
    public string Method { get; }
    public Type RequestType { get; }
    public string? Description { get; }
    public string[] Tags { get; }

    public McpMethodAttribute(string method, Type requestType, string? description = null, params string[] tags)
    {
        Method = method;
        RequestType = requestType;
        Description = description;
        Tags = tags ?? [];
    }
}

/// <summary>
/// Default MCP tool registry implementation
/// </summary>
public class McpToolRegistry : IMcpToolRegistry
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<McpToolRegistry> _logger;
    private readonly ConcurrentDictionary<string, ToolRegistration> _tools = new();
    private readonly ConcurrentDictionary<string, IDebugTool> _toolInstances = new();

    public McpToolRegistry(IServiceProvider serviceProvider, ILogger<McpToolRegistry> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public void RegisterTools()
    {
        _logger.LogInformation("Starting MCP tool registration");

        try
        {
            // Discover tools by scanning assemblies
            var toolTypes = DiscoverToolTypes();
            
            foreach (var toolType in toolTypes)
            {
                RegisterToolType(toolType);
            }

            // Register built-in health check
            RegisterBuiltInMethods();

            _logger.LogInformation("MCP tool registration completed. Registered {Count} methods: {Methods}", 
                _tools.Count, string.Join(", ", _tools.Keys));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register MCP tools");
            throw;
        }
    }

    public IDebugTool? GetTool(string method)
    {
        if (!_tools.TryGetValue(method, out var registration))
        {
            return null;
        }

        if (!registration.IsEnabled)
        {
            _logger.LogWarning("Tool for method {Method} is disabled", method);
            return null;
        }

        // Use cached instance or create new one
        return _toolInstances.GetOrAdd(method, _ => CreateToolInstance(registration.ToolType));
    }

    public IEnumerable<string> GetRegisteredMethods()
    {
        return _tools.Keys.Where(method => _tools[method].IsEnabled);
    }

    public bool IsMethodRegistered(string method)
    {
        return _tools.ContainsKey(method) && _tools[method].IsEnabled;
    }

    private IEnumerable<Type> DiscoverToolTypes()
    {
        var assembly = Assembly.GetExecutingAssembly();
        return assembly.GetTypes()
            .Where(type => 
                typeof(IDebugTool).IsAssignableFrom(type) && 
                !type.IsInterface && 
                !type.IsAbstract &&
                type.GetCustomAttributes<McpMethodAttribute>().Any());
    }

    private void RegisterToolType(Type toolType)
    {
        var methodAttributes = toolType.GetCustomAttributes<McpMethodAttribute>();
        
        foreach (var methodAttr in methodAttributes)
        {
            try
            {
                var registration = new ToolRegistration
                {
                    Method = methodAttr.Method,
                    ToolType = toolType,
                    RequestType = methodAttr.RequestType,
                    Description = methodAttr.Description,
                    Tags = methodAttr.Tags,
                    IsEnabled = true
                };

                if (_tools.TryAdd(methodAttr.Method, registration))
                {
                    _logger.LogDebug("Registered tool {ToolType} for method {Method}", 
                        toolType.Name, methodAttr.Method);
                }
                else
                {
                    _logger.LogWarning("Method {Method} is already registered by another tool", methodAttr.Method);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register method {Method} for tool {ToolType}", 
                    methodAttr.Method, toolType.Name);
            }
        }
    }

    private void RegisterBuiltInMethods()
    {
        // Register health check as a built-in method
        var healthRegistration = new ToolRegistration
        {
            Method = "health",
            ToolType = typeof(HealthCheckTool), // We'll create this
            RequestType = typeof(McpRequest),
            Description = "System health check",
            Tags = ["system", "health"],
            IsEnabled = true
        };

        _tools.TryAdd("health", healthRegistration);
    }

    private IDebugTool CreateToolInstance(Type toolType)
    {
        try
        {
            return (IDebugTool)ActivatorUtilities.CreateInstance(_serviceProvider, toolType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create instance of tool {ToolType}", toolType.Name);
            throw;
        }
    }
}

/// <summary>
/// Built-in health check tool
/// </summary>
public class HealthCheckTool : IDebugTool
{
    private readonly ILogger<HealthCheckTool> _logger;

    public HealthCheckTool(ILogger<HealthCheckTool> logger)
    {
        _logger = logger;
    }

    public async Task<McpResponse> HandleRequestAsync(McpRequest request)
    {
        _logger.LogInformation("Health check requested");
        
        return await Task.FromResult(new McpResponse<string>
        {
            Success = true,
            Data = "DebugOpsMCP server is running"
        });
    }

    public bool CanHandle(string method)
    {
        return method == "health";
    }
}