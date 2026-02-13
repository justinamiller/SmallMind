# Performance Profile Report

**Generated:** 2026-02-13 16:28:36
**Total Runtime:** 6253.76 ms
**Total Allocations:** 6189.45 MB
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
| 1 | `Inference` | 6036.566 | 96.53% | 3 | 2012.189 | 1679.231 | 2653.076 |
| 2 | `GenerateToken` | 6036.176 | 96.52% | 150 | 40.241 | 27.102 | 145.304 |
| 3 | `Transformer_Forward` | 6031.619 | 96.45% | 150 | 40.211 | 27.098 | 142.033 |
| 4 | `Transformer_ModelCreation` | 122.329 | 1.96% | 1 | 122.329 | 122.331 | 122.331 |
| 5 | `MatMul_Iteration` | 47.923 | 0.77% | 9 | 5.325 | 0.121 | 13.988 |
| 6 | `MatMul_512x512` | 34.407 | 0.55% | 1 | 34.407 | 34.407 | 34.407 |
| 7 | `MatMul_128x128` | 9.881 | 0.16% | 1 | 9.881 | 9.881 | 9.881 |
| 8 | `MatMul_256x256` | 5.383 | 0.09% | 1 | 5.383 | 5.384 | 5.384 |

## 💾 Top Allocators (by Memory)

Methods allocating the most memory:

| Rank | Method | Total Alloc (MB) | % of Total | Calls | Avg Alloc (KB) |
|------|--------|------------------|-----------|-------|----------------|
| 1 | `Inference` | 6159.051 | 99.51% | 3 | 2102289.495 |
| 2 | `GenerateToken` | 6159.051 | 99.51% | 150 | 42045.790 |
| 3 | `Transformer_Forward` | 6158.927 | 99.51% | 150 | 42044.939 |
| 4 | `Transformer_ModelCreation` | 26.453 | 0.43% | 1 | 27088.117 |
| 5 | `MatMul_128x128` | 0.008 | 0.00% | 1 | 8.008 |
| 6 | `MatMul_Iteration` | 0.000 | 0.00% | 9 | 0.000 |
| 7 | `MatMul_256x256` | 0.000 | 0.00% | 1 | 0.000 |
| 8 | `MatMul_512x512` | 0.000 | 0.00% | 1 | 0.000 |

## 📞 Most Called Methods

Methods called most frequently:

| Rank | Method | Calls | Total Time (ms) | Avg Time (μs) |
|------|--------|-------|----------------|---------------|
| 1 | `GenerateToken` | 150 | 6036.176 | 40241.175 |
| 2 | `Transformer_Forward` | 150 | 6031.619 | 40210.791 |
| 3 | `MatMul_Iteration` | 9 | 47.923 | 5324.750 |
| 4 | `Inference` | 3 | 6036.566 | 2012188.637 |
| 5 | `MatMul_128x128` | 1 | 9.881 | 9880.721 |
| 6 | `MatMul_256x256` | 1 | 5.383 | 5383.386 |
| 7 | `MatMul_512x512` | 1 | 34.407 | 34406.895 |
| 8 | `Transformer_ModelCreation` | 1 | 122.329 | 122329.382 |

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

### `Transformer_ModelCreation`

*Entry point method*

### `MatMul_Iteration`

**Called by:**
- `MatMul_128x128` (3 times)
- `MatMul_256x256` (3 times)
- `MatMul_512x512` (3 times)

## 💡 Performance Insights

- **Top 5 methods** include nested operations (some time is counted in multiple scopes)
- **4 methods** allocate more than 1 MB per call on average

