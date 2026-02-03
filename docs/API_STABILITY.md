# API Stability and Compatibility Policy

## Overview

SmallMind provides a **stable public API contract** through the `SmallMind.Public` namespace. This document defines what is stable, how versioning works, and what guarantees you can expect.

## What is Stable?

### Stable (Public Contract)

Everything in the `SmallMind.Public` namespace is considered **stable** and follows semantic versioning guarantees:

- **Interfaces**: `ISmallMindEngine`, `ITextGenerationSession`, `IEmbeddingSession`
- **Factory**: `SmallMindFactory.Create()`
- **DTOs**: `TextGenerationRequest`, `GenerationResult`, `TokenResult`, etc.
- **Options**: `SmallMindOptions`, `TextGenerationOptions`, `EmbeddingOptions`
- **Exceptions**: `SmallMindException` and all derived exception types
- **Capabilities**: `EngineCapabilities`
- **Diagnostics**: `ISmallMindDiagnosticsSink`, `SmallMindDiagnosticEvent`

**Guarantee**: Code written against `SmallMind.Public` will not break between minor versions (e.g., 1.0 → 1.1).

### Internal (Not Stable)

Everything **outside** `SmallMind.Public` is considered **internal** and may change without notice:

- `SmallMind.Core`
- `SmallMind.Transformers`
- `SmallMind.Runtime`
- `SmallMind.Abstractions` (used by SmallMind.Public internally)
- `SmallMind.Engine` (facade implementation)
- All other namespaces

**Warning**: Do not reference internal types in your production code. They exist for:
- Educational purposes (learning how LLMs work)
- Advanced customization (at your own risk)
- Internal implementation details

## Semantic Versioning

SmallMind follows [Semantic Versioning 2.0.0](https://semver.org/):

```
MAJOR.MINOR.PATCH (e.g., 1.2.3)
```

### Major Version (Breaking Changes)

A **major version** increment (e.g., 1.x → 2.0) indicates **breaking changes** to the public API:

- Removing a public interface, method, or property
- Changing method signatures (parameter types, return types)
- Changing behavior in a way that breaks existing code
- Renaming public types or members

**You must update your code** when upgrading major versions.

### Minor Version (New Features)

A **minor version** increment (e.g., 1.0 → 1.1) adds **new features** without breaking existing code:

- Adding new methods to interfaces (with default implementations or new interfaces)
- Adding new optional properties to request DTOs
- Adding new exception types
- Adding new capabilities
- Exposing new features (e.g., embeddings, batching)

**Your code will continue to work** without changes.

### Patch Version (Bug Fixes)

A **patch version** increment (e.g., 1.0.0 → 1.0.1) contains **bug fixes** and performance improvements:

- Fixing bugs in existing functionality
- Performance optimizations
- Documentation updates
- Security fixes

**Your code will continue to work** without changes.

## What Counts as a Breaking Change?

### Breaking Changes (Major Version Required)

- Removing `SmallMindFactory.Create()` or changing its signature
- Removing a method from `ISmallMindEngine`, `ITextGenerationSession`, or `IEmbeddingSession`
- Renaming `SmallMindOptions` properties
- Changing the type of a DTO property (e.g., `string → int`)
- Removing an enum value from `FinishReason` or `SmallMindErrorCode`
- Changing the inheritance hierarchy of exceptions
- Changing the behavior of existing methods in incompatible ways

### NOT Breaking Changes (Minor Version)

- Adding new methods to interfaces (if providing defaults)
- Adding new optional properties to request DTOs
- Adding new enum values
- Adding new exception types
- Adding new capabilities to `EngineCapabilities`
- Improving performance without changing behavior
- Making internal refactorings (e.g., changing how `SmallMind.Engine` works internally)

## Deprecation Policy

When we need to introduce a breaking change, we follow this process:

1. **Mark as Obsolete**: Add `[Obsolete]` attribute with migration guidance
2. **Document**: Update release notes and migration guide
3. **Wait**: Keep obsolete API for at least one minor version
4. **Remove**: Remove in next major version

Example:
```csharp
[Obsolete("Use CreateTextGenerationSession() instead. Will be removed in v2.0.")]
public ITextGenerationSession CreateSession(...) { ... }
```

## Upgrade Guidance

### Minor Version Upgrades (Safe)

```bash
# Upgrade from 1.0 to 1.1 (safe, no code changes needed)
dotnet add package SmallMind.Public --version 1.1.0
```

Your code will continue to work. Review release notes for new features.

### Major Version Upgrades (Review Required)

```bash
# Upgrade from 1.x to 2.0 (review breaking changes first)
dotnet add package SmallMind.Public --version 2.0.0
```

**Before upgrading:**
1. Read the migration guide in `CHANGELOG.md`
2. Review breaking changes
3. Update your code as needed
4. Test thoroughly

## Stability Guarantees

### What We Guarantee

✅ **Binary Compatibility**: Your compiled assemblies will work with newer patch/minor versions without recompilation

✅ **Source Compatibility**: Your source code will compile without changes when upgrading patch/minor versions

✅ **Behavioral Compatibility**: Existing functionality will behave the same way (bugs excluded)

### What We Don't Guarantee

❌ **Internal Implementation**: How things work internally may change

❌ **Performance Characteristics**: We may optimize (improve) performance without notice

❌ **Exact Error Messages**: Error message text may improve for clarity

❌ **Diagnostics Events**: Diagnostic event details may change (use event types, not messages)

## Version History

| Version | Release Date | Type | Notable Changes |
|---------|--------------|------|-----------------|
| 1.0.0   | TBD          | Initial | Stable public API introduced |

## Support Policy

- **Current Version**: Fully supported (bug fixes + new features)
- **Previous Minor Version**: Security fixes only
- **Older Versions**: No longer supported

**Recommendation**: Stay on the latest minor version within your major version.

## Questions?

If you're unsure whether an API is stable:

1. **Check the namespace**: If it's in `SmallMind.Public`, it's stable
2. **Check the documentation**: Stable APIs have complete XML docs
3. **Ask**: Open an issue on GitHub

## References

- [Semantic Versioning 2.0.0](https://semver.org/)
- [.NET API Breaking Changes](https://learn.microsoft.com/en-us/dotnet/core/compatibility/)
