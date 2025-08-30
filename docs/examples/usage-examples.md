# DebugOpsMCP Usage Examples

## Table of Contents

1. [Basic Debugging Workflow](#basic-debugging-workflow)
2. [AI Assistant Integration](#ai-assistant-integration)
3. [Advanced Scenarios](#advanced-scenarios)
4. [VS Code Extension Usage](#vs-code-extension-usage)
5. [Custom Tool Examples](#custom-tool-examples)
6. [Error Handling Patterns](#error-handling-patterns)

## Basic Debugging Workflow

### Complete Debug Session

This example demonstrates a complete debugging workflow from attachment to inspection.

```python
import json
import asyncio
from debugops_mcp_client import DebugOpsMCPClient

async def complete_debug_session():
    """Complete debugging workflow example"""
    client = DebugOpsMCPClient("DebugOpsMCP.Host.dll")
    
    try:
        # 1. Health check
        health_response = await client.send_request("health")
        print(f"Server status: {health_response['result']['data']}")
        
        # 2. Attach to running process
        attach_response = await client.send_request("debug.attach", {
            "processId": 1234,
            "configuration": {
                "stopOnEntry": False,
                "justMyCode": True
            }
        })
        
        if attach_response["result"]["success"]:
            session_id = attach_response["result"]["data"]["sessionId"]
            print(f"Attached to session: {session_id}")
            
            # 3. Set breakpoint
            breakpoint_response = await client.send_request("debug.setBreakpoint", {
                "file": "C:\\MyApp\\Program.cs",
                "line": 42,
                "condition": "variable > 10"
            })
            
            if breakpoint_response["result"]["success"]:
                bp_id = breakpoint_response["result"]["data"]["id"]
                print(f"Breakpoint set: {bp_id}")
                
                # 4. Continue execution until breakpoint hit
                continue_response = await client.send_request("debug.continue")
                print("Execution continued, waiting for breakpoint...")
                
                # 5. Get stack trace when stopped
                stack_response = await client.send_request("debug.getStackTrace", {
                    "threadId": 12345,
                    "startFrame": 0,
                    "levels": 10
                })
                
                if stack_response["result"]["success"]:
                    frames = stack_response["result"]["data"]["frames"]
                    print("Call stack:")
                    for i, frame in enumerate(frames):
                        print(f"  {i}: {frame['name']} at {frame['source']['name']}:{frame['line']}")
                
                # 6. Get local variables
                if frames:
                    vars_response = await client.send_request("debug.getVariables", {
                        "frameId": frames[0]["id"],
                        "filter": "locals"
                    })
                    
                    if vars_response["result"]["success"]:
                        variables = vars_response["result"]["data"]
                        print("Local variables:")
                        for var in variables:
                            print(f"  {var['name']}: {var['value']} ({var['type']})")
                
                # 7. Evaluate expression
                eval_response = await client.send_request("debug.evaluate", {
                    "expression": "variable * 2 + 5",
                    "frameId": frames[0]["id"],
                    "context": "watch"
                })
                
                if eval_response["result"]["success"]:
                    result = eval_response["result"]["data"]
                    print(f"Expression result: {result['result']}")
                
                # 8. Step over next line
                step_response = await client.send_request("debug.step", {
                    "threadId": 12345,
                    "stepType": "over"
                })
                print("Stepped over line")
                
        # 9. Detach from session
        detach_response = await client.send_request("debug.detach")
        print("Detached from debug session")
        
    except Exception as e:
        print(f"Debug session error: {e}")
    finally:
        client.close()

# Run the example
asyncio.run(complete_debug_session())
```

### Launch and Debug Program

```python
async def launch_and_debug():
    """Launch a program for debugging"""
    client = DebugOpsMCPClient("DebugOpsMCP.Host.dll")
    
    try:
        # Launch program with debugging
        launch_response = await client.send_request("debug.launch", {
            "program": "C:\\MyApp\\bin\\Debug\\MyApp.exe",
            "args": ["--verbose", "--debug"],
            "configuration": {
                "stopOnEntry": True,
                "workingDirectory": "C:\\MyApp"
            },
            "environment": {
                "DEBUG_MODE": "1",
                "LOG_LEVEL": "Debug"
            }
        })
        
        if launch_response["result"]["success"]:
            session_id = launch_response["result"]["data"]["sessionId"]
            print(f"Launched program in session: {session_id}")
            
            # Program is stopped at entry point
            # Get current position
            stack_response = await client.send_request("debug.getStackTrace", {
                "threadId": 12345
            })
            
            if stack_response["result"]["success"]:
                frames = stack_response["result"]["data"]["frames"]
                current_frame = frames[0]
                print(f"Stopped at: {current_frame['source']['name']}:{current_frame['line']}")
            
            # Continue to main logic
            continue_response = await client.send_request("debug.continue")
            print("Continued to main execution")
            
    except Exception as e:
        print(f"Launch error: {e}")
    finally:
        client.close()

asyncio.run(launch_and_debug())
```

## AI Assistant Integration

### OpenAI Function Calling

```python
import openai
from debugops_mcp_client import DebugOpsMCPClient

class DebugAssistant:
    def __init__(self, server_path, openai_api_key):
        self.client = DebugOpsMCPClient(server_path)
        self.openai_client = openai.Client(api_key=openai_api_key)
    
    async def debug_with_ai(self, process_id, question):
        """Use AI to help debug a process"""
        
        # Define available debug functions for AI
        functions = [
            {
                "name": "attach_debugger",
                "description": "Attach debugger to a running process",
                "parameters": {
                    "type": "object",
                    "properties": {
                        "processId": {"type": "integer", "description": "Process ID to attach to"}
                    },
                    "required": ["processId"]
                }
            },
            {
                "name": "set_breakpoint", 
                "description": "Set breakpoint in source code",
                "parameters": {
                    "type": "object",
                    "properties": {
                        "file": {"type": "string", "description": "Source file path"},
                        "line": {"type": "integer", "description": "Line number"},
                        "condition": {"type": "string", "description": "Optional condition for breakpoint"}
                    },
                    "required": ["file", "line"]
                }
            },
            {
                "name": "get_stack_trace",
                "description": "Get current call stack",
                "parameters": {
                    "type": "object",
                    "properties": {
                        "threadId": {"type": "integer", "description": "Thread ID"}
                    },
                    "required": ["threadId"]
                }
            },
            {
                "name": "get_variables",
                "description": "Get variables in current scope", 
                "parameters": {
                    "type": "object",
                    "properties": {
                        "frameId": {"type": "string", "description": "Stack frame ID"}
                    },
                    "required": ["frameId"]
                }
            },
            {
                "name": "evaluate_expression",
                "description": "Evaluate expression in debug context",
                "parameters": {
                    "type": "object",
                    "properties": {
                        "expression": {"type": "string", "description": "Expression to evaluate"},
                        "frameId": {"type": "string", "description": "Stack frame ID"}
                    },
                    "required": ["expression", "frameId"]
                }
            }
        ]
        
        # Start conversation with AI
        messages = [
            {"role": "system", "content": "You are a debugging assistant. Help the user debug their program by using the available debugging functions."},
            {"role": "user", "content": f"I need help debugging process {process_id}. {question}"}
        ]
        
        response = await self.openai_client.chat.completions.create(
            model="gpt-4",
            messages=messages,
            functions=functions,
            function_call="auto"
        )
        
        # Process AI response and execute debug commands
        message = response.choices[0].message
        
        if message.function_call:
            function_name = message.function_call.name
            function_args = json.loads(message.function_call.arguments)
            
            # Execute the debug command
            result = await self.execute_debug_function(function_name, function_args)
            
            # Send result back to AI for analysis
            messages.append({
                "role": "function",
                "name": function_name,
                "content": json.dumps(result)
            })
            
            # Get AI analysis of the debug data
            analysis_response = await self.openai_client.chat.completions.create(
                model="gpt-4",
                messages=messages + [
                    {"role": "user", "content": "Please analyze this debug information and provide insights."}
                ]
            )
            
            return analysis_response.choices[0].message.content
        
        return message.content
    
    async def execute_debug_function(self, function_name, args):
        """Execute debug function based on AI request"""
        try:
            if function_name == "attach_debugger":
                return await self.client.send_request("debug.attach", args)
            elif function_name == "set_breakpoint":
                return await self.client.send_request("debug.setBreakpoint", args)
            elif function_name == "get_stack_trace":
                return await self.client.send_request("debug.getStackTrace", args)
            elif function_name == "get_variables":
                return await self.client.send_request("debug.getVariables", args)
            elif function_name == "evaluate_expression":
                return await self.client.send_request("debug.evaluate", {
                    "expression": args["expression"],
                    "frameId": args["frameId"],
                    "context": "watch"
                })
            else:
                return {"error": f"Unknown function: {function_name}"}
        except Exception as e:
            return {"error": str(e)}

# Usage example
async def ai_debug_session():
    assistant = DebugAssistant("DebugOpsMCP.Host.dll", "your-openai-api-key")
    
    result = await assistant.debug_with_ai(
        1234, 
        "My application is crashing with a null reference exception. Can you help me find where it's happening?"
    )
    
    print(f"AI Analysis: {result}")

asyncio.run(ai_debug_session())
```

### Anthropic Claude Integration

```python
import anthropic
from debugops_mcp_client import DebugOpsMCPClient

class ClaudeDebugAssistant:
    def __init__(self, server_path, anthropic_api_key):
        self.client = DebugOpsMCPClient(server_path)
        self.anthropic_client = anthropic.Anthropic(api_key=anthropic_api_key)
        self.session_context = []
    
    async def analyze_crash(self, process_id, crash_description):
        """Use Claude to analyze a program crash"""
        
        try:
            # Attach to the crashed process (if still running) or core dump
            attach_response = await self.client.send_request("debug.attach", {
                "processId": process_id
            })
            
            if not attach_response["result"]["success"]:
                return "Could not attach to process for analysis."
            
            # Get call stack
            stack_response = await self.client.send_request("debug.getStackTrace", {
                "threadId": 12345
            })
            
            stack_info = ""
            current_frame_id = None
            
            if stack_response["result"]["success"]:
                frames = stack_response["result"]["data"]["frames"]
                current_frame_id = frames[0]["id"]
                
                stack_info = "Call Stack:\n"
                for i, frame in enumerate(frames):
                    stack_info += f"{i}: {frame['name']} at {frame['source']['path']}:{frame['line']}\n"
            
            # Get variables in the top frame
            variables_info = ""
            if current_frame_id:
                vars_response = await self.client.send_request("debug.getVariables", {
                    "frameId": current_frame_id
                })
                
                if vars_response["result"]["success"]:
                    variables = vars_response["result"]["data"]
                    variables_info = "Variables:\n"
                    for var in variables[:10]:  # Limit to first 10 variables
                        variables_info += f"  {var['name']}: {var['value']} ({var['type']})\n"
            
            # Create prompt for Claude
            prompt = f"""
I need help analyzing a program crash. Here's the information I have:

Crash Description: {crash_description}

{stack_info}

{variables_info}

Please analyze this crash and provide:
1. The most likely cause of the crash
2. Which variables or code locations to investigate further
3. Specific debugging steps to identify the root cause
4. Prevention strategies for similar crashes

Focus on practical, actionable insights based on the stack trace and variable values.
"""
            
            # Get Claude's analysis
            response = await self.anthropic_client.messages.create(
                model="claude-3-sonnet-20240229",
                max_tokens=1000,
                messages=[
                    {"role": "user", "content": prompt}
                ]
            )
            
            return response.content[0].text
            
        except Exception as e:
            return f"Error during crash analysis: {e}"
    
    async def guided_debugging_session(self, process_id, problem_description):
        """Interactive debugging session with Claude guidance"""
        
        conversation_history = [
            {"role": "user", "content": f"I need help debugging process {process_id}. Problem: {problem_description}"}
        ]
        
        # Start debugging session
        await self.client.send_request("debug.attach", {"processId": process_id})
        
        while True:
            # Get Claude's debugging suggestions
            prompt = f"""
Based on the conversation history, what should be the next debugging step?

Previous conversation:
{json.dumps(conversation_history, indent=2)}

Current debugging capabilities available:
- Set breakpoints at specific locations
- Get call stack and examine frames  
- Inspect variables in any scope
- Evaluate expressions
- Step through code execution
- Continue or pause execution

Please suggest the next specific debugging action to take, including exact parameters if needed.
If you think we have enough information to identify the issue, provide your analysis.
"""
            
            response = await self.anthropic_client.messages.create(
                model="claude-3-sonnet-20240229", 
                max_tokens=500,
                messages=[{"role": "user", "content": prompt}]
            )
            
            suggestion = response.content[0].text
            print(f"Claude suggests: {suggestion}")
            
            # Parse suggestion and execute if it's a debug command
            # This would need more sophisticated parsing in a real implementation
            user_input = input("Press Enter to continue, 'q' to quit, or enter debug command: ")
            
            if user_input.lower() == 'q':
                break
            
            conversation_history.append({"role": "assistant", "content": suggestion})
            
            if user_input:
                conversation_history.append({"role": "user", "content": user_input})

# Usage
async def claude_crash_analysis():
    assistant = ClaudeDebugAssistant("DebugOpsMCP.Host.dll", "your-anthropic-api-key")
    
    analysis = await assistant.analyze_crash(
        1234,
        "Application crashes when processing large datasets with System.NullReferenceException"
    )
    
    print(analysis)

asyncio.run(claude_crash_analysis())
```

## Advanced Scenarios

### Multi-threaded Application Debugging

```python
async def debug_multithreaded_app():
    """Debug a multi-threaded application with race conditions"""
    client = DebugOpsMCPClient("DebugOpsMCP.Host.dll")
    
    try:
        # Attach to multi-threaded process
        await client.send_request("debug.attach", {"processId": 5678})
        
        # Get all threads
        threads_response = await client.send_request("debug.getThreads")
        threads = threads_response["result"]["data"]
        
        print(f"Found {len(threads)} threads:")
        for thread in threads:
            print(f"  Thread {thread['id']}: {thread['name']} - {thread['status']}")
        
        # Set breakpoints in critical sections
        critical_sections = [
            ("C:\\App\\DataProcessor.cs", 45, "sharedResource != null"),
            ("C:\\App\\WorkerThread.cs", 123, "lockTaken == true"),
            ("C:\\App\\Manager.cs", 67, "threadCount > 0")
        ]
        
        for file, line, condition in critical_sections:
            bp_response = await client.send_request("debug.setBreakpoint", {
                "file": file,
                "line": line,
                "condition": condition
            })
            print(f"Set breakpoint: {bp_response['result']['data']['id']}")
        
        # Continue and analyze each thread when breakpoints hit
        await client.send_request("debug.continue")
        
        # When breakpoint is hit, analyze all threads
        for thread in threads:
            print(f"\nAnalyzing thread {thread['id']}:")
            
            # Get stack trace for this thread
            stack_response = await client.send_request("debug.getStackTrace", {
                "threadId": thread['id']
            })
            
            if stack_response["result"]["success"]:
                frames = stack_response["result"]["data"]["frames"]
                for frame in frames[:5]:  # Top 5 frames
                    print(f"  {frame['name']} at {frame['line']}")
                
                # Check for common race condition patterns
                if frames:
                    vars_response = await client.send_request("debug.getVariables", {
                        "frameId": frames[0]["id"]
                    })
                    
                    if vars_response["result"]["success"]:
                        variables = vars_response["result"]["data"]
                        
                        # Look for synchronization objects
                        sync_vars = [v for v in variables if 'lock' in v['name'].lower() or 'mutex' in v['name'].lower()]
                        if sync_vars:
                            print("  Synchronization variables:")
                            for var in sync_vars:
                                print(f"    {var['name']}: {var['value']}")
        
    except Exception as e:
        print(f"Multi-threading debug error: {e}")
    finally:
        client.close()

asyncio.run(debug_multithreaded_app())
```

### Memory Leak Investigation

```python
async def investigate_memory_leak():
    """Investigate memory leak patterns"""
    client = DebugOpsMCPClient("DebugOpsMCP.Host.dll")
    
    try:
        await client.send_request("debug.attach", {"processId": 9999})
        
        # Take initial memory snapshot
        initial_memory = await get_memory_info(client)
        print(f"Initial memory usage: {initial_memory}")
        
        # Set breakpoints at allocation points
        allocation_points = [
            ("C:\\App\\DataCache.cs", 34),  # Cache allocation
            ("C:\\App\\ImageProcessor.cs", 78),  # Image buffer allocation  
            ("C:\\App\\Logger.cs", 12)  # Log entry allocation
        ]
        
        for file, line in allocation_points:
            await client.send_request("debug.setBreakpoint", {
                "file": file,
                "line": line
            })
        
        # Monitor allocations over time
        allocation_count = 0
        memory_samples = []
        
        for iteration in range(10):  # Monitor 10 allocation cycles
            await client.send_request("debug.continue")
            
            # Get current position and memory state
            stack_response = await client.send_request("debug.getStackTrace", {
                "threadId": 12345
            })
            
            if stack_response["result"]["success"]:
                frame = stack_response["result"]["data"]["frames"][0]
                
                # Get variables to check allocation size
                vars_response = await client.send_request("debug.getVariables", {
                    "frameId": frame["id"]
                })
                
                allocation_info = {
                    "iteration": iteration,
                    "location": f"{frame['source']['name']}:{frame['line']}",
                    "variables": vars_response["result"]["data"] if vars_response["result"]["success"] else []
                }
                
                # Look for size-related variables
                size_vars = [v for v in allocation_info["variables"] if 'size' in v['name'].lower() or 'length' in v['name'].lower()]
                if size_vars:
                    allocation_info["allocation_size"] = size_vars[0]["value"]
                
                memory_samples.append(allocation_info)
                allocation_count += 1
                
                print(f"Allocation {allocation_count} at {allocation_info['location']}")
                if 'allocation_size' in allocation_info:
                    print(f"  Size: {allocation_info['allocation_size']}")
        
        # Analyze allocation patterns
        print("\nMemory Leak Analysis:")
        print(f"Total allocations monitored: {allocation_count}")
        
        location_counts = {}
        for sample in memory_samples:
            location = sample["location"]
            location_counts[location] = location_counts.get(location, 0) + 1
        
        print("Allocation hotspots:")
        for location, count in sorted(location_counts.items(), key=lambda x: x[1], reverse=True):
            print(f"  {location}: {count} allocations")
        
        # Check for objects that should have been deallocated
        vars_response = await client.send_request("debug.getVariables", {
            "frameId": "global"  # Global scope
        })
        
        if vars_response["result"]["success"]:
            global_vars = vars_response["result"]["data"]
            suspicious_objects = [v for v in global_vars if 'cache' in v['name'].lower() or 'buffer' in v['name'].lower()]
            
            if suspicious_objects:
                print("Potentially leaked objects:")
                for obj in suspicious_objects:
                    print(f"  {obj['name']}: {obj['value']}")
    
    except Exception as e:
        print(f"Memory leak investigation error: {e}")
    finally:
        client.close()

async def get_memory_info(client):
    """Get memory information (simplified)"""
    # This would use custom memory inspection tools
    response = await client.send_request("debug.evaluate", {
        "expression": "GC.GetTotalMemory(false)",
        "context": "repl"
    })
    
    if response["result"]["success"]:
        return response["result"]["data"]["result"]
    return "Unknown"

asyncio.run(investigate_memory_leak())
```

## VS Code Extension Usage

### Extension Configuration

```json
// settings.json
{
  "debugops-mcp.serverPath": "C:\\DebugOps\\DebugOpsMCP.Host.dll",
  "debugops-mcp.serverTimeout": 15000,
  "debugops-mcp.showServerOutput": true,
  "debugops-mcp.logLevel": "Information",
  "debugops-mcp.autoDetectServer": true
}
```

### Launch Configuration

```json
// launch.json
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": "DebugOps MCP Attach",
      "type": "debugops-mcp",
      "request": "attach",
      "processId": "${command:pickProcess}",
      "configuration": {
        "stopOnEntry": false,
        "justMyCode": true
      }
    },
    {
      "name": "DebugOps MCP Launch",
      "type": "debugops-mcp", 
      "request": "launch",
      "program": "${workspaceFolder}/bin/Debug/MyApp.exe",
      "args": ["--debug"],
      "configuration": {
        "stopOnEntry": true,
        "workingDirectory": "${workspaceFolder}"
      }
    }
  ]
}
```

### Extension Commands

```typescript
// Using VS Code extension programmatically
import * as vscode from 'vscode';

// Start debugging session
await vscode.commands.executeCommand('debugops-mcp.attach', {
    processId: 1234
});

// Set breakpoint via command
await vscode.commands.executeCommand('debugops-mcp.setBreakpoint', {
    file: 'C:\\source\\app.cs',
    line: 42
});

// Get debug status
const status = await vscode.commands.executeCommand('debugops-mcp.getStatus');
console.log('Debug status:', status);
```

## Custom Tool Examples

### Performance Profiling Tool

```csharp
[McpMethod("debug.performance")]
public class DebugPerformanceTool : IDebugTool
{
    private readonly IDebugBridge _debugBridge;
    private readonly ILogger<DebugPerformanceTool> _logger;

    public DebugPerformanceTool(IDebugBridge debugBridge, ILogger<DebugPerformanceTool> logger)
    {
        _debugBridge = debugBridge;
        _logger = logger;
    }

    public bool CanHandle(string method)
    {
        return method.StartsWith("debug.performance.");
    }

    public async Task<McpResponse> HandleAsync(McpRequest request)
    {
        return request.Method switch
        {
            "debug.performance.start" => await StartProfiling(request),
            "debug.performance.stop" => await StopProfiling(request),
            "debug.performance.report" => await GetPerformanceReport(request),
            _ => McpResponse.Error("METHOD_NOT_FOUND", $"Unknown performance method: {request.Method}")
        };
    }

    private async Task<McpResponse> StartProfiling(McpRequest request)
    {
        try
        {
            var profilingRequest = new StartProfilingRequest
            {
                SamplingInterval = 1000, // 1ms
                ProfileMemory = true,
                ProfileCpu = true
            };

            var response = await _debugBridge.SendRequestAsync<StartProfilingRequest, ProfilingResponse>(
                profilingRequest);

            return McpResponse.Success(new
            {
                sessionId = response.SessionId,
                started = response.Started,
                message = "Performance profiling started"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start profiling");
            return McpResponse.Error("PROFILING_START_FAILED", ex.Message);
        }
    }

    private async Task<McpResponse> GetPerformanceReport(McpRequest request)
    {
        try
        {
            var reportRequest = new GetProfilingReportRequest();
            var report = await _debugBridge.SendRequestAsync<GetProfilingReportRequest, PerformanceReport>(
                reportRequest);

            return McpResponse.Success(new
            {
                cpuUsage = report.CpuUsage,
                memoryUsage = report.MemoryUsage,
                topMethods = report.TopMethods,
                hotspots = report.Hotspots
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get performance report");
            return McpResponse.Error("PROFILING_REPORT_FAILED", ex.Message);
        }
    }
}

// Contract classes
public class StartProfilingRequest
{
    public int SamplingInterval { get; set; }
    public bool ProfileMemory { get; set; }
    public bool ProfileCpu { get; set; }
}

public class PerformanceReport
{
    public double CpuUsage { get; set; }
    public long MemoryUsage { get; set; }
    public MethodInfo[] TopMethods { get; set; } = Array.Empty<MethodInfo>();
    public Hotspot[] Hotspots { get; set; } = Array.Empty<Hotspot>();
}

public class MethodInfo
{
    public string Name { get; set; } = string.Empty;
    public double CpuTime { get; set; }
    public int CallCount { get; set; }
}

public class Hotspot
{
    public string Location { get; set; } = string.Empty;
    public double Impact { get; set; }
}
```

### Usage with AI Assistant

```python
async def performance_analysis_with_ai():
    """Use AI to analyze performance issues"""
    client = DebugOpsMCPClient("DebugOpsMCP.Host.dll")
    
    try:
        # Attach to process
        await client.send_request("debug.attach", {"processId": 1234})
        
        # Start performance profiling
        profile_response = await client.send_request("debug.performance.start", {
            "samplingInterval": 500,
            "profileMemory": True,
            "profileCpu": True
        })
        
        print("Started profiling, waiting 30 seconds...")
        await asyncio.sleep(30)
        
        # Get performance report
        report_response = await client.send_request("debug.performance.report")
        
        if report_response["result"]["success"]:
            report = report_response["result"]["data"]
            
            # Create AI prompt with performance data
            ai_prompt = f"""
Analyze this performance profile data and identify issues:

CPU Usage: {report['cpuUsage']}%
Memory Usage: {report['memoryUsage']} bytes

Top Methods by CPU Time:
{json.dumps(report['topMethods'], indent=2)}

Performance Hotspots:
{json.dumps(report['hotspots'], indent=2)}

Please identify:
1. Performance bottlenecks
2. Memory inefficiencies  
3. Optimization opportunities
4. Specific code areas that need attention
"""
            
            # Send to AI for analysis (implement based on your AI service)
            analysis = await analyze_with_ai(ai_prompt)
            print(f"AI Performance Analysis:\n{analysis}")
            
        # Stop profiling
        await client.send_request("debug.performance.stop")
        
    except Exception as e:
        print(f"Performance analysis error: {e}")
    finally:
        client.close()
```

## Error Handling Patterns

### Robust Error Handling

```python
class RobustDebugClient:
    def __init__(self, server_path):
        self.client = DebugOpsMCPClient(server_path)
        self.max_retries = 3
        self.retry_delay = 1.0
    
    async def safe_debug_operation(self, method, params=None, retries=None):
        """Perform debug operation with comprehensive error handling"""
        retries = retries or self.max_retries
        
        for attempt in range(retries):
            try:
                response = await self.client.send_request(method, params)
                
                # Check for MCP-level errors
                if "error" in response:
                    error = response["error"]
                    
                    # Handle specific error codes
                    if error["code"] == "DEBUG_SESSION_NOT_FOUND":
                        print("Debug session expired, attempting to reattach...")
                        await self.reattach_session()
                        continue
                    elif error["code"] == "DEBUG_TIMEOUT":
                        print(f"Request timed out, retry {attempt + 1}/{retries}")
                        await asyncio.sleep(self.retry_delay * (attempt + 1))
                        continue
                    elif error["code"] == "BRIDGE_CONNECTION_FAILED":
                        print("Bridge connection lost, reconnecting...")
                        await self.reconnect_bridge()
                        continue
                    else:
                        # Non-recoverable error
                        raise DebugOperationError(error["code"], error["message"])
                
                # Success
                return response["result"]
                
            except asyncio.TimeoutError:
                print(f"Network timeout, attempt {attempt + 1}/{retries}")
                if attempt == retries - 1:
                    raise
                await asyncio.sleep(self.retry_delay)
            
            except ConnectionError:
                print(f"Connection error, attempt {attempt + 1}/{retries}")
                if attempt == retries - 1:
                    raise
                await self.reconnect_bridge()
                await asyncio.sleep(self.retry_delay)
        
        raise DebugOperationError("MAX_RETRIES_EXCEEDED", f"Failed after {retries} attempts")
    
    async def reattach_session(self):
        """Reattach to debug session"""
        try:
            # Store current process ID if available
            if hasattr(self, 'current_process_id'):
                await self.client.send_request("debug.attach", {
                    "processId": self.current_process_id
                })
        except Exception as e:
            print(f"Failed to reattach: {e}")
    
    async def reconnect_bridge(self):
        """Reconnect the debug bridge"""
        try:
            # Reinitialize client connection
            await self.client.reinitialize()
        except Exception as e:
            print(f"Failed to reconnect bridge: {e}")

class DebugOperationError(Exception):
    def __init__(self, code, message):
        self.code = code
        self.message = message
        super().__init__(f"{code}: {message}")

# Usage with error handling
async def robust_debugging_example():
    client = RobustDebugClient("DebugOpsMCP.Host.dll")
    
    try:
        # Safe attachment with automatic retry
        attach_result = await client.safe_debug_operation("debug.attach", {
            "processId": 1234
        })
        
        client.current_process_id = 1234
        
        # Safe breakpoint setting
        bp_result = await client.safe_debug_operation("debug.setBreakpoint", {
            "file": "C:\\source\\app.cs",
            "line": 42
        })
        
        # Safe execution with retry
        continue_result = await client.safe_debug_operation("debug.continue")
        
        # Safe variable inspection
        vars_result = await client.safe_debug_operation("debug.getVariables", {
            "frameId": "frame-1"
        })
        
        print("Debug operations completed successfully")
        
    except DebugOperationError as e:
        print(f"Debug operation failed: {e.code} - {e.message}")
    except Exception as e:
        print(f"Unexpected error: {e}")
    finally:
        await client.client.close()

asyncio.run(robust_debugging_example())
```

### Graceful Degradation

```python
class GracefulDebugClient:
    def __init__(self, server_path):
        self.client = DebugOpsMCPClient(server_path)
        self.fallback_mode = False
    
    async def debug_with_fallback(self, process_id):
        """Debug with graceful degradation to simpler operations"""
        
        try:
            # Try full debugging session
            await self.full_debug_session(process_id)
        except Exception as e:
            print(f"Full debug session failed: {e}")
            
            try:
                # Fall back to basic inspection
                await self.basic_inspection_session(process_id)
            except Exception as e2:
                print(f"Basic inspection failed: {e2}")
                
                try:
                    # Final fallback to health check only
                    await self.health_check_only()
                except Exception as e3:
                    print(f"Complete failure: {e3}")
                    self.fallback_mode = True
    
    async def full_debug_session(self, process_id):
        """Full debugging with all features"""
        await self.client.send_request("debug.attach", {"processId": process_id})
        await self.client.send_request("debug.setBreakpoint", {
            "file": "C:\\source\\app.cs", "line": 42
        })
        await self.client.send_request("debug.continue")
        
        # Advanced operations
        stack = await self.client.send_request("debug.getStackTrace", {"threadId": 12345})
        variables = await self.client.send_request("debug.getVariables", {"frameId": "frame-1"})
        
        print("Full debug session successful")
    
    async def basic_inspection_session(self, process_id):
        """Basic inspection without advanced features"""
        print("Falling back to basic inspection mode")
        
        await self.client.send_request("debug.attach", {"processId": process_id})
        
        # Only basic operations
        threads = await self.client.send_request("debug.getThreads")
        status = await self.client.send_request("debug.getStatus")
        
        print("Basic inspection completed")
    
    async def health_check_only(self):
        """Health check fallback"""
        print("Falling back to health check only")
        
        health = await self.client.send_request("health")
        if health["result"]["success"]:
            print("Server is responding")
        else:
            print("Server health check failed")
```

These examples demonstrate comprehensive usage patterns for the DebugOpsMCP framework, from basic workflows to advanced AI integration and robust error handling.