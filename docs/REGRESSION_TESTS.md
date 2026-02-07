# SmallMind Regression Test Requirements

## Overview

This document defines the regression testing strategy for SmallMind's GA (General Availability) release. The test suite validates correctness, determinism, performance stability, and resource management to ensure safe releases.

**Key Principles:**
- Tests use only .NET libraries (no 3rd-party test/benchmark packages beyond xUnit)
- Zero hidden allocations or GC pressure in hot paths
- Deterministic, fast, and CI-friendly (<3 minutes)
- Actionable failure output pointing to component + metric

## Test Categories

### 1. Correctness Tests

**Purpose:** Validate that the model produces expected outputs for known inputs.

**Requirements:**
- End-to-end generation with expected token IDs for fixed prompts
- Logits shape validation (no NaNs/Infs, stable ranges)
- EOS (end-of-sequence) handling and stop conditions
- Token output correctness using tiny deterministic fixtures

**Thresholds:**
- Zero tolerance for incorrect token IDs (exact match required)
- Logits must be finite (no NaN/Inf)
- Logits range: typically [-20, 20] before softmax

**Location:** `tests/SmallMind.Tests/Regression/CorrectnessTests.cs`

### 2. Determinism Tests

**Purpose:** Ensure reproducible outputs for debugging and testing.

**Requirements:**
- Same seed + config + prompt → identical token stream
- Single-threaded mode for determinism tests
- Multiple runs produce identical results

**Thresholds:**
- Zero tolerance for non-determinism
- Byte-for-byte identical outputs across runs

**Location:** `tests/SmallMind.Tests/Regression/DeterminismTests.cs`

### 3. KV Cache Equivalence Tests

**Purpose:** Verify KV cache implementation doesn't change results.

**Requirements:**
- Compare cache-disabled vs cache-enabled for same prompt
- Selected token IDs must match exactly
- Logits comparison with floating-point tolerance (1e-5)
- GQA (Grouped Query Attention) head grouping correctness
- RoPE (Rotary Position Embedding) position alignment

**Thresholds:**
- Token IDs: exact match
- Logits: absolute difference ≤ 1e-5 or relative difference ≤ 1e-4
- Zero cache corruption (all cache hits return valid data)

**Location:** `tests/SmallMind.Tests/Regression/KVCacheTests.cs`

### 4. Memory Budget Enforcement Tests

**Purpose:** Validate resource limits are respected.

**Requirements:**
- Configure very small KV-cache budget
- Verify fail-fast behavior (no silent failures)
- Validate error messages contain actionable details:
  - Requested tokens
  - Maximum allowed tokens
  - Bytes required
- Ensure no partial initialization or leaked buffers

**Thresholds:**
- Zero tolerance for budget violations
- All errors must be actionable (contain context)

**Location:** `tests/SmallMind.Tests/Regression/BudgetEnforcementTests.cs`

### 5. Tokenizer & Model Metadata Tests

**Purpose:** Ensure tokenizer integration works correctly.

**Requirements:**
- Tokenizer metadata is read correctly (from GGUF or config)
- Consistent tokenization for known samples
- Special tokens handled correctly (BOS/EOS)
- Invalid tokenizer config fails with actionable error

**Thresholds:**
- Zero tolerance for tokenization mismatches
- All validation errors must be clear and actionable

**Location:** `tests/SmallMind.Tests/Regression/TokenizerTests.cs`

### 6. Model I/O & Import Tests

**Purpose:** Validate model loading and format compatibility.

**Requirements:**
- Import small GGUF fixture or pseudo-GGUF in tests
- All required tensors exist post-import
- Unsupported quantization types produce clear errors
- Tensor shapes map to expected architecture fields
- SMQ (SmallMind Quantized) format loads correctly

**Thresholds:**
- Zero tolerance for silent import failures
- All errors must specify missing/invalid component

**Location:** `tests/SmallMind.Tests/Regression/ModelIOTests.cs`

### 7. Performance Regression Guards

**Purpose:** Detect significant performance degradation.

**Requirements:**
- Fixed decode workload (e.g., prompt=16 tokens, generate=64 tokens)
- Measure using `Stopwatch` (no 3rd-party benchmarks)
- Metrics:
  - Tokens/second
  - TTFT (Time To First Token)
  - Allocated bytes during decode
  - GC collections (Gen0/1/2)

**Measurement Protocol:**
- Warmup: 2-3 runs before measurement
- Fixed thread count: `Environment.ProcessorCount` or specified
- Console logging disabled during measurement
- Release build required

**Thresholds:**
- Tokens/sec: must not drop >10% from within-run baseline
- TTFT: must not increase >15% from baseline
- Allocated bytes/token: ≤ 1KB (ideally ~0 in steady state)
- Gen2 collections: 0 during test

**Baseline Strategy:**
Use "relative within-run" comparisons to avoid CI flakiness:
- Compare cache-on vs cache-off (cache should be ≥2x faster)
- Compare allocations: decode loop should allocate near-zero
- No external baseline files (eliminates cross-platform issues)

**Location:** `tests/SmallMind.PerfTests/RegressionBenchmarks.cs`

### 8. Allocation/GC Regression Tests (Hard Gate)

**Purpose:** Prevent memory allocation regressions in hot paths.

**Requirements:**
- Measure `GC.GetAllocatedBytesForCurrentThread()` before/after decode loop
- Assert per-token allocation ≤ threshold
- Verify no Gen2 collections occur during test
- Diagnostic mode to print allocation sources if threshold exceeded

**Thresholds:**
- **HARD LIMIT:** ≤ 1KB allocated per token in steady-state decode
- **IDEAL:** ~0 bytes allocated per token (after warmup)
- Gen0/1 collections: acceptable (pooled short-lived allocations)
- Gen2 collections: 0 (indicates escaped allocations)

**Location:** `tests/SmallMind.PerfTests/AllocationRegressionTests.cs`

## Test Fixtures

### TinyModelFixture

**Purpose:** Provide deterministic, fast test models that don't require downloads.

**Configuration:**
```csharp
VocabSize: 128
ModelDim (dModel): 64
NumLayers: 2
NumHeads: 4
KVHeads: 2 (GQA with 2 groups)
HeadDim: 16 (dModel / NumHeads)
MaxSeqLen: 64
Dropout: 0.0 (disabled for determinism)
```

**Weight Initialization:**
- Deterministic seeded random (seed=42)
- No runtime randomness
- Weights initialized once and reused

**Tokenizer:**
- Simple CharTokenizer or deterministic BPE
- Known vocabulary for predictable encoding
- Special tokens: `<BOS>` (ID=0), `<EOS>` (ID=1), `<UNK>` (ID=2)

**Known Prompts & Expected Outputs:**
The fixture includes a suite of known prompts with expected next-token IDs:
- `"hello"` → next token ID: [expected_id]
- `"test"` → next token ID: [expected_id]
- Empty prompt → BOS handling

**Location:** `tests/SmallMind.Tests/Fixtures/TinyModelFixture.cs`

## Running Tests

### Run All Regression Tests

```bash
# Run all unit tests (includes correctness, determinism, KV cache, etc.)
dotnet test tests/SmallMind.Tests --filter "Category=Regression"

# Run performance tests (requires RUN_PERF_TESTS=true)
RUN_PERF_TESTS=true dotnet test tests/SmallMind.PerfTests
```

### Run Specific Test Categories

```bash
# Correctness only
dotnet test tests/SmallMind.Tests --filter "FullyQualifiedName~CorrectnessTests"

# KV Cache tests only
dotnet test tests/SmallMind.Tests --filter "FullyQualifiedName~KVCacheTests"

# Performance benchmarks
RUN_PERF_TESTS=true dotnet test tests/SmallMind.PerfTests --filter "FullyQualifiedName~RegressionBenchmarks"

# Allocation tests
RUN_PERF_TESTS=true dotnet test tests/SmallMind.PerfTests --filter "FullyQualifiedName~AllocationRegressionTests"
```

### Single Command (All)

```bash
# Run complete regression suite
./scripts/run-regression-tests.sh
```

Or on Windows:
```powershell
.\scripts\run-regression-tests.bat
```

## CI Integration

### GitHub Actions Workflow

The regression test suite runs on:
1. **Every PR** to `main` or `develop`:
   - Unit tests (correctness, determinism, KV cache, budget, tokenizer, model I/O)
   - Fast smoke performance test (<1 minute)

2. **Nightly builds**:
   - Full performance regression suite
   - Allocation regression tests
   - Extended workloads

3. **PRs labeled `performance`**:
   - Full performance suite (same as nightly)

### Expected Execution Time

- **Unit tests:** ~30-60 seconds (tiny models)
- **Performance tests:** ~2-3 minutes (includes warmup)
- **Total:** <3 minutes on CI infrastructure

### Workflow Configuration

See `.github/workflows/build.yml`:
```yaml
- name: Run Regression Tests
  run: dotnet test tests/SmallMind.Tests --filter "Category=Regression" --configuration Release

- name: Run Performance Regression Tests
  if: github.event_name == 'schedule' || contains(github.event.pull_request.labels.*.name, 'performance')
  env:
    RUN_PERF_TESTS: 'true'
  run: dotnet test tests/SmallMind.PerfTests --configuration Release
```

## Adding New Tests

### 1. Choose the Right Category

Determine which test category your test belongs to:
- **Correctness:** Output validation, logic checks
- **Determinism:** Reproducibility tests
- **KV Cache:** Cache correctness and equivalence
- **Budget:** Resource limit enforcement
- **Tokenizer:** Text encoding/decoding
- **Model I/O:** Import/export, format validation
- **Performance:** Throughput, latency benchmarks
- **Allocation:** Memory allocation tracking

### 2. Use Existing Fixtures

Leverage `TinyModelFixture` for fast, deterministic tests:

```csharp
[Fact]
[Trait("Category", "Regression")]
public void MyNewTest()
{
    // Arrange
    var fixture = new TinyModelFixture();
    var model = fixture.CreateModel();
    var tokenizer = fixture.CreateTokenizer();
    
    // Act & Assert
    // ...
}
```

### 3. Follow Naming Conventions

- **Test class:** `[Component][Aspect]RegressionTests.cs`
  - Example: `KVCacheEquivalenceTests.cs`
- **Test method:** `[Component]_[Scenario]_[ExpectedOutcome]`
  - Example: `KVCache_CacheEnabled_ProducesSameTokens`

### 4. Add Traits for Filtering

```csharp
[Fact]
[Trait("Category", "Regression")]
[Trait("Subcategory", "Correctness")]
public void MyTest()
{
    // ...
}
```

### 5. Include Diagnostic Output

For failures, provide context:

```csharp
Assert.True(
    tokensPerSecond > threshold,
    $"Performance regression detected: {tokensPerSecond:F2} tok/s < {threshold} tok/s threshold. " +
    $"Expected: >={threshold}, Actual: {tokensPerSecond:F2}, Diff: {threshold - tokensPerSecond:F2}"
);
```

### 6. Performance Test Template

```csharp
[Fact]
[Trait("Category", "Performance")]
public void MyPerformanceTest()
{
    if (!TestHelpers.ShouldRunPerfTests()) return;
    
    // Warmup
    for (int i = 0; i < 3; i++)
    {
        // warmup logic
    }
    
    // Measure
    var sw = Stopwatch.StartNew();
    for (int i = 0; i < 10; i++)
    {
        // measured logic
    }
    sw.Stop();
    
    double avgMs = sw.Elapsed.TotalMilliseconds / 10;
    Assert.True(avgMs < threshold, $"Took {avgMs:F2}ms, expected < {threshold}ms");
}
```

## GA-Blocking Failure Definitions

A test failure is **GA-blocking** if:

1. **Correctness Failure:**
   - Wrong token IDs generated
   - NaN/Inf in outputs
   - EOS not handled correctly

2. **Determinism Failure:**
   - Same seed produces different outputs
   - Non-reproducible results

3. **KV Cache Corruption:**
   - Cache-enabled produces different tokens than cache-disabled
   - Cache hit returns incorrect data

4. **Budget Violation:**
   - Allocation exceeds configured limit without error
   - Silent failure on OOM conditions

5. **Severe Performance Regression:**
   - >25% throughput drop (tokens/sec)
   - >2x latency increase (TTFT)
   - Gen2 GC during decode (indicates memory leak)

6. **Severe Allocation Regression:**
   - >10KB allocated per token (indicates pooling failure)
   - Continuous Gen2 collections (memory pressure)

**Non-blocking failures** (warnings):
- Minor performance variations (<10%)
- Platform-specific timing differences
- Hardware-dependent thresholds exceeded with justification

## Troubleshooting

### Test Fails Only in CI

**Possible causes:**
1. Hardware differences (different CPU, memory speed)
2. Background processes affecting timing
3. Different .NET runtime version

**Solutions:**
- Check CI environment specs
- Increase threshold by 10-15% if consistently failing
- Add `[Trait("CI", "Skip")]` if truly environment-specific
- Document threshold rationale in test comments

### Allocation Test Fails

**Diagnostic steps:**
1. Run with diagnostic mode: `ALLOCATION_DIAGNOSTICS=true`
2. Check for:
   - LINQ usage in hot paths (`.ToArray()`, `.Select()`)
   - String concatenation in loops
   - Unnecessary `new[]` allocations
   - Missing `ArrayPool<T>` usage
3. Use `dotnet-trace` or PerfView for detailed analysis

### Performance Test Inconsistent

**Best practices:**
1. Close other applications
2. Use Release build: `dotnet test -c Release`
3. Run multiple times and check variance
4. Check CPU throttling/power settings
5. Disable CPU frequency scaling on CI

### Determinism Test Fails

**Check for:**
1. Uninitialized variables
2. Concurrent access to shared state
3. Timestamp or GUID usage in generation
4. Random number generator not seeded consistently
5. Parallel operations with non-deterministic ordering

## Metrics Reference

### Performance Metrics

| Metric | Tiny Model Target | Small Model Target | Notes |
|--------|-------------------|-------------------|-------|
| Tokens/sec | >100 | >50 | Highly hardware-dependent |
| TTFT (ms) | <50 | <100 | Time to first token |
| Allocated/token | ~0 bytes | ~0 bytes | After warmup |
| Gen0/1 collections | Acceptable | Acceptable | Pooled short-lived allocs |
| Gen2 collections | 0 | 0 | Hard requirement |

### Model Sizes (Test Fixtures)

| Fixture | Params | Memory | Use Case |
|---------|--------|--------|----------|
| Tiny | ~50K | <1MB | Unit tests, CI |
| Small | ~470K | ~2MB | Integration tests |
| Medium | ~3.5M | ~14MB | Nightly perf tests |

## References

- [Performance Testing Best Practices](PERFORMANCE_SUMMARY.md)
- [Allocation Optimization Guide](ALLOCATION_REDUCTION_COMPARISON.md)
- [KV Cache Documentation](../src/SmallMind.Runtime/Cache/README.md)
- [Tokenizer Integration](tokenizers.md)
- [CI/CD Workflows](WORKFLOWS.md)

## Version History

- **v1.0.0** (2026-02-07): Initial regression test requirements for GA
