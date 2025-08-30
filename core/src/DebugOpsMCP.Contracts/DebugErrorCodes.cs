namespace DebugOpsMCP.Contracts;

/// <summary>
/// Standard MCP error codes for debug operations as documented in the API reference
/// </summary>
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
    public const string NO_DEBUG_SESSION = "NO_DEBUG_SESSION";
    
    // Execution control errors
    public const string EXECUTION_FAILED = "DEBUG_EXECUTION_FAILED";
    public const string STEP_FAILED = "DEBUG_STEP_FAILED";
    public const string CONTINUE_FAILED = "DEBUG_CONTINUE_FAILED";
    
    // Breakpoint errors
    public const string BREAKPOINT_SET_FAILED = "BREAKPOINT_SET_FAILED";
    public const string BREAKPOINT_REMOVE_FAILED = "BREAKPOINT_REMOVE_FAILED";
    public const string BREAKPOINT_NOT_FOUND = "BREAKPOINT_NOT_FOUND";
    public const string BREAKPOINT_LIST_FAILED = "BREAKPOINT_LIST_FAILED";
    
    // Inspection errors
    public const string STACK_TRACE_FAILED = "STACK_TRACE_FAILED";
    public const string VARIABLES_FAILED = "GET_VARIABLES_FAILED";
    public const string EVALUATION_FAILED = "EVALUATION_FAILED";
    
    // Thread errors
    public const string THREAD_NOT_FOUND = "THREAD_NOT_FOUND";
    public const string THREAD_OPERATION_FAILED = "THREAD_OPERATION_FAILED";
    public const string GET_THREADS_FAILED = "GET_THREADS_FAILED";
    public const string SELECT_THREAD_FAILED = "SELECT_THREAD_FAILED";
    public const string GET_STATUS_FAILED = "GET_STATUS_FAILED";
    
    // General errors
    public const string INTERNAL_ERROR = "INTERNAL_ERROR";
    public const string TIMEOUT = "TIMEOUT";
    public const string ACCESS_DENIED = "ACCESS_DENIED";
    public const string NOT_IMPLEMENTED = "NOT_IMPLEMENTED";
}