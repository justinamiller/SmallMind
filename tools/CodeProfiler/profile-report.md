# Performance Profile Report

**Generated:** 2026-02-03 00:19:25
**Total Runtime:** 7879.27 ms
**Total Allocations:** 7579.28 MB
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
| 1 | `Inference` | 7617.359 | 96.68% | 3 | 2539.120 | 2046.551 | 3470.340 |
| 2 | `GenerateToken` | 7617.020 | 96.67% | 150 | 50.780 | 26.530 | 159.045 |
| 3 | `Transformer_Forward` | 7613.678 | 96.63% | 150 | 50.758 | 26.527 | 156.902 |
| 4 | `MatMul_Iteration` | 137.714 | 1.75% | 9 | 15.302 | 2.403 | 35.520 |
| 5 | `MatMul_512x512` | 86.950 | 1.10% | 1 | 86.950 | 86.950 | 86.950 |
| 6 | `Transformer_ModelCreation` | 84.443 | 1.07% | 1 | 84.443 | 84.445 | 84.445 |
| 7 | `MatMul_128x128` | 29.817 | 0.38% | 1 | 29.817 | 29.818 | 29.817 |
| 8 | `MatMul_256x256` | 22.637 | 0.29% | 1 | 22.637 | 22.637 | 22.637 |

## 💾 Top Allocators (by Memory)

Methods allocating the most memory:

| Rank | Method | Total Alloc (MB) | % of Total | Calls | Avg Alloc (KB) |
|------|--------|------------------|-----------|-------|----------------|
| 1 | `Inference` | 7548.852 | 99.60% | 3 | 2576674.732 |
| 2 | `GenerateToken` | 7548.852 | 99.60% | 150 | 51533.495 |
| 3 | `Transformer_Forward` | 7548.852 | 99.60% | 150 | 51533.495 |
| 4 | `Transformer_ModelCreation` | 26.406 | 0.35% | 1 | 27040.156 |
| 5 | `MatMul_Iteration` | 0.088 | 0.00% | 9 | 10.000 |
| 6 | `MatMul_128x128` | 0.080 | 0.00% | 1 | 81.992 |
| 7 | `MatMul_512x512` | 0.008 | 0.00% | 1 | 8.008 |
| 8 | `MatMul_256x256` | 0.000 | 0.00% | 1 | 0.000 |

## 📞 Most Called Methods

Methods called most frequently:

| Rank | Method | Calls | Total Time (ms) | Avg Time (μs) |
|------|--------|-------|----------------|---------------|
| 1 | `GenerateToken` | 150 | 7617.020 | 50780.136 |
| 2 | `Transformer_Forward` | 150 | 7613.678 | 50757.857 |
| 3 | `MatMul_Iteration` | 9 | 137.714 | 15301.567 |
| 4 | `Inference` | 3 | 7617.359 | 2539119.575 |
| 5 | `MatMul_128x128` | 1 | 29.817 | 29817.368 |
| 6 | `MatMul_256x256` | 1 | 22.637 | 22637.025 |
| 7 | `MatMul_512x512` | 1 | 86.950 | 86950.141 |
| 8 | `Transformer_ModelCreation` | 1 | 84.443 | 84443.304 |

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

