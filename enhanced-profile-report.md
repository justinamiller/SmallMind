# Enhanced Performance Profile Report

**Generated:** 2026-02-03 02:36:36

## Summary

- **Total Runtime:** 5591.90 ms
- **Total Allocations:** 2566.92 MB
- **Methods Profiled:** 29

## 🔥 Hot Paths (Top 30)

| Rank | Method | Time (ms) | Calls | Avg (ms) | Alloc (MB) |
|------|--------|-----------|-------|----------|------------|
| 1 | `Model_Medium_Inference` | 1221.98 | 1 | 1221.976 | 734.48 |
| 2 | `Model_Medium_GenerateToken` | 1221.89 | 25 | 48.876 | 734.48 |
| 3 | `Model_Medium_Forward` | 1221.40 | 25 | 48.856 | 734.48 |
| 4 | `Model_Small_Inference` | 453.56 | 1 | 453.556 | 110.35 |
| 5 | `Model_Small_GenerateToken` | 453.49 | 25 | 18.140 | 110.35 |
| 6 | `Model_Small_Forward` | 451.24 | 25 | 18.049 | 110.35 |
| 7 | `MatMul_Iteration` | 124.23 | 12 | 10.353 | 0.02 |
| 8 | `MatMul_512x512` | 119.89 | 1 | 119.894 | 0.02 |
| 9 | `Model_Medium_Creation` | 107.09 | 1 | 107.087 | 26.41 |
| 10 | `GELU_1000000` | 59.22 | 1 | 59.221 | 0.01 |
| 11 | `GELU_Iteration` | 53.93 | 20 | 2.697 | 0.00 |
| 12 | `MatMul_256x256` | 29.17 | 1 | 29.173 | 0.01 |
| 13 | `MatMul_64x64` | 23.49 | 1 | 23.488 | 0.07 |
| 14 | `Model_Small_Creation` | 15.62 | 1 | 15.618 | 3.61 |
| 15 | `MatMul_128x128` | 9.85 | 1 | 9.847 | 0.01 |
| 16 | `GELU_100000` | 6.22 | 1 | 6.221 | 0.00 |
| 17 | `TensorAdd_10000` | 2.94 | 1 | 2.937 | 0.38 |
| 18 | `TensorAdd_Iteration` | 2.93 | 10 | 0.293 | 0.38 |
| 19 | `Softmax_256` | 2.26 | 1 | 2.258 | 0.00 |
| 20 | `Softmax_Iteration` | 2.21 | 20 | 0.111 | 0.00 |
| 21 | `Softmax_2048` | 2.01 | 1 | 2.007 | 0.00 |
| 22 | `BroadcastAdd_100x100` | 1.99 | 1 | 1.989 | 0.38 |
| 23 | `BroadcastAdd_Iteration` | 1.98 | 10 | 0.198 | 0.38 |
| 24 | `GELU_10000` | 1.43 | 1 | 1.430 | 0.00 |
| 25 | `GELU_1000` | 0.55 | 1 | 0.552 | 0.00 |
| 26 | `TensorMul_10000` | 0.54 | 1 | 0.536 | 0.38 |
| 27 | `TensorMul_Iteration` | 0.53 | 10 | 0.053 | 0.38 |
| 28 | `Softmax_1024` | 0.16 | 1 | 0.163 | 0.00 |
| 29 | `Softmax_512` | 0.11 | 1 | 0.113 | 0.00 |

## 💾 Top Allocators

| Rank | Method | Total (MB) | Calls | Avg (KB) |
|------|--------|------------|-------|----------|
| 1 | `Model_Medium_Inference` | 734.48 | 1 | 752108.33 |
| 2 | `Model_Medium_GenerateToken` | 734.48 | 25 | 30084.33 |
| 3 | `Model_Medium_Forward` | 734.48 | 25 | 30084.33 |
| 4 | `Model_Small_Inference` | 110.35 | 1 | 112994.33 |
| 5 | `Model_Small_GenerateToken` | 110.35 | 25 | 4519.77 |
| 6 | `Model_Small_Forward` | 110.35 | 25 | 4519.77 |
| 7 | `Model_Medium_Creation` | 26.41 | 1 | 27040.95 |
| 8 | `Model_Small_Creation` | 3.61 | 1 | 3697.52 |
| 9 | `TensorAdd_10000` | 0.38 | 1 | 390.86 |
| 10 | `TensorAdd_Iteration` | 0.38 | 10 | 39.09 |
| 11 | `TensorMul_10000` | 0.38 | 1 | 390.86 |
| 12 | `TensorMul_Iteration` | 0.38 | 10 | 39.09 |
| 13 | `BroadcastAdd_100x100` | 0.38 | 1 | 390.86 |
| 14 | `BroadcastAdd_Iteration` | 0.38 | 10 | 39.09 |
| 15 | `MatMul_64x64` | 0.07 | 1 | 73.70 |

## Analysis

See COMPREHENSIVE_HOT_PATHS_ANALYSIS.md for detailed recommendations.
