using System.Text.Json.Serialization;

namespace DebugOpsMCP.Contracts;

/// <summary>
/// Base class for Debug Adapter Protocol requests
/// </summary>
public abstract class DapRequest
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "request";

    [JsonPropertyName("command")]
    public abstract string Command { get; }

    [JsonPropertyName("seq")]
    public int Seq { get; set; }
}

/// <summary>
/// Base class for Debug Adapter Protocol responses
/// </summary>
public abstract class DapResponse
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "response";

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("command")]
    public string Command { get; set; } = "";

    [JsonPropertyName("request_seq")]
    public int RequestSeq { get; set; }

    [JsonPropertyName("seq")]
    public int Seq { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

// Execution Control DAP Requests

/// <summary>
/// DAP continue request
/// </summary>
public class DapContinueRequest : DapRequest
{
    public override string Command => "continue";

    [JsonPropertyName("arguments")]
    public DapContinueArguments Arguments { get; set; } = new();
}

public class DapContinueArguments
{
    [JsonPropertyName("threadId")]
    public int ThreadId { get; set; }
}

/// <summary>
/// DAP continue response
/// </summary>
public class DapContinueResponse : DapResponse
{
    [JsonPropertyName("body")]
    public DapContinueBody? Body { get; set; }
}

public class DapContinueBody
{
    [JsonPropertyName("allThreadsContinued")]
    public bool? AllThreadsContinued { get; set; }
}

/// <summary>
/// DAP pause request
/// </summary>
public class DapPauseRequest : DapRequest
{
    public override string Command => "pause";

    [JsonPropertyName("arguments")]
    public DapPauseArguments Arguments { get; set; } = new();
}

public class DapPauseArguments
{
    [JsonPropertyName("threadId")]
    public int ThreadId { get; set; }
}

/// <summary>
/// DAP pause response
/// </summary>
public class DapPauseResponse : DapResponse
{
}

/// <summary>
/// DAP next (step over) request
/// </summary>
public class DapNextRequest : DapRequest
{
    public override string Command => "next";

    [JsonPropertyName("arguments")]
    public DapNextArguments Arguments { get; set; } = new();
}

public class DapNextArguments
{
    [JsonPropertyName("threadId")]
    public int ThreadId { get; set; }

    [JsonPropertyName("granularity")]
    public string? Granularity { get; set; } = "statement";
}

/// <summary>
/// DAP next response
/// </summary>
public class DapNextResponse : DapResponse
{
}

/// <summary>
/// DAP step in request
/// </summary>
public class DapStepInRequest : DapRequest
{
    public override string Command => "stepIn";

    [JsonPropertyName("arguments")]
    public DapStepInArguments Arguments { get; set; } = new();
}

public class DapStepInArguments
{
    [JsonPropertyName("threadId")]
    public int ThreadId { get; set; }

    [JsonPropertyName("granularity")]
    public string? Granularity { get; set; } = "statement";
}

/// <summary>
/// DAP step in response
/// </summary>
public class DapStepInResponse : DapResponse
{
}

/// <summary>
/// DAP step out request
/// </summary>
public class DapStepOutRequest : DapRequest
{
    public override string Command => "stepOut";

    [JsonPropertyName("arguments")]
    public DapStepOutArguments Arguments { get; set; } = new();
}

public class DapStepOutArguments
{
    [JsonPropertyName("threadId")]
    public int ThreadId { get; set; }

    [JsonPropertyName("granularity")]
    public string? Granularity { get; set; } = "statement";
}

/// <summary>
/// DAP step out response
/// </summary>
public class DapStepOutResponse : DapResponse
{
}

// Inspection DAP Requests

/// <summary>
/// DAP stack trace request
/// </summary>
public class DapStackTraceRequest : DapRequest
{
    public override string Command => "stackTrace";

    [JsonPropertyName("arguments")]
    public DapStackTraceArguments Arguments { get; set; } = new();
}

public class DapStackTraceArguments
{
    [JsonPropertyName("threadId")]
    public int ThreadId { get; set; }

    [JsonPropertyName("startFrame")]
    public int? StartFrame { get; set; }

    [JsonPropertyName("levels")]
    public int? Levels { get; set; }
}

/// <summary>
/// DAP stack trace response
/// </summary>
public class DapStackTraceResponse : DapResponse
{
    [JsonPropertyName("body")]
    public DapStackTraceBody? Body { get; set; }
}

public class DapStackTraceBody
{
    [JsonPropertyName("stackFrames")]
    public DapStackFrame[] StackFrames { get; set; } = Array.Empty<DapStackFrame>();

    [JsonPropertyName("totalFrames")]
    public int? TotalFrames { get; set; }
}

public class DapStackFrame
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("source")]
    public DapSource? Source { get; set; }

    [JsonPropertyName("line")]
    public int Line { get; set; }

    [JsonPropertyName("column")]
    public int Column { get; set; }

    [JsonPropertyName("endLine")]
    public int? EndLine { get; set; }

    [JsonPropertyName("endColumn")]
    public int? EndColumn { get; set; }
}

public class DapSource
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("path")]
    public string? Path { get; set; }

    [JsonPropertyName("sourceReference")]
    public int? SourceReference { get; set; }
}

/// <summary>
/// DAP variables request
/// </summary>
public class DapVariablesRequest : DapRequest
{
    public override string Command => "variables";

    [JsonPropertyName("arguments")]
    public DapVariablesArguments Arguments { get; set; } = new();
}

public class DapVariablesArguments
{
    [JsonPropertyName("variablesReference")]
    public int VariablesReference { get; set; }

    [JsonPropertyName("filter")]
    public string? Filter { get; set; }

    [JsonPropertyName("start")]
    public int? Start { get; set; }

    [JsonPropertyName("count")]
    public int? Count { get; set; }
}

/// <summary>
/// DAP variables response
/// </summary>
public class DapVariablesResponse : DapResponse
{
    [JsonPropertyName("body")]
    public DapVariablesBody? Body { get; set; }
}

public class DapVariablesBody
{
    [JsonPropertyName("variables")]
    public DapVariable[] Variables { get; set; } = Array.Empty<DapVariable>();
}

public class DapVariable
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("value")]
    public string Value { get; set; } = "";

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("variablesReference")]
    public int VariablesReference { get; set; }

    [JsonPropertyName("indexedVariables")]
    public int? IndexedVariables { get; set; }

    [JsonPropertyName("namedVariables")]
    public int? NamedVariables { get; set; }
}

/// <summary>
/// DAP evaluate request
/// </summary>
public class DapEvaluateRequest : DapRequest
{
    public override string Command => "evaluate";

    [JsonPropertyName("arguments")]
    public DapEvaluateArguments Arguments { get; set; } = new();
}

public class DapEvaluateArguments
{
    [JsonPropertyName("expression")]
    public string Expression { get; set; } = "";

    [JsonPropertyName("frameId")]
    public int? FrameId { get; set; }

    [JsonPropertyName("context")]
    public string? Context { get; set; } = "repl";
}

/// <summary>
/// DAP evaluate response
/// </summary>
public class DapEvaluateResponse : DapResponse
{
    [JsonPropertyName("body")]
    public DapEvaluateBody? Body { get; set; }
}

public class DapEvaluateBody
{
    [JsonPropertyName("result")]
    public string Result { get; set; } = "";

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("variablesReference")]
    public int VariablesReference { get; set; }

    [JsonPropertyName("indexedVariables")]
    public int? IndexedVariables { get; set; }

    [JsonPropertyName("namedVariables")]
    public int? NamedVariables { get; set; }
}

/// <summary>
/// DAP threads request
/// </summary>
public class DapThreadsRequest : DapRequest
{
    public override string Command => "threads";
}

/// <summary>
/// DAP threads response
/// </summary>
public class DapThreadsResponse : DapResponse
{
    [JsonPropertyName("body")]
    public DapThreadsBody? Body { get; set; }
}

public class DapThreadsBody
{
    [JsonPropertyName("threads")]
    public DapThread[] Threads { get; set; } = Array.Empty<DapThread>();
}

public class DapThread
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}