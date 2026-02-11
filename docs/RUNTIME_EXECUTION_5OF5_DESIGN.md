# Runtime Execution 5/5 Design Document

**Status:** In Progress  
**Target:** Elevate Runtime Execution maturity from 3.5/5 to 5/5  
**Created:** 2026-02-11

## Executive Summary

This document outlines the design and implementation strategy for achieving Runtime Execution maturity level 5/5 in SmallMind. The goal is to implement production-grade runtime features including hard prefill/decode separation, first-class KV cache, deterministic execution planning, memory discipline, threading strategy, and comprehensive telemetry.

## Current State Analysis (3.5/5)

### Existing Runtime Architecture

**Generation Flow (src/SmallMind.Runtime/InferenceSession.cs)**

The current implementation has a basic prefill/decode split:

```
GenerateNextTokenAsync(context, cancellationToken)
├─ if (!_kvCacheActive)                    // PREFILL PHASE
│  ├─ _model.ResetKVCache()
│  ├─ _model.EnableKVCache()
│  ├─ Build prefillTensor(1, promptLength)
│  ├─ logits = _model.Forward(prefillTensor, positionOffset: 0)
│  └─ _currentPosition = promptLength
│
└─ else                                     // DECODE PHASE
   ├─ _decodeData[0] = lastToken
   ├─ logits = _model.Forward(_decodeTensor, positionOffset: _currentPosition)
   └─ _currentPosition++
```

**Strengths:**
- ✅ Basic prefill/decode separation exists
- ✅ KV cache is functional with position tracking
- ✅ Reuses decode tensor to avoid per-token allocation
- ✅ ArrayPool used in KvCacheEntry for memory efficiency
- ✅ Position offset correctly tracked for incremental decoding

**Gaps (Why 3.5/5 instead of 5/5):**

1. **Not a Hard Split:**
   - Prefill/decode logic embedded in private method
   - No explicit API boundary for prefill vs decode
   - Cannot independently call prefill or decode
   - No separate result types (PrefillResult vs DecodeResult)

2. **KV Cache Not First-Class:**
   - Cache can be disabled (lines 273-276)
   - No mandatory cache enforcement
   - Cache reset on every generation (line 454)
   - No cache reuse across ChatSession turns
   - No sliding window support
   - No cache pooling/pre-allocation

3. **No Execution Planning:**
   - Forward pass recomputes shapes every call
   - No compiled operator schedule
   - No buffer reuse plan
   - No operator fusion
   - Shape caching exists but limited

4. **Allocation Sources in Decode:**
   - `contextCropped = new List<int>(_blockSize)` (line 466) in prefill
   - `prefillData = new float[promptLength]` (line 478)
   - `prefillTensor = new Tensor(...)` (line 483)
   - Logits buffer allocated per vocab size check (lines 518-521)
   - Repetition penalty buffers lazy-allocated (lines 41-43)

5. **No Telemetry Separation:**
   - Metrics track overall generation
   - No prefill vs decode tok/s separation
   - TTFT measured but not exposed as metric
   - No per-layer timing
   - No cache hit/miss tracking

6. **Threading Not Controlled:**
   - Parallelism exists in kernels but not configurable
   - No deterministic mode flag
   - No MaxDegreeOfParallelism option
   - Parallel.For used without threshold checks in some places

### KV Cache Implementation

**Current Architecture (src/SmallMind.Runtime/Cache/KvCacheEntry.cs)**

```csharp
internal sealed class KvCacheEntry : IDisposable
{
    private readonly ArrayPool<float> _pool;
    private float[][] _keyCaches;    // [layer][position * heads * headDim]
    private float[][] _valueCaches;  // [layer][position * heads * headDim]
    private int _currentTokenCount;
}
```

**Strengths:**
- ✅ Uses ArrayPool.Shared for zero-allocation steady state
- ✅ Per-layer storage with proper shape layout
- ✅ Supports GQA/MQA (via ModelShape.Heads)
- ✅ Provides AppendKV and GetKeys/GetValues APIs

**Gaps:**
- ❌ Not mandatory - can be bypassed
- ❌ Not reused across turns in ChatSession
- ❌ No sliding window (fixed maxTokens)
- ❌ No cache pooling (each session allocates fresh)
- ❌ Cache lifetime tied to KvCacheEntry, not session

**Model-Level KV Cache (src/SmallMind.Transformers/Core/Transformer.cs)**

The attention layer has its own cache:

```csharp
private bool _useKVCache = false;
private int _cachePosition = 0;
private Tensor? _cachedKeys;
private Tensor? _cachedValues;
```

**Issue:** Dual cache layers - TransformerModel has cache AND Runtime has KvCacheEntry. Need to unify.

### Hot Paths and Allocations

**Per-Token Decode Allocations:**

1. **InferenceSession.GenerateNextTokenAsync:**
   - Line 466: `contextCropped = new List<int>(_blockSize)` (prefill only)
   - Line 478: `prefillData = new float[promptLength]` (prefill only)
   - Line 483: `prefillTensor = new Tensor(...)` (prefill only)
   - Line 520: `_probabilityBuffer = new float[vocabSize]` (conditional, first time only)

2. **TransformerModel.Forward:**
   - Line 270: `tokEmbDest = _workspace.GetOrCreate(...)` (workspace reuse)
   - Line 300: `posEmbDest = _workspace.GetOrCreate(...)` (workspace reuse)
   - Line 306: `addEmbDest = _workspace.GetOrCreate(...)` (workspace reuse)

3. **MultiHeadAttention.Forward:**
   - Lines 1210-1212: `GetOrAllocateWorkspace(...)` for Q, K, V (conditional allocation)
   - Line 1290: `GetOrAllocateWorkspace(...)` for scores (conditional allocation)
   - Line 1296: `GetOrAllocateWorkspace(...)` for attention output (conditional allocation)

**Workspace Reuse Pattern:**
- GetOrAllocateWorkspace checks shape match before allocating
- Marks fresh workspaces to skip redundant clearing
- Conditionally clears based on kernel type (accumulation vs store-once)

**Overall Assessment:** Prefill has some allocations (acceptable), decode should be zero-allocation but isn't proven.

### Threading Strategy

**Current Parallelism:**

1. **SIMD Kernels (src/SmallMind.Core/Simd/):**
   - MatMulOps.cs: Uses Parallel.For for large matrices
   - No threshold checks - always parallelizes if available

2. **Attention (src/SmallMind.Transformers/Core/Transformer.cs):**
   - Line 1329: `if (B >= 4) Parallel.For(...)` 
   - Batch-based parallelism threshold (good practice)

3. **No Global Control:**
   - No MaxDegreeOfParallelism setting
   - No way to force single-threaded execution
   - No deterministic mode enforcement

### Telemetry

**Current Metrics (src/SmallMind.Runtime/Metrics/PerformanceMetrics.cs):**

Tracks:
- Request start/end
- First token time (TTFT)
- Request latency
- Throughput (tokens/second)

**Gaps:**
- No prefill vs decode separation
- No per-layer timing
- No cache metrics
- No allocation counters
- Metrics not exposed via Abstractions interface

---

## Target Architecture (5/5)

### 1. Hard Prefill/Decode Split

**New Internal API:**

```csharp
namespace SmallMind.Runtime.Execution
{
    internal interface IRuntimeExecutor
    {
        PrefillResult Prefill(ReadOnlySpan<int> promptTokens, ExecutionContext context);
        DecodeResult Decode(int nextToken, ExecutionContext context);
    }

    internal readonly struct PrefillResult
    {
        public readonly ReadOnlySpan<float> Logits;
        public readonly KvCacheHandle CacheHandle;
        public readonly int ProcessedTokens;
        public readonly PrefillMetrics Metrics;
    }

    internal readonly struct DecodeResult
    {
        public readonly ReadOnlySpan<float> Logits;
        public readonly KvCacheHandle CacheHandle;
        public readonly DecodeMetrics Metrics;
    }

    internal sealed class ExecutionContext
    {
        public KvCacheHandle? CacheHandle { get; set; }
        public int CurrentPosition { get; set; }
        public RuntimeOptions Options { get; }
        public ITelemetry Telemetry { get; }
    }
}
```

**Benefits:**
- Clear API boundary
- Type-safe results
- Explicit cache ownership
- Metrics built-in

### 2. First-Class KV Cache

**Cache Pool Architecture:**

```csharp
namespace SmallMind.Runtime.Cache
{
    internal sealed class KvCachePool : IDisposable
    {
        private readonly ConcurrentBag<KvCacheHandle>[] _pools;
        
        public KvCacheHandle Rent(ModelShape shape, int maxTokens);
        public void Return(KvCacheHandle handle);
    }

    internal sealed class KvCacheHandle : IDisposable
    {
        public int CurrentPosition { get; private set; }
        public int MaxTokens { get; }
        public bool SupportsSliding { get; }
        
        public void AppendKV(int layer, ReadOnlySpan<float> keys, ReadOnlySpan<float> values);
        public ReadOnlySpan<float> GetKeys(int layer, int startPos, int length);
        public ReadOnlySpan<float> GetValues(int layer, int startPos, int length);
        public void Slide(int windowSize);
        public void Reset();
    }
}
```

**Cache Lifecycle:**
1. Prefill: Rent cache from pool → populate with prompt K/V → return handle
2. Decode: Reuse handle → append single token K/V → return handle
3. ChatSession: Keep handle across turns → slide window when needed
4. Dispose: Return cache to pool

### 3. Execution Planning

**Plan Structure:**

```csharp
namespace SmallMind.Runtime.Execution
{
    internal sealed class ExecutionPlan
    {
        public LayerPlan[] Layers { get; }
        public BufferPlan BufferPlan { get; }
        public int TotalWorkspaceBytes { get; }
    }

    internal readonly struct LayerPlan
    {
        public readonly LayerOp[] Operations;
        public readonly int[] BufferIds; // Which buffers this layer uses
    }

    internal readonly struct LayerOp
    {
        public readonly OpType Type; // MatMul, LayerNorm, Attention, etc.
        public readonly int InputBufferId;
        public readonly int OutputBufferId;
        public readonly bool InPlace;
    }

    internal sealed class BufferPlan
    {
        private readonly float[][] _buffers;
        
        public Span<float> GetBuffer(int id);
        public void Reset(); // Clear for next execution
    }
}
```

**Benefits:**
- Pre-computed operator schedule
- Buffer reuse reduces allocations
- Clear execution order
- Enables operator fusion

### 4. Memory & GC Discipline

**Pooling Strategy:**

```csharp
namespace SmallMind.Runtime.Memory
{
    internal sealed class TensorPool : IDisposable
    {
        private readonly ArrayPool<float> _arrayPool;
        private readonly ConcurrentDictionary<int, ConcurrentBag<float[]>> _sizePool;
        
        public float[] Rent(int minSize);
        public void Return(float[] buffer);
    }

    internal sealed class SessionScope : IDisposable
    {
        private readonly TensorPool _tensorPool;
        private readonly KvCachePool _cachePool;
        private readonly List<float[]> _rentedBuffers;
        
        public Span<float> RentScratch(int size);
        
        public void Dispose()
        {
            // Return all rented buffers
            foreach (var buffer in _rentedBuffers)
                _tensorPool.Return(buffer);
        }
    }
}
```

**Memory Guardrails:**

```csharp
internal sealed class MemoryBudget
{
    public long MaxAllocatedBytes { get; }
    public long CurrentAllocatedBytes { get; private set; }
    
    public bool TryAllocate(long bytes);
    public void Release(long bytes);
    public void ThrowIfExceeded();
}
```

### 5. Threading Strategy

**Runtime Options:**

```csharp
public sealed class RuntimeOptions
{
    public int MaxDegreeOfParallelism { get; set; } = Environment.ProcessorCount;
    public bool DeterministicMode { get; set; } = false;
    public int ParallelizationThreshold { get; set; } = 1024;
}
```

**Kernel Wrapper:**

```csharp
internal static class ParallelHelper
{
    public static void ParallelFor(int fromInclusive, int toExclusive, 
        Action<int> body, RuntimeOptions options)
    {
        if (options.DeterministicMode)
        {
            // Force single-threaded
            for (int i = fromInclusive; i < toExclusive; i++)
                body(i);
        }
        else if (toExclusive - fromInclusive >= options.ParallelizationThreshold)
        {
            Parallel.For(fromInclusive, toExclusive, 
                new ParallelOptions { MaxDegreeOfParallelism = options.MaxDegreeOfParallelism },
                body);
        }
        else
        {
            // Below threshold - single-threaded
            for (int i = fromInclusive; i < toExclusive; i++)
                body(i);
        }
    }
}
```

### 6. Telemetry

**Abstractions Interface:**

```csharp
namespace SmallMind.Abstractions
{
    public interface IRuntimeTelemetry
    {
        void RecordPrefillStart(int tokenCount);
        void RecordPrefillEnd(int tokenCount, double elapsedMs);
        void RecordDecodeStart();
        void RecordDecodeEnd(double elapsedMs);
        void RecordTTFT(double elapsedMs);
        void RecordCacheHit(int layer);
        void RecordCacheMiss(int layer);
        void RecordAllocation(long bytes);
    }
}
```

**Implementation:**

```csharp
namespace SmallMind.Runtime.Telemetry
{
    internal sealed class RuntimeTelemetryCollector : IRuntimeTelemetry
    {
        private long _prefillTokens;
        private long _decodeTokens;
        private double _prefillTimeMs;
        private double _decodeTimeMs;
        private double _ttftMs;
        private readonly ConcurrentDictionary<int, long> _cacheHits;
        
        public TelemetrySnapshot GetSnapshot();
        
        public void Reset();
    }

    public readonly struct TelemetrySnapshot
    {
        public readonly double PrefillTokensPerSecond;
        public readonly double DecodeTokensPerSecond;
        public readonly double TimeToFirstToken;
        public readonly long TotalAllocations;
        public readonly double CacheHitRate;
    }
}
```

---

## Implementation Phases

### Phase 2: Define Runtime Execution Contract
**Files to Create:**
- `src/SmallMind.Runtime/Execution/IRuntimeExecutor.cs`
- `src/SmallMind.Runtime/Execution/PrefillResult.cs`
- `src/SmallMind.Runtime/Execution/DecodeResult.cs`
- `src/SmallMind.Runtime/Execution/ExecutionContext.cs`
- `src/SmallMind.Runtime/Execution/RuntimeOptions.cs`

### Phase 3: KV Cache First-Class
**Files to Modify:**
- `src/SmallMind.Runtime/Cache/KvCacheEntry.cs` - Add sliding window
- `src/SmallMind.Runtime/Cache/KvCachePool.cs` - NEW: Cache pooling
- `src/SmallMind.Runtime/Cache/KvCacheHandle.cs` - NEW: Cache handle wrapper

### Phase 4: Hard Prefill/Decode Split
**Files to Modify:**
- `src/SmallMind.Runtime/InferenceSession.cs` - Use IRuntimeExecutor
- `src/SmallMind.Runtime/Execution/RuntimeExecutor.cs` - NEW: Implementation
- `src/SmallMind.Engine/ChatSession.cs` - Reuse cache across turns

### Phase 5: Memory & Allocation Elimination
**Files to Create:**
- `src/SmallMind.Runtime/Memory/TensorPool.cs`
- `src/SmallMind.Runtime/Memory/SessionScope.cs`
- `src/SmallMind.Runtime/Memory/MemoryBudget.cs`

### Phase 6: Execution Planning
**Files to Create:**
- `src/SmallMind.Runtime/Execution/ExecutionPlan.cs`
- `src/SmallMind.Runtime/Execution/BufferPlan.cs`
- `src/SmallMind.Runtime/Execution/LayerPlan.cs`

### Phase 7: Threading Strategy
**Files to Create:**
- `src/SmallMind.Runtime/Execution/ParallelHelper.cs`
**Files to Modify:**
- All SIMD kernels to use ParallelHelper

### Phase 8: Telemetry & Benchmarks
**Files to Create:**
- `src/SmallMind.Abstractions/Telemetry/IRuntimeTelemetry.cs`
- `src/SmallMind.Runtime/Telemetry/RuntimeTelemetryCollector.cs`
- `benchmarks/RuntimeExecutionBenchmarks/PrefillVsDecodeBenchmark.cs`

### Phase 9: Validation
**Files to Create:**
- `tests/SmallMind.Tests/Runtime/PrefillDecodeCorrectnessTests.cs`
- `tests/SmallMind.Tests/Runtime/KvCacheReuseTests.cs`
- `tests/SmallMind.Tests/Runtime/SlidingWindowTests.cs`

---

## Success Metrics

### Functional Parity
- ✅ Prefill+Decode matches full forward within 1e-5 tolerance
- ✅ Sliding window maintains output quality
- ✅ Deterministic mode produces identical results

### Performance
- ✅ Zero allocations per decode token (measured)
- ✅ Decode tok/s improved by 10-25% (from hotpath audit)
- ✅ TTFT measured and exposed
- ✅ Cache reuse across turns in ChatSession

### API Contract
- ✅ All new APIs remain internal
- ✅ SmallMind.Abstractions defines IRuntimeTelemetry
- ✅ Backward compatibility maintained

---

## Known Limitations

1. **Quantized KV Cache:** Currently exists as QuantizedKvCacheEntry but not integrated
2. **Flash Attention:** Fused kernels exist but not yet used in execution plan
3. **Multi-GPU:** Out of scope (CPU-only implementation)
4. **Dynamic Batching:** Exists in BatchedInferenceEngine but not optimized

---

## References

- PERF_HOTPATH_AUDIT.md - Identifies 10-25% gain opportunity
- KV_CACHE_BUDGETS_COMPLETE.md - Cache budget implementation
- TIER1_CPU_PERFORMANCE_SUMMARY.md - Current performance baselines
- PRODUCTION_READINESS_SUMMARY.md - Production features roadmap
