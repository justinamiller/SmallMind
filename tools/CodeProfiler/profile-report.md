# Performance Profile Report

**Generated:** 2026-02-03 03:14:36
**Total Runtime:** 7137.89 ms
**Total Allocations:** 7074.46 MB
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
| 1 | `Inference` | 6858.548 | 96.09% | 3 | 2286.183 | 1773.282 | 3242.916 |
| 2 | `GenerateToken` | 6858.186 | 96.08% | 150 | 45.721 | 25.270 | 158.198 |
| 3 | `Transformer_Forward` | 6853.388 | 96.01% | 150 | 45.689 | 25.267 | 155.359 |
| 4 | `Transformer_ModelCreation` | 121.499 | 1.70% | 1 | 121.499 | 121.500 | 121.500 |
| 5 | `MatMul_Iteration` | 118.405 | 1.66% | 9 | 13.156 | 1.370 | 27.910 |
| 6 | `MatMul_512x512` | 74.688 | 1.05% | 1 | 74.688 | 74.688 | 74.688 |
| 7 | `MatMul_128x128` | 27.760 | 0.39% | 1 | 27.760 | 27.760 | 27.760 |
| 8 | `MatMul_256x256` | 17.526 | 0.25% | 1 | 17.526 | 17.526 | 17.526 |

## 💾 Top Allocators (by Memory)

Methods allocating the most memory:

| Rank | Method | Total Alloc (MB) | % of Total | Calls | Avg Alloc (KB) |
|------|--------|------------------|-----------|-------|----------------|
| 1 | `Inference` | 7044.030 | 99.57% | 3 | 2404362.206 |
| 2 | `GenerateToken` | 7044.022 | 99.57% | 150 | 48087.192 |
| 3 | `Transformer_Forward` | 7044.007 | 99.57% | 150 | 48087.087 |
| 4 | `Transformer_ModelCreation` | 26.399 | 0.37% | 1 | 27032.180 |
| 5 | `MatMul_Iteration` | 0.095 | 0.00% | 9 | 10.864 |
| 6 | `MatMul_128x128` | 0.080 | 0.00% | 1 | 81.992 |
| 7 | `MatMul_512x512` | 0.015 | 0.00% | 1 | 15.781 |
| 8 | `MatMul_256x256` | 0.000 | 0.00% | 1 | 0.000 |

## 📞 Most Called Methods

Methods called most frequently:

| Rank | Method | Calls | Total Time (ms) | Avg Time (μs) |
|------|--------|-------|----------------|---------------|
| 1 | `GenerateToken` | 150 | 6858.186 | 45721.239 |
| 2 | `Transformer_Forward` | 150 | 6853.388 | 45689.256 |
| 3 | `MatMul_Iteration` | 9 | 118.405 | 13156.163 |
| 4 | `Inference` | 3 | 6858.548 | 2286182.662 |
| 5 | `MatMul_128x128` | 1 | 27.760 | 27760.214 |
| 6 | `MatMul_256x256` | 1 | 17.526 | 17525.903 |
| 7 | `MatMul_512x512` | 1 | 74.688 | 74688.228 |
| 8 | `Transformer_ModelCreation` | 1 | 121.499 | 121498.778 |

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

