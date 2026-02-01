# Versioning and Breaking Changes Policy

## Semantic Versioning

SmallMind follows [Semantic Versioning 2.0.0](https://semver.org/). Version numbers are formatted as `MAJOR.MINOR.PATCH`:

- **MAJOR**: Incremented for incompatible API changes
- **MINOR**: Incremented for backward-compatible functionality additions
- **PATCH**: Incremented for backward-compatible bug fixes

### Pre-release Versions

Pre-release versions may be tagged with suffixes like `-alpha`, `-beta`, `-rc.1`, etc. These versions make no stability guarantees and may contain breaking changes between pre-release versions.

Example: `0.2.0-beta.1` → `0.2.0-beta.2` may contain breaking changes.

## What Constitutes a Breaking Change?

Breaking changes include, but are not limited to:

### Public API Changes

- **Removing** public types, methods, properties, or fields
- **Renaming** public members
- **Changing method signatures**: parameter types, counts, or order
- **Changing return types** of public methods
- **Making types or members less accessible** (e.g., `public` → `internal`)
- **Removing or renaming** public constants or enums
- **Changing exception types** thrown by public methods (unless adding more specific exceptions in a hierarchy)

### Behavioral Changes

- **Changing semantics** of existing methods in ways that could break existing code
- **Changing default values** of optional parameters
- **Changes to serialization format** for checkpoints or configuration (requires migration)
- **Performance regressions** exceeding 50% on critical paths (e.g., matrix operations)

### Dependency Changes

- **Requiring newer .NET runtime** version
- **Adding required dependencies** (not optional)

## What is NOT a Breaking Change?

The following changes are considered non-breaking:

- **Adding new public types, methods, or properties**
- **Adding optional parameters** with default values
- **Marking members as obsolete** (with migration guidance)
- **Bug fixes** that change behavior to match documented intent
- **Performance improvements**
- **Internal implementation** changes that don't affect public API
- **Adding interfaces** to existing types (in most cases)
- **Documentation improvements**

## Deprecation Policy

### Deprecation Process

1. **Announce**: Mark the API with `[Obsolete]` attribute with a clear message indicating:
   - What is deprecated
   - Why it's deprecated
   - What to use instead
   - When it will be removed (minimum one MAJOR version)

   Example:
   ```csharp
   [Obsolete("Use NewMethod instead. This method will be removed in v2.0.0")]
   public void OldMethod() { ... }
   ```

2. **Deprecation Window**: Deprecated APIs must remain functional for at least:
   - **One MAJOR version** after deprecation announcement
   - **Minimum 6 months** from deprecation to removal

3. **Documentation**: Deprecated APIs must have:
   - Clear migration guide in XML documentation
   - Entry in CHANGELOG under "Deprecated" section
   - Migration example in release notes

4. **Removal**: After the deprecation window, removal is allowed in the next MAJOR version.

### Example Timeline

- v1.5.0: Feature X deprecated, `[Obsolete]` added, migration guide published
- v1.6.0-v1.9.0: Feature X still works with obsolete warning
- v2.0.0: Feature X removed

## Breaking Change Migration Guide Requirements

Every breaking change in a MAJOR version release must include:

1. **Clear description** of what changed and why
2. **Before/After code examples** showing old and new usage
3. **Step-by-step migration instructions**
4. **Automated migration tools** when feasible (e.g., Roslyn analyzers)
5. **Estimated migration effort** (e.g., "5-10 minutes per project")

### Migration Guide Template

```markdown
## Breaking Change: [Brief Description]

**Affected API**: `NamespaceName.ClassName.MethodName`

**Reason**: [Why the change was necessary]

**Migration**:

Before (v1.x):
```csharp
// Old code example
var result = oldApi.DoSomething(param1, param2);
```

After (v2.x):
```csharp
// New code example
var result = newApi.DoSomething(param1); // param2 is now optional
```

**Effort**: Low (5 minutes per usage)
```

## Version Support Policy

- **Current MAJOR version**: Receives new features, bug fixes, and security updates
- **Previous MAJOR version**: Receives critical bug fixes and security updates for 12 months after new MAJOR release
- **Older versions**: No support (best-effort community support only)

Example:
- v2.0.0 released → v1.x supported for 12 months
- v2.0.0 + 12 months → v1.x reaches end-of-life

## Communication Channels

Breaking changes will be communicated through:

1. **CHANGELOG.md**: All changes documented under "Changed" or "Removed" sections
2. **GitHub Releases**: Detailed release notes with migration guides
3. **README.md**: Updated examples and quickstart guides
4. **Obsolete Warnings**: Compile-time warnings in deprecated APIs
5. **GitHub Discussions**: Community Q&A for migration assistance

## Exceptions and Overrides

In rare cases, breaking changes may be introduced in MINOR versions if:

1. **Security vulnerability** requires immediate breaking fix
2. **Data corruption bug** that could cause silent data loss
3. **Critical production issue** affecting core functionality

Such exceptions will be:
- Clearly marked in release notes as "EMERGENCY BREAKING CHANGE"
- Documented with detailed migration guide
- Communicated through all channels immediately
- Followed by MAJOR version increment as soon as feasible

## Feedback and Amendments

This policy may be updated as the project evolves. Proposed changes to this policy should:

1. Be discussed in GitHub Discussions or Issues
2. Allow for community feedback (minimum 2 weeks)
3. Be approved by project maintainers
4. Be documented in CHANGELOG.md under "Changed"

---

**Last Updated**: 2026-01-31  
**Policy Version**: 1.0.0
