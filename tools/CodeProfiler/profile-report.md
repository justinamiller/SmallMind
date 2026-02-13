# Performance Profile Report

**Generated:** 2026-02-13 16:02:01
**Total Runtime:** 6303.41 ms
**Total Allocations:** 6189.57 MB
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
| 1 | `Inference` | 6068.630 | 96.28% | 3 | 2022.877 | 1658.325 | 2705.121 |
| 2 | `GenerateToken` | 6066.267 | 96.24% | 150 | 40.442 | 26.881 | 189.184 |
| 3 | `Transformer_Forward` | 6061.924 | 96.17% | 150 | 40.413 | 26.875 | 186.060 |
| 4 | `Transformer_ModelCreation` | 109.854 | 1.74% | 1 | 109.854 | 109.855 | 109.855 |
| 5 | `MatMul_Iteration` | 80.959 | 1.28% | 9 | 8.995 | 0.108 | 24.318 |
| 6 | `MatMul_512x512` | 67.773 | 1.08% | 1 | 67.773 | 67.773 | 67.773 |
| 7 | `MatMul_128x128` | 9.414 | 0.15% | 1 | 9.414 | 9.414 | 9.414 |
| 8 | `MatMul_256x256` | 5.447 | 0.09% | 1 | 5.447 | 5.448 | 5.448 |

## 💾 Top Allocators (by Memory)

Methods allocating the most memory:

| Rank | Method | Total Alloc (MB) | % of Total | Calls | Avg Alloc (KB) |
|------|--------|------------------|-----------|-------|----------------|
| 1 | `Inference` | 6159.168 | 99.51% | 3 | 2102329.432 |
| 2 | `GenerateToken` | 6159.168 | 99.51% | 150 | 42046.589 |
| 3 | `Transformer_Forward` | 6159.025 | 99.51% | 150 | 42045.610 |
| 4 | `Transformer_ModelCreation` | 26.453 | 0.43% | 1 | 27087.641 |
| 5 | `MatMul_128x128` | 0.008 | 0.00% | 1 | 8.008 |
| 6 | `MatMul_Iteration` | 0.000 | 0.00% | 9 | 0.000 |
| 7 | `MatMul_256x256` | 0.000 | 0.00% | 1 | 0.000 |
| 8 | `MatMul_512x512` | 0.000 | 0.00% | 1 | 0.000 |

## 📞 Most Called Methods

Methods called most frequently:

| Rank | Method | Calls | Total Time (ms) | Avg Time (μs) |
|------|--------|-------|----------------|---------------|
| 1 | `GenerateToken` | 150 | 6066.267 | 40441.783 |
| 2 | `Transformer_Forward` | 150 | 6061.924 | 40412.825 |
| 3 | `MatMul_Iteration` | 9 | 80.959 | 8995.400 |
| 4 | `Inference` | 3 | 6068.630 | 2022876.683 |
| 5 | `MatMul_128x128` | 1 | 9.414 | 9414.312 |
| 6 | `MatMul_256x256` | 1 | 5.447 | 5447.419 |
| 7 | `MatMul_512x512` | 1 | 67.773 | 67773.194 |
| 8 | `Transformer_ModelCreation` | 1 | 109.854 | 109853.588 |

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

