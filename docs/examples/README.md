# DebugOpsMCP Examples

This directory contains examples and scenarios demonstrating how to use DebugOpsMCP with GitHub Copilot Agent Mode.

## Quick Start

### Basic Debugging Session

Here's how GHCP would use DebugOpsMCP to debug a simple issue:

```
Human: I'm getting a NullReferenceException in my OrderProcessor.CalculateTotal method. Can you help me debug this?

GHCP (Agent Mode):
I'll help you debug the NullReferenceException. Let me attach to your application and investigate.

[Uses debug.attach() to connect to the running process]
[Uses debug.setBreakpoint() to pause at OrderProcessor.CalculateTotal]
[Uses debug.getStackTrace() to analyze the call stack]
[Uses debug.getVariables() to inspect local variables and parameters]
[Uses debug.evaluate() to test assumptions about null values]

Based on my investigation, the issue is that the `Order.Items` collection is null when `CalculateTotal` is called. Here's what I found:

- The breakpoint shows `order` parameter is not null
- However, `order.Items` is null instead of an empty collection
- This happens because the Order constructor doesn't initialize the Items collection

Recommended fix:
1. Initialize Items in the Order constructor: `Items = new List<OrderItem>();`
2. Or add a null check: `if (Items?.Any() == true) { ... }`
```

## Example Scenarios

### [Scenario 1: Null Reference Investigation](./scenarios/null-reference-investigation.md)
Step-by-step debugging of a NullReferenceException using breakpoints and variable inspection.

### [Scenario 2: Logic Error in Calculation](./scenarios/logic-error-calculation.md)
Finding incorrect calculations by stepping through code and evaluating expressions.

### [Scenario 3: Threading Issue Diagnosis](./scenarios/threading-issue-diagnosis.md)
Using thread inspection and stack traces to identify race conditions.

### [Scenario 4: Performance Investigation](./scenarios/performance-investigation.md)
Analyzing slow methods by examining call stacks and variable states.

## MCP Tool Reference

### Core Debugging Commands

```typescript
// Start debugging
await mcp.invoke("debug.attach", { processId: 1234 });
await mcp.invoke("debug.launch", { 
  program: "MyApp.exe", 
  args: ["--debug"],
  configuration: { justMyCode: true }
});

// Control execution
await mcp.invoke("debug.continue");
await mcp.invoke("debug.pause");
await mcp.invoke("debug.step", { stepType: "over" });
await mcp.invoke("debug.step", { stepType: "into" });
await mcp.invoke("debug.step", { stepType: "out" });

// Breakpoints
const breakpoint = await mcp.invoke("debug.setBreakpoint", {
  file: "OrderProcessor.cs",
  line: 25,
  condition: "order != null"
});

await mcp.invoke("debug.removeBreakpoint", { 
  breakpointId: breakpoint.result.id 
});

// Inspection
const stack = await mcp.invoke("debug.getStackTrace", { threadId: 1 });
const variables = await mcp.invoke("debug.getVariables", { 
  frameId: stack.result.frames[0].id 
});
const result = await mcp.invoke("debug.evaluate", {
  expression: "order.Items?.Count ?? 0",
  frameId: stack.result.frames[0].id
});
```

### Error Handling

All DebugOpsMCP tools return structured responses with success/error status:

```typescript
const response = await mcp.invoke("debug.setBreakpoint", {
  file: "NonExistent.cs",
  line: 10
});

if (!response.success) {
  console.error(`Breakpoint failed: ${response.error.message}`);
  // Handle specific error codes
  switch (response.error.code) {
    case "FILE_NOT_FOUND":
      // Suggest alternative files
      break;
    case "NO_DEBUG_SESSION":
      // Start debugging session first
      break;
  }
}
```

## Best Practices

### 1. Always Check Debug Status
```typescript
const status = await mcp.invoke("debug.getStatus");
if (!status.result.isDebugging) {
  throw new Error("No active debugging session. Use debug.attach() or debug.launch() first.");
}
```

### 2. Handle Breakpoint Verification
```typescript
const bp = await mcp.invoke("debug.setBreakpoint", { file: "app.cs", line: 10 });
if (!bp.result.verified) {
  console.warn("Breakpoint not verified - line may not contain executable code");
}
```

### 3. Use Conditional Breakpoints for Efficiency
```typescript
// Only break when specific condition is met
await mcp.invoke("debug.setBreakpoint", {
  file: "loop.cs",
  line: 15,
  condition: "i > 100",
  hitCondition: "> 5"  // Only after 5th hit
});
```

### 4. Inspect Variables Hierarchically
```typescript
// Get top-level variables first
const locals = await mcp.invoke("debug.getVariables", { frameId });

// Then drill down into complex objects
for (const variable of locals.result) {
  if (variable.variablesReference) {
    const childVars = await mcp.invoke("debug.getVariables", {
      scopeId: variable.variablesReference
    });
    console.log(`${variable.name} children:`, childVars.result);
  }
}
```

## Troubleshooting

### Common Issues

1. **"NO_DEBUG_SESSION" Error**
   - Ensure you've called `debug.attach()` or `debug.launch()` first
   - Check that the target process is still running

2. **Breakpoints Not Hitting**
   - Verify the file path is correct (use absolute paths)
   - Ensure the line contains executable code
   - Check that debug symbols (PDB files) are available

3. **Variable Evaluation Fails**
   - Make sure you're in a valid execution context (paused at breakpoint)
   - Use the correct frame ID from the stack trace
   - Some expressions may not be evaluable in optimized builds

### Debug Logging

Enable debug logging to troubleshoot MCP communication:

```json
{
  "Logging": {
    "LogLevel": {
      "DebugOpsMCP": "Debug"
    }
  }
}
```

## Contributing Examples

When adding new examples:

1. Use realistic scenarios that developers commonly encounter
2. Show both the GHCP interaction and the underlying MCP calls
3. Include error handling and edge cases
4. Provide sample code that reproduces the issue
5. Document expected outcomes and troubleshooting steps