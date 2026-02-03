# Performance Profile Report

**Generated:** 2026-02-03 03:00:08
**Total Runtime:** 7468.95 ms
**Total Allocations:** 7065.80 MB
**Methods Profiled:** 8

## System Information

- **OS:** Ubuntu 24.04.3 LTS
- **Architecture:** X64
- **CPU Cores:** 4
- **.NET Version:** 10.0.2
- **GC Mode:** Workstation

## 🔥 Hot Paths (by Time)

Methods consuming the most CPU time:

| Rank | Method | Total Time (ms) | % of Total | Calls | Avg Time (ms) | Min (ms) | Max (ms) |
|------|--------|----------------|-----------|-------|---------------|----------|----------|
| 1 | `Inference` | 7208.594 | 96.51% | 3 | 2402.865 | 1805.931 | 3381.563 |
| 2 | `GenerateToken` | 7208.258 | 96.51% | 150 | 48.055 | 24.343 | 156.628 |
| 3 | `Transformer_Forward` | 7205.062 | 96.47% | 150 | 48.034 | 24.341 | 154.821 |
| 4 | `MatMul_Iteration` | 131.371 | 1.76% | 9 | 14.597 | 2.258 | 32.092 |
| 5 | `Transformer_ModelCreation` | 91.810 | 1.23% | 1 | 91.810 | 91.812 | 91.812 |
| 6 | `MatMul_512x512` | 83.531 | 1.12% | 1 | 83.531 | 83.531 | 83.531 |
| 7 | `MatMul_128x128` | 27.568 | 0.37% | 1 | 27.568 | 27.568 | 27.568 |
| 8 | `MatMul_256x256` | 21.751 | 0.29% | 1 | 21.751 | 21.751 | 21.751 |

## 💾 Top Allocators (by Memory)

Methods allocating the most memory:

| Rank | Method | Total Alloc (MB) | % of Total | Calls | Avg Alloc (KB) |
|------|--------|------------------|-----------|-------|----------------|
| 1 | `Inference` | 7035.370 | 99.57% | 3 | 2401406.318 |
| 2 | `GenerateToken` | 7035.370 | 99.57% | 150 | 48028.126 |
| 3 | `Transformer_Forward` | 7035.352 | 99.57% | 150 | 48028.003 |
| 4 | `Transformer_ModelCreation` | 26.400 | 0.37% | 1 | 27033.758 |
| 5 | `MatMul_Iteration` | 0.095 | 0.00% | 9 | 10.863 |
| 6 | `MatMul_128x128` | 0.072 | 0.00% | 1 | 73.914 |
| 7 | `MatMul_512x512` | 0.015 | 0.00% | 1 | 15.844 |
| 8 | `MatMul_256x256` | 0.008 | 0.00% | 1 | 8.008 |

## 📞 Most Called Methods

Methods called most frequently:

| Rank | Method | Calls | Total Time (ms) | Avg Time (μs) |
|------|--------|-------|----------------|---------------|
| 1 | `GenerateToken` | 150 | 7208.258 | 48055.052 |
| 2 | `Transformer_Forward` | 150 | 7205.062 | 48033.747 |
| 3 | `MatMul_Iteration` | 9 | 131.371 | 14596.823 |
| 4 | `Inference` | 3 | 7208.594 | 2402864.547 |
| 5 | `MatMul_128x128` | 1 | 27.568 | 27568.004 |
| 6 | `MatMul_256x256` | 1 | 21.751 | 21751.290 |
| 7 | `MatMul_512x512` | 1 | 83.531 | 83530.992 |
| 8 | `Transformer_ModelCreation` | 1 | 91.810 | 91810.414 |

## 🌲 Call Hierarchy

Parent-child relationships for hot paths:

### `Inference`

*Entry point method*

### `GenerateToken`

**Called by:**
- `Inference` (150 times)

### `Transformer_Forward`

**Called by:**
- `GenerateToken` (150 times)

### `MatMul_Iteration`

**Called by:**
- `MatMul_128x128` (3 times)
- `MatMul_256x256` (3 times)
- `MatMul_512x512` (3 times)

### `Transformer_ModelCreation`

*Entry point method*

## 💡 Performance Insights

- **Top 5 methods** include nested operations (some time is counted in multiple scopes)
- **4 methods** allocate more than 1 MB per call on average

