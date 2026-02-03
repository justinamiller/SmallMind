# Enhanced Performance Profile Report

**Generated:** 2026-02-03 02:45:11

## Summary

- **Total Runtime:** 10312.66 ms
- **Total Allocations:** 2566.91 MB
- **Methods Profiled:** 29

## 🔥 Hot Paths (Top 30)

| Rank | Method | Time (ms) | Calls | Avg (ms) | Alloc (MB) |
|------|--------|-----------|-------|----------|------------|
| 1 | `Model_Medium_Inference` | 2553.97 | 1 | 2553.969 | 734.48 |
| 2 | `Model_Medium_GenerateToken` | 2553.92 | 25 | 102.157 | 734.48 |
| 3 | `Model_Medium_Forward` | 2553.49 | 25 | 102.140 | 734.48 |
| 4 | `Model_Small_Inference` | 447.73 | 1 | 447.725 | 110.33 |
| 5 | `Model_Small_GenerateToken` | 447.66 | 25 | 17.906 | 110.33 |
| 6 | `Model_Small_Forward` | 445.85 | 25 | 17.834 | 110.33 |
| 7 | `MatMul_512x512` | 414.73 | 1 | 414.730 | 0.02 |
| 8 | `MatMul_Iteration` | 341.18 | 12 | 28.432 | 0.02 |
| 9 | `GELU_1000000` | 178.07 | 1 | 178.070 | 0.01 |
| 10 | `GELU_Iteration` | 161.51 | 20 | 8.075 | 0.00 |
| 11 | `Model_Medium_Creation` | 82.17 | 1 | 82.166 | 26.42 |
| 12 | `MatMul_256x256` | 53.01 | 1 | 53.005 | 0.01 |
| 13 | `GELU_100000` | 20.41 | 1 | 20.412 | 0.00 |
| 14 | `Model_Small_Creation` | 20.08 | 1 | 20.077 | 3.61 |
| 15 | `MatMul_64x64` | 16.13 | 1 | 16.127 | 0.07 |
| 16 | `MatMul_128x128` | 7.04 | 1 | 7.043 | 0.01 |
| 17 | `Softmax_256` | 2.37 | 1 | 2.367 | 0.00 |
| 18 | `TensorAdd_10000` | 2.29 | 1 | 2.289 | 0.38 |
| 19 | `TensorAdd_Iteration` | 2.28 | 10 | 0.228 | 0.38 |
| 20 | `GELU_10000` | 2.03 | 1 | 2.030 | 0.00 |
| 21 | `BroadcastAdd_100x100` | 1.37 | 1 | 1.374 | 0.39 |
| 22 | `BroadcastAdd_Iteration` | 1.36 | 10 | 0.136 | 0.39 |
| 23 | `TensorMul_10000` | 1.07 | 1 | 1.065 | 0.38 |
| 24 | `TensorMul_Iteration` | 1.06 | 10 | 0.106 | 0.38 |
| 25 | `GELU_1000` | 1.01 | 1 | 1.010 | 0.00 |
| 26 | `Softmax_Iteration` | 0.40 | 20 | 0.020 | 0.00 |
| 27 | `Softmax_2048` | 0.27 | 1 | 0.270 | 0.00 |
| 28 | `Softmax_1024` | 0.14 | 1 | 0.141 | 0.00 |
| 29 | `Softmax_512` | 0.08 | 1 | 0.077 | 0.00 |

## 💾 Top Allocators

| Rank | Method | Total (MB) | Calls | Avg (KB) |
|------|--------|------------|-------|----------|
| 1 | `Model_Medium_Inference` | 734.48 | 1 | 752106.39 |
| 2 | `Model_Medium_GenerateToken` | 734.48 | 25 | 30084.26 |
| 3 | `Model_Medium_Forward` | 734.48 | 25 | 30084.26 |
| 4 | `Model_Small_Inference` | 110.33 | 1 | 112981.80 |
| 5 | `Model_Small_GenerateToken` | 110.33 | 25 | 4519.27 |
| 6 | `Model_Small_Forward` | 110.33 | 25 | 4519.27 |
| 7 | `Model_Medium_Creation` | 26.42 | 1 | 27055.30 |
| 8 | `Model_Small_Creation` | 3.61 | 1 | 3697.93 |
| 9 | `BroadcastAdd_100x100` | 0.39 | 1 | 398.87 |
| 10 | `BroadcastAdd_Iteration` | 0.39 | 10 | 39.89 |
| 11 | `TensorAdd_10000` | 0.38 | 1 | 390.86 |
| 12 | `TensorAdd_Iteration` | 0.38 | 10 | 39.09 |
| 13 | `TensorMul_10000` | 0.38 | 1 | 390.86 |
| 14 | `TensorMul_Iteration` | 0.38 | 10 | 39.09 |
| 15 | `MatMul_64x64` | 0.07 | 1 | 74.18 |

## Analysis

See COMPREHENSIVE_HOT_PATHS_ANALYSIS.md for detailed recommendations.
