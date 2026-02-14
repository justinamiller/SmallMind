# GitHub Workflows Update Summary

## Overview
Updated all GitHub Actions workflow files to reference the new consolidated benchmark directory structure.

## Changes Made

### 1. `.github/workflows/bench-ci.yml`
**Purpose:** CI benchmark runs on PRs and main branch

**Changes:**
- ✅ Build step: `bench/SmallMind.Benchmarks/SmallMind.Benchmarks.csproj` → `benchmarks/ModelInference/SmallMind.Benchmarks.csproj`
- ✅ Run step: Updated project path to `benchmarks/ModelInference/SmallMind.Benchmarks.csproj`
- ✅ Upload path: `bench/results/` → `benchmarks/results/`
- ✅ Summary display paths: Updated to reference `benchmarks/results/`

### 2. `.github/workflows/bench-nightly.yml`
**Purpose:** Full nightly benchmark runs

**Changes:**
- ✅ Build step: `bench/SmallMind.Benchmarks/SmallMind.Benchmarks.csproj` → `benchmarks/ModelInference/SmallMind.Benchmarks.csproj`
- ✅ Run step: Updated project path to `benchmarks/ModelInference/SmallMind.Benchmarks.csproj`
- ✅ Upload path: `bench/results/` → `benchmarks/results/`
- ✅ Summary display paths: Updated to reference `benchmarks/results/`

### 3. `.github/workflows/build.yml`
**Purpose:** Main build and test workflow

**Changes:**
- ✅ SIMD Benchmarks step: 
  - From: `cd benchmarks && dotnet run --configuration Release`
  - To: `cd benchmarks/KernelBenchmarks && dotnet run --configuration Release --project SimdBenchmarks.csproj`
- ✅ SmallMind Performance Benchmarks step:
  - From: `cd src/SmallMind.Benchmarks` (directory no longer exists!)
  - To: `cd benchmarks/KernelBenchmarks/SmallMind.KernelBenchmarks`
  - Output directory path adjusted: `../../artifacts/perf` → `../../../artifacts/perf`

### 4. `.github/codeql-config.yml`
**Status:** No changes needed

**Reason:** Already uses generic `benchmarks` path in exclusion list, which works with both old and new structure.

## Documentation Updates

### `benchmarks/README_ModelInference.md`
- ✅ All example commands updated from `bench/SmallMind.Benchmarks` to `benchmarks/ModelInference/SmallMind.Benchmarks`
- ✅ Default paths in option table updated
- ✅ Output format paths updated: `bench/results/` → `benchmarks/results/`
- ✅ Model manifest path updated: `bench/models/` → `benchmarks/models/`

## Migration Mapping

| Old Path | New Path | Purpose |
|----------|----------|---------|
| `bench/SmallMind.Benchmarks/` | `benchmarks/ModelInference/` | Real-model inference benchmarks |
| `src/SmallMind.Benchmarks/` | `benchmarks/KernelBenchmarks/SmallMind.KernelBenchmarks/` | Kernel benchmarks (GEMM, Q4) |
| `benchmarks/` (old, vague) | `benchmarks/KernelBenchmarks/SimdBenchmarks.csproj` | SIMD-specific benchmarks |
| `bench/results/` | `benchmarks/results/` | Benchmark output directory |
| `bench/models/` | `benchmarks/models/` | Model manifest location |

## Verification

### Files Modified
1. `.github/workflows/bench-ci.yml` - 14 lines changed
2. `.github/workflows/bench-nightly.yml` - 10 lines changed
3. `.github/workflows/build.yml` - 4 lines changed
4. `benchmarks/README_ModelInference.md` - 26 lines changed

### Total: 4 files, 54 lines changed

### Build Verification
- ✅ All workflow YAML files have valid syntax
- ✅ All referenced project paths exist in new structure
- ✅ All output directories use consistent new paths
- ✅ No merge conflicts present

## Impact

### Workflows That Will Work
- ✅ `bench-ci.yml` - Will run benchmarks on PRs and main
- ✅ `bench-nightly.yml` - Will run full nightly benchmarks
- ✅ `build.yml` - Will run SIMD and kernel benchmarks during build

### What Was Fixed
1. **Broken paths**: `src/SmallMind.Benchmarks` no longer existed, now points to correct location
2. **Ambiguous paths**: `cd benchmarks && dotnet run` was unclear, now explicitly targets `SimdBenchmarks.csproj`
3. **Output paths**: All results now consistently go to `benchmarks/results/`
4. **Documentation**: README updated to match actual paths

## Testing Recommendations

When the PR is merged, monitor:
1. First CI run after merge - verify `bench-ci.yml` works
2. First nightly run - verify `bench-nightly.yml` works
3. First full build - verify `build.yml` benchmark steps work

## Notes

- CodeQL exclusions already use generic "benchmarks" path, so no changes needed
- Old documentation files with historical examples (MULTI_ARCH_BENCHMARK_REPORT.md, etc.) were not updated as they're historical references
- All critical CI/CD workflows have been updated and will work with new structure
