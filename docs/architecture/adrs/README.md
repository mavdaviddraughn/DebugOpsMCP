# Architecture Decision Records

This directory contains Architecture Decision Records (ADRs) documenting key design decisions made during the development of DebugOpsMCP.

## ADR Index

- [ADR-001: Core Language & Runtime](./001-core-language-runtime.md)
- [ADR-002: Transport Protocol](./002-transport-protocol.md)
- [ADR-003: DAP Integration Mode](./003-dap-integration-mode.md)
- [ADR-004: Schema & Contracts](./004-schema-contracts.md)

## ADR Format

Each ADR follows this structure:

1. **Title** - Short descriptive title
2. **Status** - Proposed, Accepted, Deprecated, or Superseded
3. **Context** - What is the issue that we're seeing that is motivating this decision?
4. **Decision** - What is the change that we're proposing or doing?
5. **Consequences** - What becomes easier or more difficult to do because of this change?
6. **Alternatives Considered** - What other options were evaluated?

## Creating New ADRs

When making significant architectural decisions:

1. Create a new ADR file using the next sequential number
2. Follow the standard format
3. Get the ADR reviewed by the team
4. Update the index above