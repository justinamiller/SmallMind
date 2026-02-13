# SmallMind Release Checklist

This document provides a comprehensive checklist for preparing and executing a production-ready release of SmallMind.

## Pre-Release Validation

### 1. CI/CD Health

- [ ] **All CI workflows passing**
  - [ ] Build and Test workflow (Ubuntu, Windows, macOS)
  - [ ] Golden Tests workflow
  - [ ] CodeQL analysis (no high/critical findings)
  - [ ] Performance smoke tests (if scheduled/triggered)

- [ ] **No known critical bugs**
  - [ ] Review open issues labeled `bug` and `critical`
  - [ ] Verify all P0/P1 issues are resolved or deferred with justification

### 2. Testing

- [ ] **Unit Tests**
  - [ ] All unit tests passing on all OS targets (Ubuntu, Windows, macOS)
  - [ ] No flaky tests observed in recent runs
  - [ ] Code coverage ≥80% for core modules (SmallMind, SmallMind.Runtime)

- [ ] **Integration Tests**
  - [ ] All integration tests passing
  - [ ] End-to-end workflow tests validated
  - [ ] GGUF model loading tests (if GGUF support is stable)

- [ ] **Golden Output Tests**
  - [ ] Golden regression tests passing
  - [ ] Deterministic output validated across platforms
  - [ ] No unexpected golden value changes

- [ ] **Performance Regression Tests**
  - [ ] Performance benchmarks run successfully
  - [ ] No regressions exceeding thresholds (allocation, GFLOPS, GC)
  - [ ] Review `artifacts/perf/perf-results-latest.md` for any red flags

- [ ] **Negative Tests (GGUF)**
  - [ ] GgufNegativeTests passing (corrupted files, missing tensors, etc.)
  - [ ] Error messages are actionable and user-friendly

### 3. Documentation

- [ ] **README.md**
  - [ ] Updated version number
  - [ ] Installation instructions current
  - [ ] Quick start examples tested
  - [ ] Badges reflect latest status (build, CodeQL, NuGet)

- [ ] **CHANGELOG.md**
  - [ ] All changes since last release documented
  - [ ] Breaking changes clearly marked
  - [ ] Contributors acknowledged (if applicable)

- [ ] **API Documentation**
  - [ ] XML doc comments complete for public API
  - [ ] No missing or outdated documentation warnings
  - [ ] API reference generated (if applicable)

- [ ] **Compatibility Matrix**
  - [ ] `docs/compatibility-matrix.md` updated
  - [ ] `docs/GGUF_TENSOR_COMPATIBILITY_MATRIX.md` accurate
  - [ ] Supported/unsupported features clearly documented

- [ ] **Performance Documentation**
  - [ ] `PERFORMANCE_RESULTS_SUMMARY.md` reflects latest benchmarks
  - [ ] `docs/benchmarking.md` up to date with CLI changes

- [ ] **Server Documentation**
  - [ ] `docs/SERVER_OPENAI_COMPAT.md` accurate
  - [ ] `tools/SmallMind.Server/README.md` current

### 4. Security

- [ ] **CodeQL Analysis**
  - [ ] Latest CodeQL scan passed with no high/critical alerts
  - [ ] Review and resolve medium/low alerts or document as false positives

- [ ] **Dependency Audit**
  - [ ] No known vulnerabilities in NuGet dependencies
  - [ ] All dependencies up to date (or pinned with justification)
  - [ ] BCL-only constraint maintained (no third-party ML libraries)

- [ ] **Secrets Management**
  - [ ] No hardcoded secrets in source code
  - [ ] `.gitignore` properly configured
  - [ ] API keys, tokens, credentials excluded from repository

### 5. API Stability

- [ ] **Public API Review**
  - [ ] No breaking changes to `SmallMind` namespace (stable contract)
  - [ ] Experimental APIs marked with `[Obsolete]` or similar attribute
  - [ ] New APIs reviewed for consistency and usability

- [ ] **Contract Surface Guard**
  - [ ] `ContractSurfaceGuardTests` passing
  - [ ] Public API surface matches expected contract
  - [ ] No unintended additions to stable API

### 6. Performance Validation

- [ ] **Benchmarks**
  - [ ] Kernel benchmarks (MatMul) meet GFLOPS target (50-150 on AVX2)
  - [ ] Zero allocations in hot paths (decode phase)
  - [ ] Zero GC collections during steady-state inference

- [ ] **Baseline Comparison**
  - [ ] Compare to previous release baseline (if available)
  - [ ] No regressions in tokens/sec, TTFT, or memory usage
  - [ ] Document any intentional performance trade-offs

### 7. Packaging

- [ ] **NuGet Packages**
  - [ ] `SmallMind.csproj` package metadata current (version, authors, description, license)
  - [ ] `SmallMind.Runtime.csproj` package metadata current
  - [ ] Package dependencies correct (no over-specification)
  - [ ] Symbols package included (`.snupkg`)

- [ ] **Build Artifacts**
  - [ ] Release build succeeds on all target platforms
  - [ ] No warnings in Release configuration (or justified and documented)
  - [ ] Binary size within expected range

## Release Execution

### 1. Version Bump

- [ ] **Update Version**
  - [ ] Decide version number (SemVer: MAJOR.MINOR.PATCH)
  - [ ] Update `<Version>` in `.csproj` files
  - [ ] Update `README.md` and `CHANGELOG.md` with version

- [ ] **Tag Release**
  - [ ] Create annotated Git tag: `git tag -a v1.0.0 -m "Release v1.0.0"`
  - [ ] Verify tag points to correct commit

### 2. Pre-Release Build

- [ ] **Test Build Locally**
  - [ ] Run `dotnet build -c Release` successfully
  - [ ] Run full test suite: `dotnet test -c Release`
  - [ ] Verify no unexpected warnings or errors

- [ ] **Pack Locally**
  - [ ] Run `dotnet pack -c Release`
  - [ ] Inspect `.nupkg` contents (unzip and verify file list)
  - [ ] Test package installation in a clean project

### 3. CI Validation

- [ ] **Trigger Publish Workflow**
  - [ ] Push tag: `git push origin v1.0.0`
  - [ ] Monitor GitHub Actions workflow progress
  - [ ] Verify all jobs pass (build, test, pack, publish)

- [ ] **Review Artifacts**
  - [ ] Download artifacts from GitHub Actions
  - [ ] Verify package version and contents
  - [ ] Check symbols package (`.snupkg`)

### 4. NuGet Publication

- [ ] **NuGet.org (if configured)**
  - [ ] Verify `NUGET_API_KEY` secret is set in repository
  - [ ] Workflow publishes to NuGet.org successfully
  - [ ] Packages visible at https://www.nuget.org/packages/SmallMind

- [ ] **GitHub Packages**
  - [ ] Workflow publishes to GitHub Packages
  - [ ] Packages visible in repository packages

### 5. Post-Release Validation

- [ ] **Test Installation**
  - [ ] Create a new project: `dotnet new console`
  - [ ] Install package: `dotnet add package SmallMind --version 1.0.0`
  - [ ] Verify package resolves and restores correctly
  - [ ] Run quick start example from README

- [ ] **Update Documentation**
  - [ ] Create GitHub Release with changelog
  - [ ] Attach release artifacts (if applicable)
  - [ ] Link to NuGet package in release notes

- [ ] **Announce Release**
  - [ ] Post release notes in Discussions (if enabled)
  - [ ] Update project website (if applicable)
  - [ ] Notify users via Twitter, blog, etc. (if applicable)

## Rollback Plan

If critical issues are discovered post-release:

1. **Immediate Actions**
   - [ ] Un-list problematic NuGet package version (do not delete)
   - [ ] Create GitHub issue describing the problem
   - [ ] Alert users via release notes update

2. **Hotfix Process**
   - [ ] Branch from release tag: `git checkout -b hotfix-v1.0.1 v1.0.0`
   - [ ] Fix critical issue
   - [ ] Run full test suite
   - [ ] Tag hotfix: `git tag -a v1.0.1 -m "Hotfix v1.0.1"`
   - [ ] Publish hotfix via workflow

3. **Communication**
   - [ ] Update CHANGELOG.md with hotfix details
   - [ ] Post advisory in GitHub Releases
   - [ ] Recommend users upgrade to hotfix version

## Release Types

### Major Release (1.0.0 → 2.0.0)

Breaking changes allowed. Use for:
- Public API changes
- Model format version changes
- Major architectural changes

**Extra Validation:**
- [ ] Migration guide provided for breaking changes
- [ ] Deprecated APIs clearly documented
- [ ] Compatibility matrix updated

### Minor Release (1.0.0 → 1.1.0)

New features, no breaking changes. Use for:
- New capabilities (e.g., new model architectures)
- Performance improvements
- New APIs (additive only)

**Extra Validation:**
- [ ] New features documented
- [ ] Examples provided for new capabilities

### Patch Release (1.0.0 → 1.0.1)

Bug fixes only, no new features. Use for:
- Critical bug fixes
- Security patches
- Documentation corrections

**Extra Validation:**
- [ ] Regression tests added for fixed bugs
- [ ] Security advisories published (if applicable)

## Approval Checklist

Before merging and tagging:

- [ ] **Code Review:** At least one maintainer approval
- [ ] **Testing:** All automated tests passing
- [ ] **Documentation:** Reviewed and updated
- [ ] **Performance:** No unexpected regressions
- [ ] **Security:** CodeQL clean or issues triaged

## Notes

- **BCL-Only Constraint:** SmallMind must remain free of third-party ML dependencies. Verify no new dependencies sneak in.
- **CPU-Only:** No GPU dependencies or code paths.
- **Determinism:** Golden tests ensure cross-platform reproducibility. Validate on Ubuntu, Windows, macOS.
- **API Stability:** `SmallMind` namespace is the stable contract. Changes require major version bump.

---

**Last Updated:** 2026-02-13  
**Release Manager:** (To be assigned per release)
