# Enhanced Performance Profile Report

**Generated:** 2026-02-04 02:02:13

## Summary

- **Total Runtime:** 9237.19 ms
- **Total Allocations:** 338.62 MB
- **Methods Profiled:** 29

## 🔥 Hot Paths (Top 30)

| Rank | Method | Time (ms) | Calls | Avg (ms) | Alloc (MB) |
|------|--------|-----------|-------|----------|------------|
| 1 | `Model_Medium_Inference` | 1863.27 | 1 | 1863.273 | 83.10 |
| 2 | `Model_Medium_GenerateToken` | 1863.22 | 25 | 74.529 | 83.10 |
| 3 | `Model_Medium_Forward` | 1862.82 | 25 | 74.513 | 83.10 |
| 4 | `MatMul_512x512` | 905.90 | 1 | 905.900 | 0.00 |
| 5 | `MatMul_Iteration` | 775.87 | 12 | 64.655 | 0.00 |
| 6 | `Model_Small_Inference` | 443.94 | 1 | 443.939 | 19.00 |
| 7 | `Model_Small_GenerateToken` | 443.88 | 25 | 17.755 | 19.00 |
| 8 | `Model_Small_Forward` | 441.38 | 25 | 17.655 | 19.00 |
| 9 | `GELU_1000000` | 202.40 | 1 | 202.398 | 0.01 |
| 10 | `GELU_Iteration` | 186.08 | 20 | 9.304 | 0.00 |
| 11 | `MatMul_256x256` | 112.93 | 1 | 112.931 | 0.00 |
| 12 | `Model_Medium_Creation` | 54.53 | 1 | 54.529 | 26.41 |
| 13 | `Model_Small_Creation` | 20.21 | 1 | 20.213 | 3.61 |
| 14 | `GELU_100000` | 20.16 | 1 | 20.163 | 0.00 |
| 15 | `MatMul_128x128` | 13.29 | 1 | 13.290 | 0.00 |
| 16 | `MatMul_64x64` | 7.39 | 1 | 7.395 | 0.00 |
| 17 | `Softmax_256` | 2.47 | 1 | 2.474 | 0.00 |
| 18 | `BroadcastAdd_100x100` | 2.40 | 1 | 2.403 | 0.38 |
| 19 | `BroadcastAdd_Iteration` | 2.39 | 10 | 0.239 | 0.38 |
| 20 | `GELU_10000` | 2.30 | 1 | 2.304 | 0.00 |
| 21 | `TensorAdd_10000` | 2.24 | 1 | 2.245 | 0.38 |
| 22 | `TensorAdd_Iteration` | 2.23 | 10 | 0.223 | 0.38 |
| 23 | `TensorMul_10000` | 1.97 | 1 | 1.969 | 0.38 |
| 24 | `TensorMul_Iteration` | 1.95 | 10 | 0.195 | 0.38 |
| 25 | `GELU_1000` | 1.02 | 1 | 1.022 | 0.00 |
| 26 | `Softmax_Iteration` | 0.44 | 20 | 0.022 | 0.00 |
| 27 | `Softmax_2048` | 0.26 | 1 | 0.259 | 0.00 |
| 28 | `Softmax_1024` | 0.15 | 1 | 0.150 | 0.00 |
| 29 | `Softmax_512` | 0.07 | 1 | 0.073 | 0.00 |

## 💾 Top Allocators

| Rank | Method | Total (MB) | Calls | Avg (KB) |
|------|--------|------------|-------|----------|
| 1 | `Model_Medium_Inference` | 83.10 | 1 | 85094.55 |
| 2 | `Model_Medium_GenerateToken` | 83.10 | 25 | 3403.78 |
| 3 | `Model_Medium_Forward` | 83.10 | 25 | 3403.78 |
| 4 | `Model_Medium_Creation` | 26.41 | 1 | 27043.44 |
| 5 | `Model_Small_Inference` | 19.00 | 1 | 19460.30 |
| 6 | `Model_Small_GenerateToken` | 19.00 | 25 | 778.41 |
| 7 | `Model_Small_Forward` | 19.00 | 25 | 778.09 |
| 8 | `Model_Small_Creation` | 3.61 | 1 | 3697.93 |
| 9 | `TensorAdd_10000` | 0.38 | 1 | 390.86 |
| 10 | `TensorAdd_Iteration` | 0.38 | 10 | 39.09 |
| 11 | `TensorMul_10000` | 0.38 | 1 | 390.86 |
| 12 | `TensorMul_Iteration` | 0.38 | 10 | 39.09 |
| 13 | `BroadcastAdd_100x100` | 0.38 | 1 | 390.86 |
| 14 | `BroadcastAdd_Iteration` | 0.38 | 10 | 39.09 |
| 15 | `GELU_1000000` | 0.01 | 1 | 8.28 |

## Analysis

See COMPREHENSIVE_HOT_PATHS_ANALYSIS.md for detailed recommendations.
