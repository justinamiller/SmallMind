# Performance Profile Report

**Generated:** 2026-02-03 03:18:44
**Total Runtime:** 6811.22 ms
**Total Allocations:** 7074.44 MB
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
| 1 | `Inference` | 6611.783 | 97.07% | 3 | 2203.928 | 1832.722 | 2859.068 |
| 2 | `GenerateToken` | 6611.450 | 97.07% | 150 | 44.076 | 26.489 | 87.546 |
| 3 | `Transformer_Forward` | 6607.784 | 97.01% | 150 | 44.052 | 26.485 | 85.332 |
| 4 | `MatMul_Iteration` | 110.548 | 1.62% | 9 | 12.283 | 1.486 | 26.774 |
| 5 | `MatMul_512x512` | 71.644 | 1.05% | 1 | 71.644 | 71.644 | 71.644 |
| 6 | `Transformer_ModelCreation` | 51.370 | 0.75% | 1 | 51.370 | 51.371 | 51.371 |
| 7 | `MatMul_128x128` | 25.235 | 0.37% | 1 | 25.235 | 25.235 | 25.235 |
| 8 | `MatMul_256x256` | 15.202 | 0.22% | 1 | 15.202 | 15.202 | 15.202 |

## 💾 Top Allocators (by Memory)

Methods allocating the most memory:

| Rank | Method | Total Alloc (MB) | % of Total | Calls | Avg Alloc (KB) |
|------|--------|------------------|-----------|-------|----------------|
| 1 | `Inference` | 7044.009 | 99.57% | 3 | 2404355.122 |
| 2 | `GenerateToken` | 7044.009 | 99.57% | 150 | 48087.102 |
| 3 | `Transformer_Forward` | 7043.994 | 99.57% | 150 | 48086.998 |
| 4 | `Transformer_ModelCreation` | 26.400 | 0.37% | 1 | 27033.984 |
| 5 | `MatMul_Iteration` | 0.096 | 0.00% | 9 | 10.886 |
| 6 | `MatMul_128x128` | 0.080 | 0.00% | 1 | 81.961 |
| 7 | `MatMul_512x512` | 0.016 | 0.00% | 1 | 16.016 |
| 8 | `MatMul_256x256` | 0.000 | 0.00% | 1 | 0.000 |

## 📞 Most Called Methods

Methods called most frequently:

| Rank | Method | Calls | Total Time (ms) | Avg Time (μs) |
|------|--------|-------|----------------|---------------|
| 1 | `GenerateToken` | 150 | 6611.450 | 44076.333 |
| 2 | `Transformer_Forward` | 150 | 6607.784 | 44051.893 |
| 3 | `MatMul_Iteration` | 9 | 110.548 | 12283.166 |
| 4 | `Inference` | 3 | 6611.783 | 2203927.764 |
| 5 | `MatMul_128x128` | 1 | 25.235 | 25234.871 |
| 6 | `MatMul_256x256` | 1 | 15.202 | 15201.694 |
| 7 | `MatMul_512x512` | 1 | 71.644 | 71643.857 |
| 8 | `Transformer_ModelCreation` | 1 | 51.370 | 51369.766 |

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

