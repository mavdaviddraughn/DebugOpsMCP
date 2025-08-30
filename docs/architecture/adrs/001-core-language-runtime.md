# ADR-001: Core Language & Runtime

## Status
Accepted

## Context
The DebugOpsMCP server needs to be implemented in a programming language that supports:
- Cross-platform execution
- Strong JSON handling capabilities
- Excellent async/await patterns for handling concurrent debugging sessions
- Good integration with Debug Adapter Protocol libraries
- Editor-agnostic design for future Visual Studio VSIX support

We considered three main options:

## Decision
Use **.NET 8** for the core DebugOpsMCP server implementation.

## Consequences

### Positive
- **Modern C# features**: Records, pattern matching, nullable reference types improve code quality
- **Excellent JSON support**: System.Text.Json provides high-performance serialization
- **Strong async/await**: Native support for asynchronous operations critical for debugging
- **Cross-platform**: Runs on Windows, macOS, and Linux
- **Rich ecosystem**: Microsoft.Extensions.* libraries provide dependency injection, logging, configuration
- **Future VSIX support**: .NET can be embedded in Visual Studio extensions

### Negative
- **Runtime dependency**: Users must have .NET 8 runtime installed
- **Learning curve**: Team members unfamiliar with modern C# may need training
- **Compatibility concerns**: Some very old target applications might have issues

## Alternatives Considered

### .NET Framework 4.8
- **Pros**: Better compatibility with legacy Windows applications, no additional runtime
- **Cons**: Windows-only, outdated tooling, limited modern language features
- **Verdict**: Rejected due to cross-platform requirements and technical debt

### Node.js/TypeScript
- **Pros**: Consistent with VS Code extension ecosystem, good JSON handling
- **Cons**: Less mature debugging protocol libraries, different concurrency model
- **Verdict**: Rejected due to complexity of implementing DAP client features

### Rust
- **Pros**: Excellent performance, memory safety, growing ecosystem
- **Cons**: Steeper learning curve, less mature JSON/debugging ecosystem
- **Verdict**: Rejected due to team expertise and development speed requirements

## Implementation Notes
- Target .NET 8 LTS for stability
- Use Microsoft.Extensions.Hosting for structured service management
- Leverage System.Text.Json for all serialization
- Design core abstractions to be runtime-agnostic for future portability