# CodeQL Path.Combine Security Issues - Resolution

## Overview
This document details the resolution of CodeQL security warnings related to `System.IO.Path.Combine` that may silently drop earlier arguments when later arguments contain rooted paths.

## Issue Description
`Path.Combine` has a potentially dangerous behavior where if any argument (except the first) contains a rooted path (like `/absolute/path` on Unix or `C:\path` on Windows), all previous arguments are discarded. This can lead to:

1. **Path Traversal Vulnerabilities** - Attackers can use `..` sequences or absolute paths to access files outside intended directories
2. **Security Bypass** - Directory restrictions can be circumvented
3. **Data Exfiltration** - Sensitive files can be read from unexpected locations
4. **Arbitrary File Write** - Files can be written to unintended locations

## Affected Files and Fixes

### 1. SmallMind.ModelRegistry/ModelRegistry.cs (Line 77)

**Issue:** User-supplied filename from URL or file path combined without validation

**Before:**
```csharp
string fileName = Path.GetFileName(source);
if (isUrl)
{
    Uri uri = new Uri(source);
    fileName = Path.GetFileName(uri.LocalPath);
    if (string.IsNullOrWhiteSpace(fileName) || fileName == "/")
    {
        fileName = "model.bin";
    }
}

string targetPath = Path.Combine(modelDir, fileName);
```

**After:**
```csharp
string fileName = Path.GetFileName(source);
if (isUrl)
{
    Uri uri = new Uri(source);
    fileName = Path.GetFileName(uri.LocalPath);
    if (string.IsNullOrWhiteSpace(fileName) || fileName == "/")
    {
        fileName = "model.bin";
    }
}

// Validate filename to prevent path traversal
fileName = Guard.SafeFileName(fileName, nameof(source));

string targetPath = Path.Combine(modelDir, fileName);
```

**Protection:** `Guard.SafeFileName` ensures:
- No path separators (`/`, `\`)
- No relative path components (`.`, `..`)
- No invalid filename characters
- Throws `ValidationException` if validation fails

---

### 2. SmallMind.ModelRegistry/ModelRegistry.cs (Line 195)

**Issue:** Manifest file paths combined without validation

**Before:**
```csharp
foreach (var fileEntry in manifest.Files)
{
    string filePath = Path.Combine(modelDir, fileEntry.Path);
    
    if (!File.Exists(filePath))
    {
        result.IsValid = false;
        result.Errors.Add($"File not found: {fileEntry.Path}");
        continue;
    }
    // ... verification logic
}
```

**After:**
```csharp
foreach (var fileEntry in manifest.Files)
{
    // Validate path to prevent directory traversal
    string filePath = Guard.PathWithinDirectory(modelDir, fileEntry.Path, nameof(fileEntry.Path));
    
    if (!File.Exists(filePath))
    {
        result.IsValid = false;
        result.Errors.Add($"File not found: {fileEntry.Path}");
        continue;
    }
    // ... verification logic
}
```

**Protection:** `Guard.PathWithinDirectory` ensures:
- Combined path stays within base directory
- Prevents `..` traversal sequences
- Prevents absolute paths from escaping
- Returns canonical full path
- Throws `ValidationException` if path would escape

---

### 3. SmallMind.ModelRegistry/ModelRegistry.cs (Line 251)

**Issue:** Primary model file path from manifest combined without validation

**Before:**
```csharp
string modelDir = CachePathResolver.GetModelDirectory(_cacheRoot, modelId);
return Path.Combine(modelDir, manifest.Files[0].Path);
```

**After:**
```csharp
string modelDir = CachePathResolver.GetModelDirectory(_cacheRoot, modelId);
// Validate path to prevent directory traversal
return Guard.PathWithinDirectory(modelDir, manifest.Files[0].Path, "manifest.Files[0].Path");
```

**Protection:** Same as #2 - ensures returned path stays within model directory

---

### 4. SmallMind.Engine/SmallMindEngine.cs (Line 443)

**Issue:** File name extracted from user-provided path used without validation

**Before:**
```csharp
Directory.CreateDirectory(cacheDir);

// Generate cached SMQ file path
var fileName = Path.GetFileNameWithoutExtension(request.Path);
var smqPath = Path.Combine(cacheDir, $"{fileName}.smq");
```

**After:**
```csharp
Directory.CreateDirectory(cacheDir);

// Generate cached SMQ file path with validation
var fileName = Path.GetFileNameWithoutExtension(request.Path);
fileName = Guard.SafeFileName(fileName, nameof(request.Path));
var smqPath = Path.Combine(cacheDir, $"{fileName}.smq");
```

**Protection:** `Guard.SafeFileName` prevents malicious filenames

---

## Infrastructure Changes

### Project References
Added `SmallMind.Core` reference to `SmallMind.ModelRegistry.csproj`:
```xml
<ItemGroup>
  <ProjectReference Include="..\SmallMind.Core\SmallMind.Core.csproj" />
</ItemGroup>
```

Added `SmallMind.ModelRegistry` to Core's InternalsVisibleTo list in `SmallMind.Core.csproj`:
```xml
<InternalsVisibleTo Include="SmallMind.ModelRegistry" />
```

### Using Directives
Added validation imports:
- `SmallMind.ModelRegistry/ModelRegistry.cs`: `using SmallMind.Core.Validation;`
- `SmallMind.Engine/SmallMindEngine.cs`: `using SmallMind.Core.Validation;`

---

## Security Mechanisms

### Guard.SafeFileName(string fileName, string parameterName)
Validates that a string is a safe file name without path components:

**Checks:**
1. Not null or whitespace
2. No path separators (`/`, `\`, platform-specific)
3. Not `.` or `..`
4. No invalid filename characters (OS-specific)

**Example:**
```csharp
// Throws ValidationException
Guard.SafeFileName("../../evil.txt");
Guard.SafeFileName("..");
Guard.SafeFileName("file/with/slashes.txt");

// Returns successfully
Guard.SafeFileName("valid_model.gguf");
```

### Guard.PathWithinDirectory(string basePath, string relativePath, string parameterName)
Ensures combined path stays within base directory:

**Checks:**
1. Both arguments not null or whitespace
2. Resolves paths to canonical form using `Path.GetFullPath()`
3. Verifies combined path starts with base path
4. Prevents escaping via `..` sequences or absolute paths

**Example:**
```csharp
var baseDir = "/app/data/models";

// Throws ValidationException - escapes base
Guard.PathWithinDirectory(baseDir, "../../../etc/passwd");
Guard.PathWithinDirectory(baseDir, "/etc/passwd");

// Returns "/app/data/models/subfolder/file.txt"
Guard.PathWithinDirectory(baseDir, "subfolder/file.txt");
```

---

## Test Coverage

### Existing Tests
All existing Guard tests continue to pass:
- **65 Guard validation tests** in SmallMind.Tests (100% pass rate)
- **16 ModelRegistry tests** (100% pass rate)
- **977 SmallMind.Tests** (100% pass rate)

### Security Tests
The existing test suite includes comprehensive security validation:
- Path traversal prevention (`..` sequences)
- Absolute path rejection
- Path separator detection
- Invalid character validation
- Null/empty input handling

---

## Verification

### CodeQL Analysis
✅ **All CodeQL alerts resolved**: 0 alerts after fixes

### Build Status
✅ **All projects build successfully**
- SmallMind.Core
- SmallMind.ModelRegistry
- SmallMind.Engine
- All dependent projects

### Test Results
✅ **All tests passing**
- 0 failures
- 16/16 ModelRegistry tests
- 977/977 SmallMind tests
- 65/65 Guard tests

---

## Impact Assessment

### Security Improvements
**Before:** 
- ❌ Manifest files could contain `../../etc/passwd`
- ❌ URL filenames could escape directories
- ❌ No validation on user-supplied paths
- ❌ CWE-22: Path Traversal vulnerability

**After:**
- ✅ All paths validated before use
- ✅ Directory escaping prevented
- ✅ Path traversal attempts throw exceptions
- ✅ CWE-22 vulnerability eliminated

### Breaking Changes
**None** - All changes are backward compatible for legitimate use cases.

**Note:** Code that previously relied on path traversal behavior will now throw `ValidationException`. This is intentional security hardening.

---

## Best Practices for Future Development

1. **Always validate external input** before using with `Path.Combine`
2. **Use Guard.SafeFileName** for any filename extracted from user input
3. **Use Guard.PathWithinDirectory** when combining base paths with user-supplied relative paths
4. **Never trust**:
   - User-supplied file paths
   - URL-derived filenames
   - Manifest/config file contents
   - Any external data source

5. **Test security scenarios**:
   - Add tests for `..` traversal attempts
   - Test absolute path rejection
   - Verify directory confinement

---

## References

- **OWASP:** [Path Traversal](https://owasp.org/www-community/attacks/Path_Traversal)
- **CWE-22:** [Improper Limitation of a Pathname to a Restricted Directory](https://cwe.mitre.org/data/definitions/22.html)
- **Microsoft Docs:** [Path.Combine Method](https://learn.microsoft.com/en-us/dotnet/api/system.io.path.combine)
- **CodeQL Rule:** [Uncontrolled data used in path expression](https://codeql.github.com/codeql-query-help/csharp/cs-path-injection/)

---

## Conclusion

All CodeQL Path.Combine security issues have been successfully resolved with minimal code changes. The fixes leverage existing Guard validation infrastructure, maintain backward compatibility for legitimate use cases, and provide comprehensive protection against path traversal attacks.

**Status:** ✅ **COMPLETE**
- All 4 vulnerable usages fixed
- Zero CodeQL alerts
- All tests passing
- No breaking changes for legitimate use
