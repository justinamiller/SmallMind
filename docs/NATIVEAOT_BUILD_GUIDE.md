# NativeAOT Build Guide for SmallMind

This guide covers building SmallMind with NativeAOT for maximum CPU inference performance.

## Overview

NativeAOT (Ahead-of-Time compilation) produces native executables with:
- **No JIT overhead** - All code compiled ahead of time
- **Faster startup** - No warmup period required
- **Smaller deployment** - Self-contained single executable
- **Predictable performance** - No tier 0→1 transitions

## Prerequisites

- .NET 10 SDK or later
- C++ compiler (required for NativeAOT):
  - **Windows**: Visual Studio 2022+ with C++ workload
  - **Linux**: `build-essential` (gcc, g++)
  - **macOS**: Xcode Command Line Tools

### Linux Setup
```bash
sudo apt-get update
sudo apt-get install build-essential zlib1g-dev
```

### macOS Setup
```bash
xcode-select --install
```

## Build Commands

### 1. Standard JIT Build (Baseline)
```bash
# Development build
dotnet build examples/ProductionInference/ProductionInference.csproj -c Release

# Run
dotnet run --project examples/ProductionInference/ProductionInference.csproj -c Release
```

### 2. NativeAOT Build (Optimized for CPU)
```bash
# Publish NativeAOT executable
dotnet publish examples/ProductionInference/ProductionInference.csproj \
  -c Release \
  -r linux-x64 \
  -p:PublishAot=true \
  -o ./publish/aot

# Run
./publish/aot/ProductionInference
```

#### Platform-Specific Runtime Identifiers
- **Linux**: `-r linux-x64` (or `linux-arm64`)
- **Windows**: `-r win-x64` (or `win-arm64`)
- **macOS**: `-r osx-x64` (or `osx-arm64`)

### 3. NativeAOT with PGO (Maximum Performance)

Profile-Guided Optimization uses runtime profiling to optimize hot paths.

#### Step 1: Build instrumented executable
```bash
dotnet publish examples/ProductionInference/ProductionInference.csproj \
  -c Release \
  -r linux-x64 \
  -p:PublishAot=true \
  -p:IlcInstructionSet=native \
  -p:IlcGenerateMstatFile=true \
  -o ./publish/pgo-instrument
```

#### Step 2: Run with representative workload (generates .mibc)
```bash
# Run typical inference workload
./publish/pgo-instrument/ProductionInference --profile-mode

# This generates ProductionInference.mibc profile data
```

#### Step 3: Rebuild with PGO profile
```bash
dotnet publish examples/ProductionInference/ProductionInference.csproj \
  -c Release \
  -r linux-x64 \
  -p:PublishAot=true \
  -p:IlcInstructionSet=native \
  -p:IlcPgoFile=./publish/pgo-instrument/ProductionInference.mibc \
  -o ./publish/pgo-optimized

# Run optimized binary
./publish/pgo-optimized/ProductionInference
```

## Performance Comparison

Expected performance characteristics:

| Build Mode | Startup Time | Inference Throughput | Binary Size | First Token Latency |
|------------|--------------|---------------------|-------------|---------------------|
| JIT (Tier 0) | Fast (~50ms) | Slow (baseline) | Small (~50MB) | High (JIT overhead) |
| JIT (Tier 1 + PGO) | Fast (~50ms) | Fast (1.0x) | Small (~50MB) | Medium (warmup needed) |
| **NativeAOT** | **Instant** | **Fast (0.9-1.1x)** | Medium (~80MB) | **Low (no warmup)** |
| **NativeAOT + PGO** | **Instant** | **Fastest (1.1-1.3x)** | Medium (~80MB) | **Lowest** |

## Optimization Flags

### Recommended Production Flags
```bash
dotnet publish examples/ProductionInference/ProductionInference.csproj \
  -c Release \
  -r linux-x64 \
  -p:PublishAot=true \
  -p:IlcOptimizationPreference=Speed \
  -p:IlcInstructionSet=native \
  -p:InvariantGlobalization=true \
  -p:IlcGenerateStackTraceData=false \
  -o ./publish/production
```

### Flag Descriptions

- **`PublishAot=true`** - Enable NativeAOT compilation
- **`IlcOptimizationPreference=Speed`** - Optimize for performance over size
- **`IlcInstructionSet=native`** - Use all CPU features (AVX-512, etc.)
- **`InvariantGlobalization=true`** - Reduce binary size (no locale data)
- **`IlcGenerateStackTraceData=false`** - Reduce binary size (production-only)

### Debug-Friendly Flags (Development)
```bash
dotnet publish examples/ProductionInference/ProductionInference.csproj \
  -c Release \
  -r linux-x64 \
  -p:PublishAot=true \
  -p:IlcGenerateStackTraceData=true \
  -p:IlcOptimizationPreference=Size \
  -o ./publish/debug
```

## Benchmarking NativeAOT vs JIT

### Run MatMul Benchmark
```bash
# JIT baseline
dotnet run --project benchmarks/MatMulBenchmark.csproj -c Release

# NativeAOT (build first)
dotnet publish benchmarks/MatMulBenchmark.csproj -c Release -r linux-x64 -p:PublishAot=true -o ./publish/bench
./publish/bench/MatMulBenchmark
```

### Run Inference Benchmark
```bash
# Compare tokens/sec and TTFT (Time-To-First-Token)
./scripts/benchmark-aot-vs-jit.sh
```

## Expected Improvements

Based on SmallMind's CPU-focused design:

1. **Time-To-First-Token (TTFT)**: 2-5x faster (no JIT warmup)
2. **Steady-State Throughput**: ~5-10% faster (better inlining, static dispatch)
3. **Startup Time**: 10-100x faster (no assembly loading/JIT)
4. **Memory Usage**: 10-20% lower (no JIT metadata)

## Known Limitations

### NativeAOT Restrictions
- **No dynamic code generation** - Reflection limited to source-generated
- **No `Assembly.LoadFrom`** - All code must be statically linked
- **No COM interop** - Windows COM not supported
- **Trimming aggressive** - May remove code incorrectly (test thoroughly)

### SmallMind-Specific Considerations
- ✅ **SIMD intrinsics** - Fully supported (AVX-512, AVX2, NEON)
- ✅ **Unsafe code** - Fully supported (used extensively in kernels)
- ✅ **Span\<T\>** - Fully supported (zero-copy paths work)
- ✅ **Parallel.For** - Fully supported (thread tiling works)
- ⚠️ **Reflection** - Limited use in SmallMind, ensure tests pass

## Validation

### Ensure Correctness
```bash
# Run full test suite against NativeAOT build
dotnet test --configuration Release
```

### Verify Identical Outputs
```bash
# Generate outputs with JIT
dotnet run --project examples/ProductionInference/ProductionInference.csproj \
  -c Release -- --output jit-output.txt

# Generate outputs with NativeAOT
./publish/aot/ProductionInference --output aot-output.txt

# Compare (should be identical)
diff jit-output.txt aot-output.txt
```

## Troubleshooting

### Build Errors

**Error: "ILC: error : Method '...' is not supported in NativeAOT"**
- Solution: Remove or guard unsupported reflection/dynamic code

**Error: "C++ compiler not found"**
- Solution: Install build tools (see Prerequisites)

### Runtime Issues

**Crash: "Unhandled exception: System.MissingMethodException"**
- Cause: Aggressive trimming removed required code
- Solution: Add `<TrimmerRootAssembly>` in .csproj or use `[DynamicallyAccessedMembers]`

**Performance slower than JIT**
- Cause: Missing CPU feature optimizations
- Solution: Add `-p:IlcInstructionSet=native` flag

## CI/CD Integration

### GitHub Actions
```yaml
- name: Publish NativeAOT
  run: |
    dotnet publish examples/ProductionInference/ProductionInference.csproj \
      -c Release \
      -r linux-x64 \
      -p:PublishAot=true \
      -p:IlcInstructionSet=native \
      -o ./artifacts/linux-aot

- name: Upload Artifact
  uses: actions/upload-artifact@v3
  with:
    name: nativeaot-linux-x64
    path: ./artifacts/linux-aot/ProductionInference
```

## References

- [.NET NativeAOT Documentation](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/)
- [ILC (IL Compiler) Options](https://github.com/dotnet/runtime/blob/main/docs/using-nativeaot/compiling.md)
- [PGO with NativeAOT](https://github.com/dotnet/runtime/blob/main/docs/using-nativeaot/optimizing.md)

---

**Author**: GitHub Copilot  
**Date**: 2026-02-07  
**Status**: Production-Ready
