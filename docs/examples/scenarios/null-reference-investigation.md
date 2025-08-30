# Scenario 1: Null Reference Investigation

This scenario demonstrates how GHCP would use DebugOpsMCP to investigate a NullReferenceException in a typical .NET application.

## Problem Description

A developer reports that their OrderProcessor.CalculateTotal method is throwing a NullReferenceException intermittently. The error occurs when processing certain orders, but it's unclear why.

```csharp
public class OrderProcessor 
{
    public decimal CalculateTotal(Order order)
    {
        decimal total = 0;
        
        // NullReferenceException occurs on this line
        foreach (var item in order.Items)  // ← Exception here
        {
            total += item.Price * item.Quantity;
        }
        
        return total;
    }
}
```

## GHCP Debugging Workflow

### Step 1: Attach to Process

**Human Input:**
> "I'm getting a NullReferenceException in OrderProcessor.CalculateTotal. Can you help me debug this?"

**GHCP Response:**
> I'll help you debug that NullReferenceException. Let me attach to your application and investigate.

**MCP Commands Used:**
```typescript
// GHCP attaches to the running process
const attachResponse = await mcp.invoke("debug.attach", {
  processId: 1234,
  configuration: { justMyCode: true }
});

// Verify successful attachment
const status = await mcp.invoke("debug.getStatus");
// Result: { isDebugging: true, isPaused: false, sessionId: "session-123" }
```

### Step 2: Set Strategic Breakpoint

**GHCP Analysis:**
> Based on the error location, I'll set a breakpoint at the beginning of the CalculateTotal method to examine the state when the exception occurs.

**MCP Commands Used:**
```typescript
const breakpoint = await mcp.invoke("debug.setBreakpoint", {
  file: "C:\\MyApp\\OrderProcessor.cs",
  line: 5,  // Beginning of CalculateTotal method
  condition: null
});

// Verify breakpoint was set
console.log(`Breakpoint set: ${breakpoint.result.id} at ${breakpoint.result.file}:${breakpoint.result.line}`);
// Result: Breakpoint set: bp-123 at C:\MyApp\OrderProcessor.cs:5
```

### Step 3: Wait for Breakpoint Hit and Examine State

**GHCP Response:**
> Breakpoint is set. Please trigger the code path that causes the exception. Once we hit the breakpoint, I'll examine the method parameters.

*[User triggers the problematic order processing]*

When breakpoint hits:

**MCP Commands Used:**
```typescript
// Get current stack trace
const stack = await mcp.invoke("debug.getStackTrace");
const currentFrame = stack.result.frames[0];

// Examine method parameters and local variables
const variables = await mcp.invoke("debug.getVariables", {
  frameId: currentFrame.id
});

// Check the 'order' parameter specifically
const orderEvaluation = await mcp.invoke("debug.evaluate", {
  expression: "order",
  frameId: currentFrame.id
});

// Check if order.Items is null
const itemsEvaluation = await mcp.invoke("debug.evaluate", {
  expression: "order.Items",
  frameId: currentFrame.id
});
```

### Step 4: Analyze Findings

**GHCP Analysis Based on Results:**

```typescript
// Stack trace shows:
// Frame 0: OrderProcessor.CalculateTotal(Order order) at line 5
// Frame 1: OrderService.ProcessOrder(Order order) at line 28
// Frame 2: OrderController.SubmitOrder(int orderId) at line 15

// Variables show:
// - order: { OrderId: 123, CustomerId: 456, Items: null }
// - total: 0 (initialized but not used yet)

// Expression evaluations:
// order → "{ OrderId: 123, CustomerId: 456, Items: null }"
// order.Items → "null"
```

**GHCP Response:**
> I found the issue! The `order.Items` property is null instead of being an empty collection. This will cause a NullReferenceException when the foreach loop tries to iterate over it.

> Let me investigate further by examining how the Order object was created.

### Step 5: Investigate Root Cause

**MCP Commands Used:**
```typescript
// Step back in the call stack to see how the order was created
const callerVariables = await mcp.invoke("debug.getVariables", {
  frameId: stack.result.frames[1].id  // OrderService.ProcessOrder
});

// Check how the order was retrieved/constructed
const orderCreationEval = await mcp.invoke("debug.evaluate", {
  expression: "order.Items == null",
  frameId: stack.result.frames[1].id
});
```

### Step 6: Provide Solution

**GHCP Final Response:**
> ## Root Cause Analysis

> The NullReferenceException occurs because the `Order.Items` property is null rather than an empty collection. This happens when:

> 1. The Order object is created but the Items collection is not initialized
> 2. The database query or deserialization process doesn't populate the Items property
> 3. The Order constructor doesn't initialize the Items collection

> ## Recommended Fixes

> ### Option 1: Fix Order Constructor (Preferred)
> ```csharp
> public class Order 
> {
>     public Order()
>     {
>         Items = new List<OrderItem>(); // Initialize to empty collection
>     }
>     
>     public int OrderId { get; set; }
>     public int CustomerId { get; set; }
>     public List<OrderItem> Items { get; set; }
> }
> ```

> ### Option 2: Add Defensive Null Check
> ```csharp
> public decimal CalculateTotal(Order order)
> {
>     decimal total = 0;
>     
>     // Add null check
>     if (order?.Items != null)
>     {
>         foreach (var item in order.Items)
>         {
>             total += item.Price * item.Quantity;
>         }
>     }
>     
>     return total;
> }
> ```

> ### Option 3: Use Null-Coalescing
> ```csharp
> foreach (var item in order.Items ?? Enumerable.Empty<OrderItem>())
> {
>     total += item.Price * item.Quantity;
> }
> ```

> I recommend **Option 1** as it prevents the issue at the source and follows the principle that collections should never be null.

### Step 7: Clean Up

**MCP Commands Used:**
```typescript
// Remove the breakpoint
await mcp.invoke("debug.removeBreakpoint", {
  breakpointId: "bp-123"
});

// Continue execution
await mcp.invoke("debug.continue");

// Disconnect when done
await mcp.invoke("debug.disconnect");
```

## Key Learning Points

1. **Strategic Breakpoint Placement**: Set breakpoints at method entry points to examine parameter state
2. **Progressive Investigation**: Start with the error location, then work backwards through the call stack
3. **Multiple Evaluation Techniques**: Use both variable inspection and expression evaluation
4. **Root Cause vs. Symptom**: Don't just fix the exception; understand why the null condition exists
5. **Provide Multiple Solutions**: Offer different approaches with trade-offs explained

## Expected MCP Tool Usage

- `debug.attach()` - 1 call
- `debug.setBreakpoint()` - 1 call  
- `debug.getStackTrace()` - 1 call
- `debug.getVariables()` - 2 calls (current frame + caller frame)
- `debug.evaluate()` - 3-4 calls for various expressions
- `debug.removeBreakpoint()` - 1 call
- `debug.continue()` - 1 call
- `debug.disconnect()` - 1 call

**Total**: ~10-12 MCP tool invocations for a complete investigation

This demonstrates the power of programmatic debugging - GHCP can systematically investigate issues using the same techniques an experienced developer would use, but faster and more consistently.