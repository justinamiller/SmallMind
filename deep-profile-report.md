# Performance Profile Report

**Generated:** 2026-02-02 15:33:49
**Total Runtime:** 25766.11 ms
**Total Allocations:** 7579.92 MB
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
| 1 | `Inference` | 25161.172 | 97.65% | 3 | 8387.057 | 7909.923 | 9337.022 |
| 2 | `GenerateToken` | 25160.700 | 97.65% | 150 | 167.738 | 88.867 | 307.574 |
| 3 | `Transformer_Forward` | 25154.383 | 97.63% | 150 | 167.696 | 88.851 | 307.538 |
| 4 | `MatMul_Iteration` | 365.097 | 1.42% | 9 | 40.566 | 1.619 | 107.425 |
| 5 | `MatMul_512x512` | 296.630 | 1.15% | 1 | 296.630 | 296.630 | 296.630 |
| 6 | `Transformer_ModelCreation` | 221.630 | 0.86% | 1 | 221.630 | 221.632 | 221.632 |
| 7 | `MatMul_256x256` | 40.860 | 0.16% | 1 | 40.860 | 40.861 | 40.861 |
| 8 | `MatMul_128x128` | 29.115 | 0.11% | 1 | 29.115 | 29.116 | 29.116 |

## 💾 Top Allocators (by Memory)

Methods allocating the most memory:

| Rank | Method | Total Alloc (MB) | % of Total | Calls | Avg Alloc (KB) |
|------|--------|------------------|-----------|-------|----------------|
| 1 | `Inference` | 7549.488 | 99.60% | 3 | 2576891.802 |
| 2 | `GenerateToken` | 7549.488 | 99.60% | 150 | 51537.836 |
| 3 | `Transformer_Forward` | 7549.488 | 99.60% | 150 | 51537.836 |
| 4 | `Transformer_ModelCreation` | 26.402 | 0.35% | 1 | 27035.336 |
| 5 | `MatMul_Iteration` | 0.095 | 0.00% | 9 | 10.851 |
| 6 | `MatMul_128x128` | 0.080 | 0.00% | 1 | 81.641 |
| 7 | `MatMul_512x512` | 0.016 | 0.00% | 1 | 16.016 |
| 8 | `MatMul_256x256` | 0.000 | 0.00% | 1 | 0.000 |

## 📞 Most Called Methods

Methods called most frequently:

| Rank | Method | Calls | Total Time (ms) | Avg Time (μs) |
|------|--------|-------|----------------|---------------|
| 1 | `GenerateToken` | 150 | 25160.700 | 167738.001 |
| 2 | `Transformer_Forward` | 150 | 25154.383 | 167695.888 |
| 3 | `MatMul_Iteration` | 9 | 365.097 | 40566.319 |
| 4 | `Inference` | 3 | 25161.172 | 8387057.275 |
| 5 | `MatMul_128x128` | 1 | 29.115 | 29115.400 |
| 6 | `MatMul_256x256` | 1 | 40.860 | 40860.389 |
| 7 | `MatMul_512x512` | 1 | 296.630 | 296629.747 |
| 8 | `Transformer_ModelCreation` | 1 | 221.630 | 221630.085 |

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

