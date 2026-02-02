# SmallMind Benchmark Results Summary

## Overview
Successfully executed SmallMind.Benchmarks tool against a minimal .smq model (124KB) to obtain baseline performance metrics for the SmallMind LLM inference engine.

## Model Details
- **Format**: .smq (SmallMind Quantized)
- **Size**: 124 KB
- **Architecture**: Decoder-only Transformer
- **Configuration**:
  - Vocabulary Size: 50 tokens
  - Context Length: 32 tokens  
  - Embedding Dimension: 64
  - Layers: 2
  - Attention Heads: 4
  - Quantization: Q8_0 (8-bit)

## Key Performance Metrics

### Time To First Token (TTFT)
- **P50 (Median)**: 2.79 ms
- **P95**: 4.24 ms
- **Mean**: 3.03 ms

### Throughput
- **Steady Tokens/Sec (P50)**: 678.24 tokens/sec
- **Steady Tokens/Sec (Mean)**: 658.99 tokens/sec
- **Overall Tokens/Sec (P50)**: 676.41 tokens/sec

### End-to-End Latency
- **P50 (Median)**: 74.12 ms (for 50 tokens)
- **P95**: 78.60 ms
- **Mean**: 74.48 ms

### Memory Footprint
- **Working Set (Avg)**: 69.43 MB
- **Private Memory (Avg)**: 274.29 MB
- **Managed Heap (Avg)**: 10.83 MB

### GC and Allocations (per 50-token generation)
- **Gen0 Collections**: 83
- **Total Allocated**: 1330.21 MB
- **Allocations per Operation**: 133.02 MB

### System Utilization
- **CPU Usage (Avg)**: 70-71%
- **Allocation Rate**: 1500-1800 MB/sec
- **Time in GC**: 2-4%

## Test Environment

### Hardware
- **OS**: Ubuntu 24.04.3 LTS
- **Architecture**: X64
- **CPU Cores**: 4

### Software
- **.NET Version**: 10.0.2
- **Build Configuration**: Release
- **SmallMind Engine Threads**: 4

## Benchmark Configuration
- **Scenarios**: All (TTFT, Tokens/Sec, Latency, Memory, GC, Concurrency)
- **Iterations**: 10 per scenario
- **Warmup Iterations**: 2
- **Tokens Generated**: 50 per run
- **Temperature**: 1.0
- **Top-K**: 1
- **Seed**: 42 (for reproducibility)

## Comparison Context

These metrics are for a **minimal untrained model** (124KB) with very small dimensions:
- **Tiny vocabulary** (50 tokens vs. typical 50K-100K)
- **Tiny context** (32 tokens vs. typical 2K-8K)  
- **Tiny embedding** (64 dim vs. typical 768-4096)
- **Minimal layers** (2 vs. typical 12-96)

For production-scale models (billions of parameters), expect:
- TTFT: 10-100x higher
- Throughput: 10-100x lower  
- Memory: 100-1000x higher
- Latency proportional to model size and token count

## Key Observations

1. **Fast Inference**: The tiny model achieves ~660 tokens/sec, demonstrating SmallMind's CPU-based inference is viable for small models
2. **Low TTFT**: <3ms median time to first token shows minimal initialization overhead
3. **Memory Efficient**: ~70MB working set for model + inference is reasonable for a tiny model
4. **High Allocation Rate**: 1.5-1.8 GB/sec allocation rate indicates opportunities for memory optimization
5. **GC Pressure**: 83 Gen0 collections and 133MB allocations per 50-token generation suggests room for pooling/reuse

## Files Generated
- `benchmark-model.smq` - Quantized model file (124KB)
- `benchmark-report.md` - Full detailed benchmark report  
- `benchmarks/results/*/results.json` - Machine-readable benchmark data

## Next Steps for Performance

Based on these results, potential optimizations include:
1. Implement tensor pooling to reduce GC pressure
2. Optimize memory allocations in hot paths (inference loop)
3. Add KV-cache reuse for repeated inference
4. Profile SIMD vectorization coverage
5. Benchmark with larger, trained models to identify scaling bottlenecks

