using System.Text.Json.Serialization;

namespace DebugOpsMCP.Contracts;

/// <summary>
/// Base class for all MCP request payloads
/// </summary>
public abstract class McpRequest
{
    [JsonPropertyName("method")]
    public required string Method { get; set; }
}

/// <summary>
/// Base class for all MCP response payloads
/// </summary>
public abstract class McpResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

/// <summary>
/// Generic MCP response with typed result data
/// </summary>
public class McpResponse<T> : McpResponse
{
    [JsonPropertyName("result")]
    public T? Result { get; set; }
}

/// <summary>
/// MCP error response with structured error information
/// </summary>
public class McpErrorResponse : McpResponse
{
    [JsonPropertyName("error")]
    public required McpError Error { get; set; }

    public McpErrorResponse()
    {
        Success = false;
    }
}

/// <summary>
/// Structured error information for MCP responses
/// </summary>
public class McpError
{
    [JsonPropertyName("code")]
    public required string Code { get; set; }

    [JsonPropertyName("message")]
    public required string Message { get; set; }

    [JsonPropertyName("details")]
    public Dictionary<string, object>? Details { get; set; }
}