# Performance Profile Report

**Generated:** 2026-02-02 17:08:52
**Total Runtime:** 25453.06 ms
**Total Allocations:** 7579.78 MB
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
| 1 | `Inference` | 24831.871 | 97.56% | 3 | 8277.290 | 7767.128 | 9213.635 |
| 2 | `GenerateToken` | 24831.417 | 97.56% | 150 | 165.543 | 91.051 | 288.451 |
| 3 | `Transformer_Forward` | 24823.440 | 97.53% | 150 | 165.490 | 91.043 | 288.438 |
| 4 | `MatMul_Iteration` | 371.039 | 1.46% | 9 | 41.227 | 2.949 | 104.375 |
| 5 | `MatMul_512x512` | 290.342 | 1.14% | 1 | 290.342 | 290.342 | 290.342 |
| 6 | `Transformer_ModelCreation` | 232.431 | 0.91% | 1 | 232.431 | 232.433 | 232.433 |
| 7 | `MatMul_256x256` | 51.028 | 0.20% | 1 | 51.028 | 51.029 | 51.029 |
| 8 | `MatMul_128x128` | 31.090 | 0.12% | 1 | 31.090 | 31.090 | 31.090 |

## 💾 Top Allocators (by Memory)

Methods allocating the most memory:

| Rank | Method | Total Alloc (MB) | % of Total | Calls | Avg Alloc (KB) |
|------|--------|------------------|-----------|-------|----------------|
| 1 | `Inference` | 7549.349 | 99.60% | 3 | 2576844.458 |
| 2 | `GenerateToken` | 7549.349 | 99.60% | 150 | 51536.889 |
| 3 | `Transformer_Forward` | 7549.349 | 99.60% | 150 | 51536.889 |
| 4 | `Transformer_ModelCreation` | 26.401 | 0.35% | 1 | 27034.445 |
| 5 | `MatMul_Iteration` | 0.095 | 0.00% | 9 | 10.853 |
| 6 | `MatMul_128x128` | 0.080 | 0.00% | 1 | 81.664 |
| 7 | `MatMul_512x512` | 0.016 | 0.00% | 1 | 16.016 |
| 8 | `MatMul_256x256` | 0.000 | 0.00% | 1 | 0.000 |

## 📞 Most Called Methods

Methods called most frequently:

| Rank | Method | Calls | Total Time (ms) | Avg Time (μs) |
|------|--------|-------|----------------|---------------|
| 1 | `GenerateToken` | 150 | 24831.417 | 165542.779 |
| 2 | `Transformer_Forward` | 150 | 24823.440 | 165489.601 |
| 3 | `MatMul_Iteration` | 9 | 371.039 | 41226.514 |
| 4 | `Inference` | 3 | 24831.871 | 8277290.385 |
| 5 | `MatMul_128x128` | 1 | 31.090 | 31089.946 |
| 6 | `MatMul_256x256` | 1 | 51.028 | 51028.397 |
| 7 | `MatMul_512x512` | 1 | 290.342 | 290341.570 |
| 8 | `Transformer_ModelCreation` | 1 | 232.431 | 232431.184 |

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

### `MatMul_512x512`

*Entry point method*

## 💡 Performance Insights

- **Top 5 methods** include nested operations (some time is counted in multiple scopes)
- **4 methods** allocate more than 1 MB per call on average

