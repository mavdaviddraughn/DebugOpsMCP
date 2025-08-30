# DebugOpsMCP

A VS Code extension that exposes debugging automation capabilities through the Model Context Protocol (MCP) to enable GitHub Copilot Agent Mode to perform sophisticated debugging operations.

## Problem Statement

Modern AI agents like GitHub Copilot need programmatic access to debugging capabilities to help developers diagnose issues, set breakpoints, inspect variables, and step through code execution. Currently, there's no standardized way for AI agents to interact with debuggers across different editors and platforms.

DebugOpsMCP bridges this gap by providing a structured MCP interface that translates high-level debugging commands from AI agents into Debug Adapter Protocol (DAP) operations in VS Code.

## Scope & Constraints

### Phase 1 (MVP) - Core Debugging
- **IN SCOPE**: Basic debugging operations (attach/launch, breakpoints, stepping, stack inspection, variable evaluation, thread management)
- **IN SCOPE**: VS Code extension with DAP integration
- **IN SCOPE**: Editor-agnostic core server architecture
- **OUT OF SCOPE**: WPF-specific visual tree/binding probes (deferred to Phase 2)
- **OUT OF SCOPE**: Hot Reload, Live Visual Tree, designer tooling
- **OUT OF SCOPE**: Visual Studio VSIX (planned for Phase 3)

### Technical Constraints
- Target: .NET 8 for core server
- Primary target processes: .NET Framework 4.8 x86 (but architecture supports others)
- Transport: MCP over stdio (JSON-RPC)
- Integration: Extension-mediated DAP communication (Mode A)

## Architecture

### High-Level Component Flow
```
┌─────────────────────┐
│ GHCP (Agent Mode)   │
└──────────┬──────────┘
           │ MCP (JSON-RPC over stdio)
           ▼
┌─────────────────────┐
│ VS Code Extension   │ (TypeScript - adapter only)
│ - Spawns core       │
│ - Proxies messages  │
└──────────┬──────────┘
           │ spawn & stdio
           ▼
┌─────────────────────┐
│ DebugOpsMCP Core    │ (C# .NET 8 - editor-agnostic)
│ ├─ McpHost          │ (transport, validation, routing)
│ ├─ DebugBridge      │ (DAP client abstraction)
│ ├─ Tools            │ (debug.* implementations)
│ └─ Contracts        │ (DTOs, errors)
└──────────┬──────────┘
           │ DAP JSON-RPC
           ▼
┌─────────────────────┐
│ VS Code DAP         │
└──────────┬──────────┘
           │ Platform-specific protocols
           ▼
┌─────────────────────┐
│ Target Process      │ (.NET Framework 4.8 x86, etc.)
└─────────────────────┘
```

### Repository Structure
```
/
├── README.md
├── .vscode/
│   ├── settings.json
│   ├── launch.json
│   └── tasks.json
├── core/                          # C# .NET 8 DebugOpsMCP Server
│   ├── DebugOpsMCP.sln
│   ├── src/
│   │   ├── DebugOpsMCP.Core/      # Main server implementation
│   │   ├── DebugOpsMCP.Contracts/ # Shared DTOs and interfaces
│   │   └── DebugOpsMCP.Host/      # Console host application
│   └── tests/
│       ├── DebugOpsMCP.Core.Tests/
│       └── DebugOpsMCP.Integration.Tests/
├── vscode-extension/              # TypeScript VS Code Extension
│   ├── package.json
│   ├── src/
│   │   ├── extension.ts
│   │   └── debugOpsMcpClient.ts
│   └── test/
└── docs/                          # Documentation and ADRs
    ├── architecture/
    │   ├── diagrams/
    │   └── adrs/                  # Architecture Decision Records
    ├── examples/
    └── contributing.md
```

## Design Decisions & Trade-offs

### ADR-001: Core Language & Runtime
**Decision**: .NET 8 for core server
**Alternatives Considered**: 
- .NET Framework 4.8: Better compatibility with target processes but outdated tooling
- Node.js: Consistent with VS Code ecosystem but less suitable for DAP client implementation
**Trade-offs**: 
- ✅ Modern C# features, better async/await, cross-platform potential
- ✅ Excellent JSON and networking libraries
- ❌ Additional runtime dependency for users
- ❌ Potential compatibility issues with very old target processes

### ADR-002: Transport Protocol
**Decision**: MCP over stdio (JSON-RPC)
**Alternatives Considered**:
- Named pipes: Better for Windows but platform-specific
- TCP sockets: More complex security model
**Trade-offs**:
- ✅ Simple, secure, cross-platform
- ✅ Standard MCP transport
- ❌ Slightly more complex than direct function calls
- ❌ Debugging the transport layer adds complexity

### ADR-003: DAP Integration Mode
**Decision**: Extension-mediated (Mode A) - VS Code extension proxies DAP communication
**Alternatives Considered**:
- Core-client (Mode B): Core server directly connects to DAP
**Trade-offs**:
- ✅ Leverages existing VS Code DAP infrastructure
- ✅ Simpler security model (no direct network access from core)
- ✅ Better integration with VS Code debugging UI
- ❌ Tighter coupling to VS Code
- ❌ More complex message routing

### ADR-004: Schema & Contracts
**Decision**: Hand-rolled DTOs with JSON serialization
**Alternatives Considered**:
- Code generation from OpenAPI/JSON Schema
- Protocol Buffers
**Trade-offs**:
- ✅ Full control over serialization
- ✅ Easy to debug and modify
- ✅ Good TypeScript interop
- ❌ Manual maintenance of schemas
- ❌ Potential for drift between client/server

## Security & Trust Model

### Trust Boundaries
1. **GHCP → VS Code Extension**: Trusted (same user session)
2. **VS Code Extension → Core Server**: Trusted (spawned child process)
3. **Core Server → VS Code DAP**: Trusted (local communication)
4. **VS Code DAP → Target Process**: Controlled by VS Code's debugging permissions

### Security Considerations
- Core server only accepts connections from parent VS Code extension
- No network listeners or external connections
- File system access limited to debugging artifacts (PDB files, source files)
- Process access limited to debugging permissions granted to VS Code

## MCP Tool Surface (Phase 1)

```typescript
// Core debugging lifecycle
debug.attach(processId, configuration?)
debug.launch(program, args?, configuration?)
debug.disconnect()
debug.terminate()

// Execution control  
debug.continue()
debug.pause()
debug.stepOver()
debug.stepInto()
debug.stepOut()

// Breakpoints
debug.setBreakpoint(file, line, condition?, hitCondition?)
debug.removeBreakpoint(breakpointId)
debug.listBreakpoints()

// State inspection
debug.getStackTrace(threadId?)
debug.getVariables(scopeId?, filter?)
debug.evaluate(expression, frameId?, context?)

// Thread management
debug.getThreads()
debug.selectThread(threadId)

// Session info
debug.getStatus()
debug.getCapabilities()
```

## MVP Acceptance Criteria

### Functional Requirements
- [ ] GHCP can attach to a running .NET process
- [ ] GHCP can set breakpoints and verify they're hit
- [ ] GHCP can inspect call stack when paused at breakpoint
- [ ] GHCP can evaluate simple expressions in current scope
- [ ] GHCP can inspect local variables and their values
- [ ] GHCP can step through code (over, into, out)
- [ ] GHCP can continue execution after pause
- [ ] All operations return structured responses with success/error status

### Non-Functional Requirements
- [ ] All MCP requests/responses logged with timestamps
- [ ] Error responses include actionable messages
- [ ] Tool calls complete within 5 seconds for simple operations
- [ ] Memory usage remains stable during typical debugging sessions

### Quality Gates
- [ ] CI enforces architectural boundaries (no cross-layer imports)
- [ ] Unit tests cover all MCP tool implementations
- [ ] Integration tests validate end-to-end debugging scenarios
- [ ] TypeScript compilation succeeds with strict mode
- [ ] C# code analysis passes with no warnings

## Roadmap

### Phase 1: Core Debugging (Current)
- ✅ Repository scaffold and documentation
- [ ] DebugOpsMCP core server with MCP host
- [ ] VS Code extension with stdio communication
- [ ] DAP bridge implementation (Mode A)
- [ ] Core debugging tools (attach, breakpoints, stepping, inspection)
- [ ] Unit and integration tests
- [ ] CI/CD pipeline with boundary enforcement

### Phase 2: WPF Runtime Probes
- [ ] `wpf.getVisualTree()` - WPF visual tree inspection
- [ ] `wpf.getBindings()` - Data binding analysis
- [ ] `wpf.getResources()` - Resource dictionary inspection
- [ ] WPF-specific debugging scenarios and examples

### Phase 3: Visual Studio VSIX
- [ ] Visual Studio extension project
- [ ] VSIX adapter reusing DebugOpsMCP core
- [ ] Visual Studio DAP integration
- [ ] Cross-IDE testing and documentation

### Phase 4: Advanced Features
- [ ] Multi-target debugging (multiple processes)
- [ ] Debugging configuration persistence
- [ ] Custom debugger expression evaluators
- [ ] Performance profiling integration

## Contributing

### Development Setup
1. Install .NET 8 SDK
2. Install Node.js 18+ and npm
3. Install VS Code with C# and TypeScript extensions
4. Clone repository and run `./setup.sh` (or `setup.bat` on Windows)

### Architecture Boundaries
- **Core server** must remain editor-agnostic (no VS Code-specific references)
- **Contracts** must be serializable and version-compatible
- **Extensions** are thin adapters only (no business logic)
- **Tests** must not depend on external processes or network

### Testing Strategy
- **Unit Tests**: Individual components in isolation
- **Integration Tests**: End-to-end MCP scenarios with mock DAP
- **System Tests**: Real debugging scenarios with sample applications
- **Boundary Tests**: Validate architectural constraints in CI

## Example Usage

```bash
# From GHCP Agent Mode
> I need to debug why the Order.Total property is returning null in the ProcessOrder method

# Behind the scenes, GHCP would:
# 1. Use debug.attach() to connect to the running application
# 2. Use debug.setBreakpoint() to pause at ProcessOrder method entry
# 3. Use debug.getStackTrace() and debug.getVariables() to inspect state
# 4. Use debug.evaluate() to test expressions and identify the issue
# 5. Provide analysis and suggested fixes to the developer
```

For detailed examples and debugging scenarios, see `/docs/examples/`.

## Assumptions & Limitations

### Current Assumptions
- VS Code is the primary development environment
- Target applications are .NET-based with PDB symbol files
- Users have appropriate debugging permissions
- Single debugging session per extension instance

### Known Limitations
- Phase 1 focuses on basic debugging; advanced scenarios require Phase 2+
- No cross-machine debugging support
- Limited to debugger adapters supported by VS Code
- Performance may vary with large call stacks or complex object graphs

## License

MIT - See LICENSE file for details.