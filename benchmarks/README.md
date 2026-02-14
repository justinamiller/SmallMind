# SmallMind Benchmarks

Comprehensive performance benchmarking suite for SmallMind - a pure C# LLM implementation.

## Overview

This directory contains all benchmark projects for SmallMind, organized by category to avoid duplication and provide comprehensive coverage of all performance-critical areas. Previously scattered across `bench/`, `src/`, and `examples/benchmarks/`, all benchmark projects are now consolidated here for easier navigation and maintenance.

## 📁 Benchmark Categories

### 🔧 Core (`Core/`)
Shared infrastructure and utilities used by all benchmark projects.

### 🎯 Model Inference (`ModelInference/`)
Real-model inference benchmarks with actual model files - token generation, TTFT, memory tracking, thread scaling.

### ⚙️ Runtime Metrics (`RuntimeMetrics/`)
Comprehensive runtime/engine performance - single-stream, concurrent streams, memory growth, KV cache.

### 🧮 Kernel Benchmarks (`KernelBenchmarks/`)
Low-level computational kernels - GEMM, SIMD, MatMul, Q4 quantization, optimization tiers.

### 🚀 Inference Benchmarks (`InferenceBenchmarks/`)
End-to-end inference features - chat, sampling, Q4 profiling, standard LLM metrics.

### 💾 Memory Benchmarks (`MemoryBenchmarks/`)
Memory allocation and GC pressure - TensorPool, allocations, hot paths.

### 🎓 Training Benchmarks (`TrainingBenchmarks/`)
Training-specific performance - AdamW, gradient computation.

### 🔤 Tokenizer Benchmarks (`TokenizerBenchmarks/`)
Tokenizer performance - tokens/sec, allocations.

## 🗂️ Migration from Previous Structure

| Old Location | New Location |
|-------------|--------------|
| `bench/SmallMind.Benchmarks.Core` | `benchmarks/Core/` |
| `bench/SmallMind.Benchmarks` | `benchmarks/ModelInference/` |
| `benchmarks/SmallMind.Benchmarks` | `benchmarks/RuntimeMetrics/` |
| `src/SmallMind.Benchmarks` | `benchmarks/KernelBenchmarks/SmallMind.KernelBenchmarks` |
| `src/SmallMind.Benchmarks.CpuComparison` | `benchmarks/KernelBenchmarks/SmallMind.KernelBenchmarks` |
| `examples/benchmarks/*` | `benchmarks/*/` (categorized) |

Old directories preserved as `bench_old/`, `benchmarks_old2/`, `examples/benchmarks_old/`.

See detailed documentation in README_ModelInference.md and README_RuntimeMetrics.md.
