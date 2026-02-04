# Enhanced Performance Profile Report

**Generated:** 2026-02-04 03:09:32

## Summary

- **Total Runtime:** 2959.29 ms
- **Total Allocations:** 338.47 MB
- **Methods Profiled:** 29

## 🔥 Hot Paths (Top 30)

| Rank | Method | Time (ms) | Calls | Avg (ms) | Alloc (MB) |
|------|--------|-----------|-------|----------|------------|
| 1 | `Model_Medium_Inference` | 600.35 | 1 | 600.351 | 83.06 |
| 2 | `Model_Medium_GenerateToken` | 600.29 | 25 | 24.012 | 83.06 |
| 3 | `Model_Medium_Forward` | 599.95 | 25 | 23.998 | 83.06 |
| 4 | `Model_Small_Inference` | 241.57 | 1 | 241.575 | 18.95 |
| 5 | `Model_Small_GenerateToken` | 241.53 | 25 | 9.661 | 18.95 |
| 6 | `Model_Small_Forward` | 239.27 | 25 | 9.571 | 18.94 |
| 7 | `MatMul_512x512` | 103.19 | 1 | 103.186 | 0.02 |
| 8 | `MatMul_Iteration` | 92.17 | 12 | 7.680 | 0.02 |
| 9 | `Model_Medium_Creation` | 62.23 | 1 | 62.229 | 26.44 |
| 10 | `GELU_1000000` | 52.23 | 1 | 52.231 | 0.01 |
| 11 | `GELU_Iteration` | 46.98 | 20 | 2.349 | 0.00 |
| 12 | `MatMul_128x128` | 18.47 | 1 | 18.474 | 0.07 |
| 13 | `Model_Small_Creation` | 16.64 | 1 | 16.638 | 3.61 |
| 14 | `MatMul_256x256` | 11.83 | 1 | 11.829 | 0.01 |
| 15 | `MatMul_64x64` | 7.92 | 1 | 7.924 | 0.00 |
| 16 | `GELU_100000` | 5.43 | 1 | 5.430 | 0.00 |
| 17 | `TensorAdd_10000` | 2.95 | 1 | 2.945 | 0.38 |
| 18 | `TensorAdd_Iteration` | 2.93 | 10 | 0.293 | 0.38 |
| 19 | `GELU_1000` | 2.14 | 1 | 2.141 | 0.00 |
| 20 | `GELU_10000` | 1.98 | 1 | 1.976 | 0.00 |
| 21 | `BroadcastAdd_100x100` | 1.84 | 1 | 1.841 | 0.38 |
| 22 | `BroadcastAdd_Iteration` | 1.83 | 10 | 0.183 | 0.38 |
| 23 | `Softmax_Iteration` | 1.62 | 20 | 0.081 | 0.00 |
| 24 | `Softmax_2048` | 1.50 | 1 | 1.504 | 0.00 |
| 25 | `Softmax_256` | 1.08 | 1 | 1.084 | 0.00 |
| 26 | `TensorMul_10000` | 0.57 | 1 | 0.573 | 0.38 |
| 27 | `TensorMul_Iteration` | 0.56 | 10 | 0.056 | 0.38 |
| 28 | `Softmax_1024` | 0.15 | 1 | 0.155 | 0.00 |
| 29 | `Softmax_512` | 0.07 | 1 | 0.067 | 0.00 |

## 💾 Top Allocators

| Rank | Method | Total (MB) | Calls | Avg (KB) |
|------|--------|------------|-------|----------|
| 1 | `Model_Medium_Inference` | 83.06 | 1 | 85054.52 |
| 2 | `Model_Medium_GenerateToken` | 83.06 | 25 | 3402.18 |
| 3 | `Model_Medium_Forward` | 83.06 | 25 | 3402.18 |
| 4 | `Model_Medium_Creation` | 26.44 | 1 | 27071.29 |
| 5 | `Model_Small_Inference` | 18.95 | 1 | 19400.73 |
| 6 | `Model_Small_GenerateToken` | 18.95 | 25 | 776.03 |
| 7 | `Model_Small_Forward` | 18.94 | 25 | 775.71 |
| 8 | `Model_Small_Creation` | 3.61 | 1 | 3697.93 |
| 9 | `TensorAdd_10000` | 0.38 | 1 | 390.86 |
| 10 | `TensorAdd_Iteration` | 0.38 | 10 | 39.09 |
| 11 | `TensorMul_10000` | 0.38 | 1 | 390.86 |
| 12 | `TensorMul_Iteration` | 0.38 | 10 | 39.09 |
| 13 | `BroadcastAdd_100x100` | 0.38 | 1 | 390.86 |
| 14 | `BroadcastAdd_Iteration` | 0.38 | 10 | 39.09 |
| 15 | `MatMul_128x128` | 0.07 | 1 | 74.08 |

## Analysis

See COMPREHENSIVE_HOT_PATHS_ANALYSIS.md for detailed recommendations.
