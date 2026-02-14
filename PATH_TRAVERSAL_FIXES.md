# Path Traversal Security Fixes

## Overview
This document describes the security fixes implemented to prevent path traversal vulnerabilities in `System.IO.Path.Combine` usage throughout the SmallMind codebase.

## Issues Identified

### 1. InferenceEngine.cs - Line 107
**Issue**: Insecure file name extraction from user-supplied path
```csharp
// BEFORE (Vulnerable):
var fileName = System.IO.Path.GetFileNameWithoutExtension(ggufPath);
var smqPath = System.IO.Path.Combine(cacheDir, $"{fileName}.smq");
```

**Problem**: If `ggufPath` contains path traversal sequences like `../../evil`, `GetFileNameWithoutExtension` may not properly strip all path components, potentially allowing files to be written outside the intended cache directory.

**Fix**: Extract the actual filename first, then validate it
```csharp
// AFTER (Secure):
var fileName = System.IO.Path.GetFileName(ggufPath);
var fileNameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(fileName);
Guard.SafeFileName(fileNameWithoutExt, nameof(ggufPath));
var smqPath = System.IO.Path.Combine(cacheDir, $"{fileNameWithoutExt}.smq");
```

### 2. PretrainedRegistry.cs - Line 148
**Issue**: Unvalidated path combination allowing directory traversal
```csharp
// BEFORE (Vulnerable):
public string GetPackFullPath(string packPath)
{
    return System.IO.Path.Combine(BasePath, packPath);
}
```

**Problem**: If `packPath` contains `..` or is an absolute path, the result could escape the `BasePath` directory, allowing access to files outside the intended directory structure.

**Fix**: Validate that the combined path remains within the base directory
```csharp
// AFTER (Secure):
public string GetPackFullPath(string packPath)
{
    return Guard.PathWithinDirectory(BasePath, packPath, nameof(packPath));
}
```

## Security Mechanisms Added

### Guard.SafeFileName()
Validates that a string is a safe file name without path components:
- Checks for path separators (`/`, `\`, and platform-specific separators)
- Rejects relative path components (`.` and `..`)
- Validates against platform-specific invalid file name characters
- Throws `ValidationException` if validation fails

**Example Usage**:
```csharp
// Throws ValidationException - contains path separator
Guard.SafeFileName("../../evil.txt");

// Throws ValidationException - relative path component
Guard.SafeFileName("..");

// Returns "model.gguf" - valid filename
Guard.SafeFileName("model.gguf");
```

### Guard.PathWithinDirectory()
Ensures a combined path stays within a base directory:
- Resolves both paths to their full canonical form
- Verifies the combined path starts with the base path
- Prevents path traversal using `..` sequences
- Prevents absolute paths from escaping the base directory
- Throws `ValidationException` if the path would escape

**Example Usage**:
```csharp
var baseDir = "/app/data/models";

// Throws ValidationException - escapes base directory
Guard.PathWithinDirectory(baseDir, "../../../etc/passwd");

// Returns "/app/data/models/subfolder/file.txt" - stays within base
Guard.PathWithinDirectory(baseDir, "subfolder/file.txt");
```

## Test Coverage

Added comprehensive unit tests in `GuardClauseTests.cs`:

### SafeFileName Tests (13 tests)
- ✅ Validates valid file names
- ✅ Rejects paths with forward slashes
- ✅ Rejects paths with backslashes
- ✅ Rejects `..` traversal attempts
- ✅ Rejects relative path components (`.` and `..`)
- ✅ Rejects invalid file name characters
- ✅ Handles null inputs appropriately

### PathWithinDirectory Tests (7 tests)
- ✅ Accepts valid relative paths
- ✅ Rejects path traversal attempts (`../../../etc/passwd`)
- ✅ Rejects absolute paths that escape base directory
- ✅ Rejects multiple traversal sequences
- ✅ Handles null inputs appropriately
- ✅ Returns full canonical paths

**All 65 Guard-related tests pass successfully.**

## Security Impact

These fixes prevent:

1. **Path Traversal Attacks**: Malicious users cannot use `..` sequences or absolute paths to access files outside intended directories
2. **File System Access Control Bypass**: Ensures files can only be created/accessed within designated safe directories
3. **Information Disclosure**: Prevents reading sensitive files (e.g., `/etc/passwd`, Windows system files)
4. **Code Execution**: Prevents writing files to arbitrary locations that could be executed

## CodeQL Compliance

These changes address CodeQL security warnings related to:
- CWE-22: Improper Limitation of a Pathname to a Restricted Directory ('Path Traversal')
- Unvalidated user input in file path operations

## Backward Compatibility

These changes are **backward compatible** with one important exception:

- **Breaking Change**: Code that previously relied on path traversal behavior will now throw `ValidationException`
- **Impact**: This is intentional - such code would have been a security vulnerability
- **Migration**: Callers should ensure they only pass valid file names and relative paths within the expected directory structure

## References

- OWASP: [Path Traversal](https://owasp.org/www-community/attacks/Path_Traversal)
- CWE-22: [Improper Limitation of a Pathname to a Restricted Directory](https://cwe.mitre.org/data/definitions/22.html)
- Microsoft Security Advisory: [Path Traversal Vulnerabilities](https://docs.microsoft.com/en-us/security-updates/securityadvisories/)
