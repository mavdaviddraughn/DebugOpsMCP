# Contributing to DebugOpsMCP

Thank you for your interest in contributing to DebugOpsMCP! This document outlines the development process, coding standards, and architectural boundaries that help maintain the project's quality and vision.

## Development Setup

### Prerequisites
- .NET 8 SDK
- Node.js 18+ with npm
- VS Code with the C# extension
- Git

### Getting Started
1. Clone the repository
2. Open the root folder in VS Code
3. Run the build task: `Ctrl+Shift+P` → "Tasks: Run Task" → "build-core"
4. Run tests: `Ctrl+Shift+P` → "Tasks: Run Task" → "test-core"

### Project Structure
```
/
├── core/                    # C# .NET 8 server implementation
│   ├── src/
│   │   ├── Contracts/      # Shared DTOs and interfaces
│   │   ├── Core/           # Business logic and MCP host
│   │   └── Host/           # Console application entry point
│   └── tests/              # Unit and integration tests
├── vscode-extension/       # TypeScript VS Code extension
└── docs/                   # Documentation and ADRs
```

## Architectural Boundaries

DebugOpsMCP enforces strict architectural boundaries to maintain modularity and enable future extensibility:

### Layer Dependencies (Enforced by CI)
```
┌─────────────────┐
│ VS Code Ext     │ (TypeScript - thin adapter only)
└─────────┬───────┘
          │ spawns & stdio
┌─────────▼───────┐
│ Host            │ (C# - console app entry point)
└─────────┬───────┘
          │ uses
┌─────────▼───────┐
│ Core            │ (C# - MCP host & debug bridge)
└─────────┬───────┘
          │ uses
┌─────────▼───────┐
│ Contracts       │ (C# - DTOs and interfaces only)
└─────────────────┘
```

### Boundary Rules (Enforced by Linting/CI)

1. **Contracts** → No dependencies (pure DTOs)
2. **Core** → Only depends on Contracts + Microsoft.Extensions.*
3. **Host** → Depends on Core + Contracts + hosting libraries
4. **VS Code Extension** → Editor-specific, no C# dependencies

### Prohibited Cross-Layer Access
- ❌ Contracts cannot reference Core, Host, or VS Code extension
- ❌ Core cannot reference Host or VS Code extension  
- ❌ VS Code extension cannot directly reference C# projects
- ❌ No circular dependencies between any layers

## Coding Standards

### C# Guidelines
- Follow Microsoft's [C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/inside-a-program/coding-conventions)
- Use nullable reference types (`#nullable enable`)
- Prefer `record` types for immutable data transfer objects
- Use `async/await` for all I/O operations
- Include XML documentation for public APIs

### Example C# Code Style
```csharp
namespace DebugOpsMCP.Core.Tools;

/// <summary>
/// Handles debug breakpoint operations
/// </summary>
public class BreakpointTool : IDebugTool
{
    private readonly ILogger<BreakpointTool> _logger;
    private readonly IDebugBridge _debugBridge;

    public BreakpointTool(
        ILogger<BreakpointTool> logger,
        IDebugBridge debugBridge)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _debugBridge = debugBridge ?? throw new ArgumentNullException(nameof(debugBridge));
    }

    public async Task<McpResponse> HandleAsync(McpRequest request)
    {
        // Implementation
    }
}
```

### TypeScript Guidelines
- Use strict TypeScript configuration
- Prefer `interface` over `type` for object shapes
- Use meaningful variable names
- Handle all error cases explicitly

### Example TypeScript Code Style
```typescript
export interface DebugSession {
    readonly sessionId: string;
    readonly processId: number;
    readonly status: 'starting' | 'running' | 'paused' | 'terminated';
}

export class DebugSessionManager {
    private readonly sessions = new Map<string, DebugSession>();

    public async startSession(config: LaunchConfig): Promise<DebugSession> {
        // Implementation with proper error handling
    }
}
```

## Testing Requirements

### Unit Tests (Required for all PRs)
- Minimum 80% code coverage for new code
- Test both happy path and error scenarios
- Use descriptive test names: `ProcessRequestAsync_InvalidJson_ReturnsJsonParseError`
- Mock external dependencies

### Integration Tests
- Test complete MCP request/response cycles
- Use real server process but mock external debugger connections
- Verify JSON serialization/deserialization works correctly

### Testing Tools
- **xUnit** for C# unit tests
- **Moq** for mocking in C# tests
- **VS Code Test API** for extension integration tests

## Pull Request Process

### Before Submitting
1. Run all tests: `dotnet test`
2. Check code formatting: Extension should auto-format on save
3. Verify architectural boundaries: CI will check this
4. Update documentation if adding new features
5. Add/update tests for new functionality

### PR Requirements
- [ ] All tests pass
- [ ] Code follows project style guidelines
- [ ] Architectural boundaries respected
- [ ] Documentation updated (if needed)
- [ ] ADR created for significant design decisions

### Review Criteria
- **Functionality**: Does it work as intended?
- **Architecture**: Does it respect layer boundaries?
- **Testing**: Adequate test coverage and quality?
- **Documentation**: Clear and up-to-date?
- **Security**: No security vulnerabilities introduced?

## Adding New Debug Tools

When implementing new MCP debug tools:

### 1. Add Contracts (Phase 1)
```csharp
// In DebugOpsMCP.Contracts/Debug/
public class DebugNewFeatureRequest : McpRequest
{
    [JsonPropertyName("parameter")]
    public required string Parameter { get; set; }

    public DebugNewFeatureRequest()
    {
        Method = "debug.newFeature";
    }
}

public class DebugNewFeatureResponse : McpResponse<NewFeatureResult>
{
}
```

### 2. Update McpHost Routing (Phase 2)
```csharp
// In McpHost.RouteRequestAsync()
"debug.newFeature" => await ProcessDebugRequestAsync<DebugNewFeatureRequest>(requestJson),
```

### 3. Implement Tool Logic (Phase 3)
```csharp
// Create new tool class in DebugOpsMCP.Core/Tools/
public class NewFeatureTool : IDebugTool
{
    // Implementation
}
```

### 4. Add Tests (Phase 4)
- Unit tests for the tool logic
- Integration tests for the complete request/response cycle
- Error handling tests

### 5. Update Documentation (Phase 5)
- Add to MCP tool surface in README.md
- Create usage examples in /docs/examples/
- Update any relevant ADRs

## Security Considerations

### Trust Model
- Only accept connections from trusted VS Code extension
- Validate all input parameters
- Limit file system access to debugging artifacts
- No network connections beyond local debug adapter

### Code Review Focus Areas
- Input validation and sanitization
- Proper error handling without information leakage
- Resource management (dispose patterns)
- Thread safety in concurrent scenarios

## Release Process

### Versioning Strategy
- Follow [Semantic Versioning](https://semver.org/)
- MAJOR: Breaking changes to MCP API
- MINOR: New debug tools or capabilities
- PATCH: Bug fixes and performance improvements

### Release Checklist
1. All tests pass in CI
2. Documentation is up-to-date
3. CHANGELOG.md updated with notable changes
4. Version numbers bumped consistently
5. Integration tests pass against sample applications

## Getting Help

- **Questions**: Open a GitHub Discussion
- **Bugs**: Create a GitHub Issue with reproduction steps
- **Feature Requests**: Open a GitHub Issue with use case description
- **Architecture Discussions**: Propose an ADR

## Recognition

Contributors will be recognized in:
- CONTRIBUTORS.md file
- Release notes for significant contributions
- GitHub contributor graphs and stats

Thank you for helping make DebugOpsMCP better!