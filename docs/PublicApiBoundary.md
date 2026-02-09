# SmallMind Public API Boundary Policy

## Overview

This document defines the **public API boundary policy** for the SmallMind project. The goal is to provide a clear, stable, and minimal public API surface while keeping implementation details internal. This improves developer experience, reduces API bloat, prevents accidental breaking changes, and eliminates ambiguous type name collisions.

## Project Categories

SmallMind projects are organized into four categories based on their role:

### 1. Public API Project: `SmallMind`

**Purpose**: The canonical, consumer-facing API project.

**Responsibilities**:
- Expose end-user configuration options (e.g., `SmallMindOptions`, `TextGenerationOptions`, `ChatClientOptions`)
- Provide top-level entry points (e.g., `ISmallMindEngine`, `IChatClient`, `ITextGenerationSession`)
- Define builder/factory patterns for initialization
- Host all user-facing exceptions and error codes

**Rules**:
- ✅ **Public** types are the norm - this is the consumer API
- ✅ End-user `*Options` classes belong here
- ✅ Public exceptions inherit from this project's exception hierarchy
- ❌ Implementation logic must remain minimal (delegation to implementation projects)

**Examples of appropriate types**:
- `SmallMindOptions`, `TextGenerationOptions`, `EmbeddingOptions`
- `ISmallMindEngine`, `ITextGenerationSession`, `IChatClient`
- `GenerationResult`, `TokenResult`, `Usage`, `FinishReason`
- `SmallMindException` and derived exception types

---

### 2. Contract Project: `SmallMind.Abstractions`

**Purpose**: Shared contracts, interfaces, DTOs, and enums used across multiple implementation projects.

**Responsibilities**:
- Define cross-project interfaces (e.g., `IModelHandle`, `IChatSession`, `IRagEngine`)
- Provide data transfer objects (DTOs) used internally (e.g., `GenerationRequest`, `ModelLoadRequest`)
- Define enums and value types shared across layers (e.g., `ChatRole`, `GenerationMode`, `TokenEventKind`)
- Host core exceptions that can be raised by multiple projects

**Rules**:
- ✅ **Public** types for contracts intended for cross-project use
- ✅ Interfaces, DTOs, enums, and value types
- ✅ Should remain **small and intentional** - not a dumping ground
- ❌ No implementation logic - abstractions only
- ❌ Not intended for direct consumer use (consumers use `SmallMind` API)

**Examples of appropriate types**:
- `ISmallMindEngine`, `IModelHandle`, `IChatSession`, `IRagEngine`, `IRagIndex`
- `GenerationRequest`, `ModelLoadRequest`, `SessionOptions`, `ModelInfo`
- `ChatRole`, `GenerationMode`, `TokenEventKind`
- Core exception types: `SmallMindException`, `ContextLimitExceededException`, `BudgetExceededException`

---

### 3. Implementation Projects (Internal by Default)

**Purpose**: Core ML infrastructure, runtime engines, specialized features - implementation details not exposed to consumers.

**Projects**:
- `SmallMind.Core` - Tensor operations, automatic differentiation, SIMD kernels
- `SmallMind.Runtime` - Text generation, inference engine, sampling, scheduling
- `SmallMind.Transformers` - Transformer architecture components
- `SmallMind.Tokenizers` - Tokenization implementations
- `SmallMind.Quantization` - Model quantization and GGUF/SMQ format support
- `SmallMind.Engine` - Chat session management, context policies, RAG integration
- `SmallMind.Rag` - Retrieval-Augmented Generation features
- `SmallMind.ModelRegistry` - Model discovery and caching

**Rules**:
- ❌ **Internal by default** - types should not be public unless explicitly justified
- ✅ Can reference `SmallMind.Abstractions` for contracts
- ✅ Can reference each other as needed for implementation
- ❌ Should NOT reference `SmallMind` (the public API project) to avoid circular dependencies

**Exceptions** (rare, must be documented):
- Types required by advanced scenarios (e.g., custom plugins, extensibility points)
- Performance-critical types exposed for zero-allocation scenarios
- Types needed for testing that cannot be accessed via internal visibility

**Process for making a type public**:
1. Document the reason in code comments
2. Add to the allowlist in the API boundary validation tool
3. Consider if it should be moved to `SmallMind.Abstractions` instead

---

### 4. Tooling Projects (Internal Only)

**Purpose**: Executable tools, benchmarks, and utilities - not packaged for NuGet distribution.

**Projects**:
- `SmallMind.Console` - Interactive CLI for model testing
- `SmallMind.Benchmarks` - Performance benchmarking suite
- `SmallMind.Perf` - Low-level performance profiling

**Rules**:
- ❌ **All types must be internal** except `Program.Main` (entry point)
- ❌ Not packaged (`IsPackable=false`)
- ❌ Not referenced by other projects (except for development/testing purposes)
- ✅ Can reference any project for their functionality

**Rationale**: These are standalone executables for developers, not libraries for consumers.

---

## Duplicate Type Name Resolution

When the same type name appears in multiple projects, follow this resolution strategy:

### Resolution Rules

1. **Contracts → `SmallMind.Abstractions`**
   - If the type is an interface, DTO, or enum used across multiple projects
   - Move to Abstractions and update all references

2. **End-User Options → `SmallMind`**
   - If the type is a user-facing configuration or option class
   - Move to SmallMind API project and update all references

3. **Implementation-Specific → Make Internal**
   - If the type is specific to one implementation project and not needed elsewhere
   - Make it `internal` and optionally rename to clarify scope (e.g., `RuntimeGenerationOptions`)

### Naming Conventions for Internal Types

When keeping internal types with similar names, use prefixes to clarify ownership:
- `RuntimeGenerationOptions` (in `SmallMind.Runtime`)
- `RagGenerationOptions` (in `SmallMind.Rag`)
- `EngineSessionOptions` (in `SmallMind.Engine`)

---

## API Leak Prevention

To prevent accidental API surface expansion, the following guardrails are in place:

### Automated Validation

A validation tool/test scans all projects and enforces:
- **Tooling projects**: No public types except entry points
- **Implementation projects**: Public types must be in the allowlist
- **Readable reporting**: Lists project + file + type for violations

### Allowlist

Implementation projects can have public types if they are explicitly allowlisted and documented. The allowlist is maintained in:
- Unit test: `tests/SmallMind.Tests/PublicApiValidationTests.cs` (if test project exists)
- Console tool: `tools/ApiValidator/Program.cs` (if standalone validator is used)

### Process for Adding to Allowlist

1. Add a code comment explaining why the type must be public
2. Update the allowlist with the fully qualified type name
3. Consider if the type should be moved to `SmallMind.Abstractions` instead

---

## Benefits of This Policy

### 1. Clear Developer Experience
- Consumers have a single entry point: `SmallMind` API
- No confusion about which namespace or type to use
- Reduced IntelliSense noise in IDEs

### 2. Stable Contracts
- `SmallMind.Abstractions` defines stable contracts
- Implementation changes don't leak into the public API
- Easier to version and maintain SemVer

### 3. Reduced API Bloat
- Minimal public surface area
- Fewer types to document and support
- Lower risk of accidental breaking changes

### 4. No Ambiguous Collisions
- Single canonical location for each concept
- No duplicate `GenerationOptions`, `FinishReason`, etc.
- Clear ownership and responsibility

---

## Enforcement

This policy is enforced through:
1. **Code reviews** - reviewers check for public types in implementation projects
2. **Automated validation** - guardrail tool/test fails CI if violations are detected
3. **Documentation** - this policy document and inline code comments

---

## Migration Guide

When this policy is implemented, some types will move or become internal. Breaking changes are documented with migration paths:

### Example Migration

**Before**:
```csharp
using SmallMind.Runtime;

var options = new GenerationOptions { MaxNewTokens = 100 };
```

**After**:
```csharp
using SmallMind.Abstractions;

var options = new GenerationOptions { MaxNewTokens = 100 };
```

Or for end-user scenarios:
```csharp
using SmallMind;

var options = new TextGenerationOptions { MaxOutputTokens = 100 };
```

A detailed migration guide with all breaking changes is provided in the final implementation report.

---

## Rationale

### Why Separate `SmallMind` and `SmallMind.Abstractions`?

- **`SmallMind`**: High-level, user-friendly API designed for consumers
  - Optimized for ease of use, not for extensibility
  - Can evolve independently from internal contracts

- **`SmallMind.Abstractions`**: Low-level contracts for internal use
  - Allows implementation projects to share types without coupling to the public API
  - Provides stability for internal architecture changes

This separation allows:
- Implementation projects to depend on Abstractions without circular dependencies
- Public API to aggregate and simplify abstractions for end users
- Internal contracts to evolve independently from the public API surface

---

## Version History

- **v1.0** (2026-02-09): Initial policy document
