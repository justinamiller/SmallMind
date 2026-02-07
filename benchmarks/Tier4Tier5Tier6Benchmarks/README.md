# Tier 4-6 Performance Benchmarks

This benchmark suite measures the performance improvements from implementing Tier 4-6 optimizations in SmallMind.

## Optimizations Measured

### Tier 4: Generation Loop Optimizations
- **Tensor Buffer Reuse**: Eliminates per-token allocations in `Generate()` and `GenerateWithCache()` by pre-allocating and reusing buffers
- **FastExp in Softmax**: Replaces `MathF.Exp` with `MathUtils.FastExp` (3-5x faster) while maintaining numerical accuracy

### Tier 5: SIMD Micro-Optimizations  
- **SkipLocalsInit**: Applied to hot methods in `LayerNormOps`, `MatMulOps`, and `ActivationOps` to avoid zero-initialization overhead
- **K-Loop Unrolling**: 2x loop unrolling in `MatMulTransposeBRowAvx2` to process 16 floats per iteration

### Tier 6: Architecture-Level Optimizations
- **Fused Residual+LayerNorm**: Combines residual addition and layer normalization into a single pass, eliminating redundant memory operations

## Running the Benchmarks

```bash
dotnet run -c Release
```

## Expected Results

- **FastExp Speedup**: 1.6-1.7x faster than MathF.Exp with KL divergence < 2.0
- **Fused ResidualLayerNorm**: 1.3-1.6x speedup for medium-large batch sizes
- **K-Loop Unrolling**: Improved GFLOPS, especially for headSize=128

## Benchmark Details

All benchmarks use:
- BCL only (no BenchmarkDotNet)
- Warmup + measured iterations
- Allocation tracking via `GC.GetAllocatedBytesForCurrentThread()`
- GC collection count monitoring
- Accuracy/correctness validation
