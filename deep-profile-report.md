# Performance Profile Report

**Generated:** 2026-02-02 15:29:11
**Total Runtime:** 24160.28 ms
**Total Allocations:** 7580.07 MB
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
| 1 | `Inference` | 23562.992 | 97.53% | 3 | 7854.331 | 7363.992 | 8771.153 |
| 2 | `GenerateToken` | 23562.586 | 97.53% | 150 | 157.084 | 85.106 | 262.188 |
| 3 | `Transformer_Forward` | 23559.011 | 97.51% | 150 | 157.060 | 85.096 | 262.150 |
| 4 | `MatMul_Iteration` | 363.720 | 1.51% | 9 | 40.413 | 1.635 | 123.604 |
| 5 | `MatMul_512x512` | 299.608 | 1.24% | 1 | 299.608 | 299.609 | 299.609 |
| 6 | `Transformer_ModelCreation` | 212.644 | 0.88% | 1 | 212.644 | 212.646 | 212.646 |
| 7 | `MatMul_256x256` | 36.007 | 0.15% | 1 | 36.007 | 36.007 | 36.007 |
| 8 | `MatMul_128x128` | 29.516 | 0.12% | 1 | 29.516 | 29.516 | 29.516 |

## 💾 Top Allocators (by Memory)

Methods allocating the most memory:

| Rank | Method | Total Alloc (MB) | % of Total | Calls | Avg Alloc (KB) |
|------|--------|------------------|-----------|-------|----------------|
| 1 | `Inference` | 7549.632 | 99.60% | 3 | 2576940.888 |
| 2 | `GenerateToken` | 7549.632 | 99.60% | 150 | 51538.818 |
| 3 | `Transformer_Forward` | 7549.632 | 99.60% | 150 | 51538.818 |
| 4 | `Transformer_ModelCreation` | 26.401 | 0.35% | 1 | 27034.508 |
| 5 | `MatMul_Iteration` | 0.095 | 0.00% | 9 | 10.852 |
| 6 | `MatMul_128x128` | 0.080 | 0.00% | 1 | 81.648 |
| 7 | `MatMul_512x512` | 0.016 | 0.00% | 1 | 16.016 |
| 8 | `MatMul_256x256` | 0.000 | 0.00% | 1 | 0.000 |

## 📞 Most Called Methods

Methods called most frequently:

| Rank | Method | Calls | Total Time (ms) | Avg Time (μs) |
|------|--------|-------|----------------|---------------|
| 1 | `GenerateToken` | 150 | 23562.586 | 157083.904 |
| 2 | `Transformer_Forward` | 150 | 23559.011 | 157060.070 |
| 3 | `MatMul_Iteration` | 9 | 363.720 | 40413.337 |
| 4 | `Inference` | 3 | 23562.992 | 7854330.608 |
| 5 | `MatMul_128x128` | 1 | 29.516 | 29515.830 |
| 6 | `MatMul_256x256` | 1 | 36.007 | 36007.014 |
| 7 | `MatMul_512x512` | 1 | 299.608 | 299608.375 |
| 8 | `Transformer_ModelCreation` | 1 | 212.644 | 212643.846 |

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

- **Top 5 hot paths** account for **295.3%** of total runtime
- **4 methods** allocate more than 1 MB per call on average

