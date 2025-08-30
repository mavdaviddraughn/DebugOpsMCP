# Changelog

All notable changes to DebugOpsMCP will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Initial DebugOpsMCP project structure and architecture
- Comprehensive README.md with problem statement, architecture, and roadmap
- .NET 8 core server implementation with MCP host
- VS Code extension scaffolding with TypeScript
- Complete MCP tool surface for Phase 1 debugging operations
- Debug lifecycle tools (attach, launch, disconnect, terminate)
- Debug execution control (continue, pause, step over/into/out)
- Breakpoint management (set, remove, list) with verification
- Runtime inspection (stack trace, variables, expression evaluation)  
- Thread management (list, select)
- Debug status and capability reporting
- Comprehensive unit test suite with xUnit and Moq
- Integration tests for end-to-end MCP communication
- Architecture Decision Records (ADRs) documenting design choices
- Detailed debugging scenario examples and documentation
- CI/CD pipeline with architectural boundary enforcement
- Security scanning and vulnerability detection
- Dependency injection with proper service registration
- Structured error handling with actionable error codes
- Comprehensive logging throughout all components

### Architecture
- Editor-agnostic core server design for future Visual Studio VSIX support
- Extension-mediated DAP integration (Mode A) for VS Code
- Strict architectural boundaries with CI enforcement
- Clean separation of concerns with contracts, core, and host layers
- Dependency injection container with tool registration

### Documentation
- Complete project setup and contribution guidelines
- Example debugging scenarios with step-by-step MCP usage
- API reference for all debug tools
- Best practices for debugging automation
- Troubleshooting guide for common issues

## [0.1.0] - TBD

### Added
- Initial release with Phase 1 MVP functionality
- Basic debugging operations through MCP
- VS Code extension for MCP server management

---

## Release Notes Template

### Version X.Y.Z - YYYY-MM-DD

#### Added
- New features and capabilities

#### Changed  
- Changes to existing functionality

#### Deprecated
- Features that will be removed in future versions

#### Removed
- Features that were removed in this version

#### Fixed
- Bug fixes and corrections

#### Security
- Security-related changes and fixes