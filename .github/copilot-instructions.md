# SmallMind - GitHub Copilot Performance Optimization Instructions

> **Purpose**: Guide Copilot to suggest performance-optimized code for CPU-bound operations, memory efficiency, and overall throughput in this pure C# LLM implementation.

## Project Context

SmallMind is an educational, pure C# language model with:
- Custom automatic differentiation (no external ML libraries)
- Decoder-only Transformer architecture
- CPU-only execution (no GPU)
- Character-level tokenization
- Training and inference capabilities

**Performance is critical** because all operations run on CPU without optimized linear algebra libraries.

---

## 🔴 Critical Performance Rules

### 1. AVOID ALLOCATIONS IN HOT PATHS

```csharp
// ❌ BAD: Allocates new array every call
public float[] Forward(float[] input)
{
    float[] output = new float[input.Length];  // GC pressure!
    // ...
    return output;
}

// ✅ GOOD: Reuse pre-allocated buffers
public void Forward(float[] input, float[] output)
{
    // output is pre-allocated and reused
    // ...
}

// ✅ BETTER: Use ArrayPool for temporary buffers
public void Forward(ReadOnlySpan<float> input, Span<float> output)
{
    float[] temp = ArrayPool<float>.Shared.Rent(input.Length);
    try
    {
        // Use temp...
    }
    finally
    {
        ArrayPool<float>.Shared.Return(temp);
    }
}
```

### 2. USE SPAN<T> AND MEMORY<T> FOR ARRAY OPERATIONS

```csharp
// ❌ BAD: Array slicing creates copies
float[] slice = data.Skip(offset).Take(length).ToArray();

// ✅ GOOD: Span creates views without allocation
Span<float> slice = data.AsSpan(offset, length);
ReadOnlySpan<float> readSlice = data.AsSpan(offset, length);
```

### 3. PREFER SIMD VECTORIZATION

```csharp
// ❌ BAD: Scalar operations
for (int i = 0; i < length; i++)
{
    result[i] = a[i] + b[i];
}

// ✅ GOOD: Use Vector<T> for SIMD
using System.Numerics;

int vectorSize = Vector<float>.Count;
int i = 0;

// Process SIMD-width chunks
for (; i <= length - vectorSize; i += vectorSize)
{
    var va = new Vector<float>(a, i);
    var vb = new Vector<float>(b, i);
    (va + vb).CopyTo(result, i);
}

// Handle remainder
for (; i < length; i++)
{
    result[i] = a[i] + b[i];
}
```

### 4. OPTIMIZE MATRIX MULTIPLICATION

Matrix multiplication is the #1 bottleneck. Use these patterns:

```csharp
// ❌ BAD: Naive triple loop with poor cache locality
for (int i = 0; i < M; i++)
    for (int j = 0; j < N; j++)
        for (int k = 0; k < K; k++)
            C[i, j] += A[i, k] * B[k, j];

// ✅ GOOD: Cache-friendly loop order (ikj instead of ijk)
for (int i = 0; i < M; i++)
{
    for (int k = 0; k < K; k++)
    {
        float aik = A[i * K + k];
        int bRowStart = k * N;
        int cRowStart = i * N;
        
        for (int j = 0; j < N; j++)
        {
            C[cRowStart + j] += aik * B[bRowStart + j];
        }
    }
}

// ✅ BETTER: Tiled/blocked multiplication for L1 cache
const int TILE_SIZE = 32; // Tune for your CPU's L1 cache

for (int i0 = 0; i0 < M; i0 += TILE_SIZE)
{
    for (int k0 = 0; k0 < K; k0 += TILE_SIZE)
    {
        for (int j0 = 0; j0 < N; j0 += TILE_SIZE)
        {
            int iMax = Math.Min(i0 + TILE_SIZE, M);
            int kMax = Math.Min(k0 + TILE_SIZE, K);
            int jMax = Math.Min(j0 + TILE_SIZE, N);
            
            for (int i = i0; i < iMax; i++)
            {
                for (int k = k0; k < kMax; k++)
                {
                    float aik = A[i * K + k];
                    for (int j = j0; j < jMax; j++)
                    {
                        C[i * N + j] += aik * B[k * N + j];
                    }
                }
            }
        }
    }
}

// ✅ BEST: SIMD + Tiled + Parallel
public static void MatMulOptimized(
    ReadOnlySpan<float> A, ReadOnlySpan<float> B, Span<float> C,
    int M, int K, int N)
{
    const int TILE = 32;
    int vectorSize = Vector<float>.Count;
    
    Parallel.For(0, (M + TILE - 1) / TILE, i0Block =>
    {
        int i0 = i0Block * TILE;
        int iMax = Math.Min(i0 + TILE, M);
        
        for (int k0 = 0; k0 < K; k0 += TILE)
        {
            int kMax = Math.Min(k0 + TILE, K);
            
            for (int j0 = 0; j0 < N; j0 += TILE)
            {
                int jMax = Math.Min(j0 + TILE, N);
                
                for (int i = i0; i < iMax; i++)
                {
                    int cRow = i * N;
                    
                    for (int k = k0; k < kMax; k++)
                    {
                        float aik = A[i * K + k];
                        var vaik = new Vector<float>(aik);
                        int bRow = k * N;
                        
                        int j = j0;
                        for (; j <= jMax - vectorSize; j += vectorSize)
                        {
                            var vb = new Vector<float>(B.Slice(bRow + j));
                            var vc = new Vector<float>(C.Slice(cRow + j));
                            (vc + vaik * vb).CopyTo(C.Slice(cRow + j));
                        }
                        
                        for (; j < jMax; j++)
                        {
                            C[cRow + j] += aik * B[bRow + j];
                        }
                    }
                }
            }
        }
    });
}
```

### 5. PARALLELIZE CORRECTLY

```csharp
// ❌ BAD: Parallel overhead for small work
Parallel.For(0, 10, i => { /* tiny work */ });

// ✅ GOOD: Only parallelize when beneficial
const int PARALLEL_THRESHOLD = 1000;

if (workSize >= PARALLEL_THRESHOLD)
{
    Parallel.For(0, workSize, new ParallelOptions 
    { 
        MaxDegreeOfParallelism = Environment.ProcessorCount 
    }, 
    i => { /* work */ });
}
else
{
    for (int i = 0; i < workSize; i++) { /* work */ }
}

// ✅ GOOD: Partition work to reduce overhead
int chunkSize = Math.Max(1, workSize / Environment.ProcessorCount);
Parallel.ForEach(
    Partitioner.Create(0, workSize, chunkSize),
    range =>
    {
        for (int i = range.Item1; i < range.Item2; i++)
        {
            // Process item i
        }
    });
```

---

## 🟡 Memory Optimization Patterns

### Tensor Memory Layout

```csharp
// ❌ BAD: Separate arrays for data and gradients
public class Tensor
{
    public float[] Data;      // Cache miss when accessing gradient
    public float[] Gradient;
}

// ✅ GOOD: Interleaved or contiguous layout
public class Tensor
{
    private float[] _buffer;  // [data..., gradient...]
    public Span<float> Data => _buffer.AsSpan(0, _size);
    public Span<float> Gradient => _buffer.AsSpan(_size, _size);
}

// ✅ BETTER: Use structs for small tensors
[StructLayout(LayoutKind.Sequential)]
public readonly struct SmallVector4
{
    public readonly float X, Y, Z, W;
}
```

### Object Pooling for Tensors

```csharp
public sealed class TensorPool
{
    private readonly ConcurrentBag<float[]>[] _pools;
    private static readonly int[] _sizes = { 64, 128, 256, 512, 1024, 2048, 4096 };
    
    public TensorPool()
    {
        _pools = new ConcurrentBag<float[]>[_sizes.Length];
        for (int i = 0; i < _pools.Length; i++)
            _pools[i] = new ConcurrentBag<float[]>();
    }
    
    public float[] Rent(int minSize)
    {
        int bucketIndex = GetBucketIndex(minSize);
        if (bucketIndex >= 0 && _pools[bucketIndex].TryTake(out var array))
            return array;
        
        int actualSize = bucketIndex >= 0 ? _sizes[bucketIndex] : minSize;
        return new float[actualSize];
    }
    
    public void Return(float[] array)
    {
        int bucketIndex = GetBucketIndex(array.Length);
        if (bucketIndex >= 0 && array.Length == _sizes[bucketIndex])
        {
            Array.Clear(array); // Optional: clear for security
            _pools[bucketIndex].Add(array);
        }
    }
    
    private int GetBucketIndex(int size)
    {
        // Find the smallest bucket that fits the requested size
        for (int i = 0; i < _sizes.Length; i++)
        {
            if (size <= _sizes[i])
                return i;
        }
        return -1; // Size too large for pooling
    }
}
```

### Avoid Boxing

```csharp
// ❌ BAD: Boxing in generic math
public T Sum<T>(T[] values) where T : struct
{
    object sum = default(T);  // Boxing!
    // ...
}

// ✅ GOOD: Use generic math interfaces (.NET 7+)
public T Sum<T>(ReadOnlySpan<T> values) where T : INumber<T>
{
    T sum = T.Zero;
    foreach (var v in values)
        sum += v;
    return sum;
}
```

---

## 🟢 Neural Network Specific Optimizations

### Softmax Optimization

```csharp
// ❌ BAD: Multiple passes, allocations
public float[] Softmax(float[] logits)
{
    float max = logits.Max();  // Pass 1
    float[] exp = logits.Select(x => MathF.Exp(x - max)).ToArray();  // Allocation + Pass 2
    float sum = exp.Sum();  // Pass 3
    return exp.Select(x => x / sum).ToArray();  // Allocation + Pass 4
}

// ✅ GOOD: Single allocation, fused passes
public void SoftmaxInPlace(Span<float> logits)
{
    // Pass 1: Find max
    float max = float.NegativeInfinity;
    for (int i = 0; i < logits.Length; i++)
        if (logits[i] > max) max = logits[i];
    
    // Pass 2: Exp and sum (fused)
    float sum = 0f;
    for (int i = 0; i < logits.Length; i++)
    {
        logits[i] = MathF.Exp(logits[i] - max);
        sum += logits[i];
    }
    
    // Pass 3: Normalize
    float invSum = 1f / sum;
    for (int i = 0; i < logits.Length; i++)
        logits[i] *= invSum;
}

// ✅ BEST: SIMD softmax
public void SoftmaxSIMD(Span<float> logits)
{
    int vectorSize = Vector<float>.Count;
    int length = logits.Length;
    
    // Find max (SIMD)
    var maxVec = new Vector<float>(float.NegativeInfinity);
    int i = 0;
    for (; i <= length - vectorSize; i += vectorSize)
        maxVec = Vector.Max(maxVec, new Vector<float>(logits.Slice(i)));
    
    float max = float.NegativeInfinity;
    for (int j = 0; j < vectorSize; j++)
        if (maxVec[j] > max) max = maxVec[j];
    for (; i < length; i++)
        if (logits[i] > max) max = logits[i];
    
    // Exp and sum (scalar - exp doesn't have SIMD intrinsic)
    float sum = 0f;
    for (i = 0; i < length; i++)
    {
        logits[i] = MathF.Exp(logits[i] - max);
        sum += logits[i];
    }
    
    // Normalize (SIMD)
    float invSum = 1f / sum;
    var invSumVec = new Vector<float>(invSum);
    for (i = 0; i <= length - vectorSize; i += vectorSize)
    {
        var v = new Vector<float>(logits.Slice(i));
        (v * invSumVec).CopyTo(logits.Slice(i));
    }
    for (; i < length; i++)
        logits[i] *= invSum;
}
```

### GELU Activation Optimization

```csharp
// ❌ BAD: Exact GELU with expensive operations
public float GELU(float x)
{
    return 0.5f * x * (1f + MathF.Tanh(
        MathF.Sqrt(2f / MathF.PI) * (x + 0.044715f * x * x * x)));
}

// ✅ GOOD: Approximate GELU (faster, used by GPT-2)
public float GELUFast(float x)
{
    // Sigmoid approximation: much faster than tanh version
    return x * Sigmoid(1.702f * x);
}

[MethodImpl(MethodImplOptions.AggressiveInlining)]
private static float Sigmoid(float x)
{
    return 1f / (1f + MathF.Exp(-x));
}

// ✅ BEST: SIMD GELU with lookup table for exp approximation
private static readonly float[] _expLookup = BuildExpLookup();

public void GELUBatch(Span<float> x)
{
    for (int i = 0; i < x.Length; i++)
    {
        float v = x[i];
        x[i] = v * SigmoidFast(1.702f * v);
    }
}

[MethodImpl(MethodImplOptions.AggressiveInlining)]
private static float SigmoidFast(float x)
{
    // Clamp to avoid overflow
    x = Math.Clamp(x, -20f, 20f);
    return 1f / (1f + ExpFast(-x));
}
```

### LayerNorm Optimization

```csharp
// ❌ BAD: Multiple passes with LINQ
public float[] LayerNorm(float[] x, float[] gamma, float[] beta)
{
    float mean = x.Average();
    float variance = x.Select(v => (v - mean) * (v - mean)).Average();
    float std = MathF.Sqrt(variance + 1e-5f);
    return x.Select((v, i) => gamma[i] * (v - mean) / std + beta[i]).ToArray();
}

// ✅ GOOD: Fused single-pass mean/variance
public void LayerNormInPlace(Span<float> x, ReadOnlySpan<float> gamma, 
                             ReadOnlySpan<float> beta, float eps = 1e-5f)
{
    int n = x.Length;
    
    // Single-pass mean and variance (Welford's algorithm)
    float mean = 0f;
    float m2 = 0f;
    for (int i = 0; i < n; i++)
    {
        float delta = x[i] - mean;
        mean += delta / (i + 1);
        m2 += delta * (x[i] - mean);
    }
    float variance = m2 / n;
    float invStd = 1f / MathF.Sqrt(variance + eps);
    
    // Normalize and scale
    for (int i = 0; i < n; i++)
    {
        x[i] = gamma[i] * (x[i] - mean) * invStd + beta[i];
    }
}
```

### Attention Score Optimization

```csharp
// ❌ BAD: Full materialization of attention matrix
public float[,] ComputeAttention(float[,] Q, float[,] K, float[,] V)
{
    int seqLen = Q.GetLength(0);
    int headDim = Q.GetLength(1);
    
    // Full attention matrix: O(seqLen²) memory!
    float[,] scores = new float[seqLen, seqLen];
    // ...
}

// ✅ GOOD: Block-wise attention for long sequences
public void ComputeAttentionBlocked(
    ReadOnlySpan<float> Q, ReadOnlySpan<float> K, ReadOnlySpan<float> V,
    Span<float> output, int seqLen, int headDim, int blockSize = 64)
{
    float scale = 1f / MathF.Sqrt(headDim);
    float[] scoreBlock = ArrayPool<float>.Shared.Rent(blockSize * blockSize);
    
    try
    {
        for (int qBlock = 0; qBlock < seqLen; qBlock += blockSize)
        {
            int qEnd = Math.Min(qBlock + blockSize, seqLen);
            
            for (int kBlock = 0; kBlock <= qBlock; kBlock += blockSize) // Causal: kBlock <= qBlock
            {
                int kEnd = Math.Min(kBlock + blockSize, seqLen);
                
                // Compute block scores
                ComputeScoreBlock(Q, K, scoreBlock, 
                    qBlock, qEnd, kBlock, kEnd, headDim, scale);
                
                // Apply causal mask within block
                ApplyCausalMask(scoreBlock, qBlock, kBlock, qEnd - qBlock, kEnd - kBlock);
                
                // Softmax per query row (numerically stable)
                // ... (incremental softmax for blocked attention)
            }
        }
    }
    finally
    {
        ArrayPool<float>.Shared.Return(scoreBlock);
    }
}
```

### KV-Cache for Inference

```csharp
public sealed class KVCache
{
    private readonly float[][] _keyCache;    // [layer][seqLen * headDim * numHeads]
    private readonly float[][] _valueCache;
    private int _currentLength;
    private readonly int _maxLength;
    private readonly int _numHeads;
    private readonly int _headDim;
    
    public KVCache(int numLayers, int maxSeqLen, int numHeads, int headDim)
    {
        _maxLength = maxSeqLen;
        _numHeads = numHeads;
        _headDim = headDim;
        int cacheSize = maxSeqLen * numHeads * headDim;
        
        _keyCache = new float[numLayers][];
        _valueCache = new float[numLayers][];
        
        for (int i = 0; i < numLayers; i++)
        {
            _keyCache[i] = new float[cacheSize];
            _valueCache[i] = new float[cacheSize];
        }
    }
    
    public void AppendKV(int layer, ReadOnlySpan<float> key, ReadOnlySpan<float> value)
    {
        int stride = _numHeads * _headDim;
        int offset = _currentLength * stride;
        key.CopyTo(_keyCache[layer].AsSpan(offset));
        value.CopyTo(_valueCache[layer].AsSpan(offset));
    }
    
    public ReadOnlySpan<float> GetKeys(int layer)
    {
        int stride = _numHeads * _headDim;
        return _keyCache[layer].AsSpan(0, (_currentLength + 1) * stride);
    }
    
    public ReadOnlySpan<float> GetValues(int layer)
    {
        int stride = _numHeads * _headDim;
        return _valueCache[layer].AsSpan(0, (_currentLength + 1) * stride);
    }
}
```

---

## 🔵 Training Specific Optimizations

### Gradient Accumulation Memory

```csharp
// ❌ BAD: Storing full gradient history
public class Parameter
{
    public float[] Weights;
    public float[] Gradients;
    public List<float[]> GradientHistory;  // Memory explosion!
}

// ✅ GOOD: Accumulate in-place
public class Parameter
{
    public float[] Weights;
    public float[] Gradients;  // Accumulated gradients
    public float[] M;          // Adam first moment
    public float[] V;          // Adam second moment
    
    public void AccumulateGradient(ReadOnlySpan<float> grad)
    {
        for (int i = 0; i < Gradients.Length; i++)
            Gradients[i] += grad[i];
    }
    
    public void ZeroGradients()
    {
        Array.Clear(Gradients);
    }
}
```

### Batch Processing

```csharp
// ❌ BAD: Process one sample at a time
foreach (var sample in batch)
{
    Forward(sample);
    Backward(sample);
}

// ✅ GOOD: Batch forward/backward with parallelism
public void ForwardBatch(ReadOnlySpan<float> batchedInput, Span<float> batchedOutput,
                         int batchSize, int inputDim, int outputDim)
{
    Parallel.For(0, batchSize, new ParallelOptions 
    { 
        MaxDegreeOfParallelism = Environment.ProcessorCount 
    },
    b =>
    {
        var input = batchedInput.Slice(b * inputDim, inputDim);
        var output = batchedOutput.Slice(b * outputDim, outputDim);
        ForwardSingle(input, output);
    });
}
```

---

## 🟣 Profiling and Debugging

### Add Timing Instrumentation

```csharp
public sealed class PerformanceTracker
{
    private readonly Dictionary<string, (long totalTicks, int count)> _timings = new();
    private readonly Stopwatch _stopwatch = new();
    
    public IDisposable Track(string operation)
    {
        return new TimingScope(this, operation);
    }
    
    private sealed class TimingScope : IDisposable
    {
        private readonly PerformanceTracker _tracker;
        private readonly string _operation;
        private readonly long _startTicks;
        
        public TimingScope(PerformanceTracker tracker, string operation)
        {
            _tracker = tracker;
            _operation = operation;
            _startTicks = Stopwatch.GetTimestamp();
        }
        
        public void Dispose()
        {
            long elapsed = Stopwatch.GetTimestamp() - _startTicks;
            lock (_tracker._timings)
            {
                if (_tracker._timings.TryGetValue(_operation, out var existing))
                    _tracker._timings[_operation] = (existing.totalTicks + elapsed, existing.count + 1);
                else
                    _tracker._timings[_operation] = (elapsed, 1);
            }
        }
    }
    
    public void PrintReport()
    {
        Console.WriteLine("\n=== Performance Report ===");
        foreach (var (op, (ticks, count)) in _timings.OrderByDescending(x => x.Value.totalTicks))
        {
            double ms = ticks * 1000.0 / Stopwatch.Frequency;
            Console.WriteLine($"{op}: {ms:F2}ms total, {ms/count:F3}ms avg, {count} calls");
        }
    }
}

// Usage:
using (tracker.Track("MatMul"))
{
    MatrixMultiply(A, B, C);
}
```

### Memory Profiling Helpers

```csharp
public static class MemoryDiagnostics
{
    public static void PrintMemoryUsage(string label)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        long managed = GC.GetTotalMemory(true);
        using var proc = Process.GetCurrentProcess();
        long working = proc.WorkingSet64;
        
        Console.WriteLine($"[{label}] Managed: {managed / 1024 / 1024}MB, Working Set: {working / 1024 / 1024}MB");
    }
    
    public static void PrintGCStats()
    {
        Console.WriteLine($"GC Gen0: {GC.CollectionCount(0)}, Gen1: {GC.CollectionCount(1)}, Gen2: {GC.CollectionCount(2)}");
    }
}
```

---

## ⚡ Quick Reference: Performance Checklist

When writing or reviewing code for SmallMind:

- [ ] **Allocations**: Does this hot path allocate? Can I use `Span<T>`, `ArrayPool`, or pre-allocated buffers?
- [ ] **SIMD**: Is this a vectorizable operation? Use `Vector<T>` or `System.Runtime.Intrinsics`
- [ ] **Parallelism**: Is the work large enough to parallelize? Use threshold checks
- [ ] **Cache Locality**: Is the memory access pattern cache-friendly? Consider blocking/tiling
- [ ] **Math Functions**: Am I using `MathF` (float) instead of `Math` (double)?
- [ ] **Branching**: Can I eliminate branches in inner loops with branchless techniques?
- [ ] **Inlining**: Are small, hot methods marked with `[MethodImpl(MethodImplOptions.AggressiveInlining)]`?

---

## 📚 Additional Resources

- [.NET Performance Best Practices](https://learn.microsoft.com/en-us/dotnet/standard/performance/)
- [High-Performance .NET Code](https://adamsitnik.com/Hardware-Counters-Diagnoser/)
- [SIMD in .NET](https://devblogs.microsoft.com/dotnet/hardware-intrinsics-in-net-core/)
- [ArrayPool Documentation](https://learn.microsoft.com/en-us/dotnet/api/system.buffers.arraypool-1)
