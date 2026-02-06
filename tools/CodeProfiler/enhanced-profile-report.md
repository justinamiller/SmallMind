# Enhanced Performance Profile Report

**Generated:** 2026-02-06 15:36:24

## Summary

- **Total Runtime:** 2566.09 ms
- **Total Allocations:** 337.83 MB
- **Methods Profiled:** 29

## 🔥 Hot Paths (Top 30)

| Rank | Method | Time (ms) | Calls | Avg (ms) | Alloc (MB) |
|------|--------|-----------|-------|----------|------------|
| 1 | `Model_Medium_Inference` | 510.90 | 1 | 510.904 | 82.87 |
| 2 | `Model_Medium_GenerateToken` | 510.81 | 25 | 20.433 | 82.87 |
| 3 | `Model_Medium_Forward` | 510.35 | 25 | 20.414 | 82.87 |
| 4 | `Model_Small_Inference` | 232.60 | 1 | 232.600 | 18.92 |
| 5 | `Model_Small_GenerateToken` | 232.54 | 25 | 9.302 | 18.92 |
| 6 | `Model_Small_Forward` | 230.15 | 25 | 9.206 | 18.91 |
| 7 | `MatMul_512x512` | 107.13 | 1 | 107.135 | 0.01 |
| 8 | `MatMul_Iteration` | 97.03 | 12 | 8.086 | 0.02 |
| 9 | `Model_Medium_Creation` | 44.83 | 1 | 44.828 | 26.42 |
| 10 | `MatMul_128x128` | 16.87 | 1 | 16.868 | 0.07 |
| 11 | `Model_Small_Creation` | 15.98 | 1 | 15.983 | 3.61 |
| 12 | `MatMul_256x256` | 14.29 | 1 | 14.289 | 0.01 |
| 13 | `GELU_1000000` | 6.79 | 1 | 6.793 | 0.02 |
| 14 | `MatMul_64x64` | 5.69 | 1 | 5.694 | 0.00 |
| 15 | `GELU_Iteration` | 5.15 | 20 | 0.257 | 0.01 |
| 16 | `GELU_1000` | 4.70 | 1 | 4.696 | 0.00 |
| 17 | `TensorAdd_10000` | 3.49 | 1 | 3.491 | 0.38 |
| 18 | `TensorAdd_Iteration` | 3.48 | 10 | 0.348 | 0.38 |
| 19 | `Softmax_Iteration` | 2.49 | 20 | 0.125 | 0.00 |
| 20 | `Softmax_256` | 2.42 | 1 | 2.421 | 0.00 |
| 21 | `Softmax_2048` | 2.19 | 1 | 2.187 | 0.00 |
| 22 | `BroadcastAdd_100x100` | 1.97 | 1 | 1.969 | 0.38 |
| 23 | `BroadcastAdd_Iteration` | 1.95 | 10 | 0.195 | 0.38 |
| 24 | `GELU_100000` | 0.70 | 1 | 0.702 | 0.00 |
| 25 | `TensorMul_10000` | 0.57 | 1 | 0.571 | 0.38 |
| 26 | `TensorMul_Iteration` | 0.56 | 10 | 0.056 | 0.38 |
| 27 | `Softmax_1024` | 0.29 | 1 | 0.286 | 0.00 |
| 28 | `Softmax_512` | 0.10 | 1 | 0.104 | 0.00 |
| 29 | `GELU_10000` | 0.05 | 1 | 0.052 | 0.00 |

## 💾 Top Allocators

| Rank | Method | Total (MB) | Calls | Avg (KB) |
|------|--------|------------|-------|----------|
| 1 | `Model_Medium_Inference` | 82.87 | 1 | 84863.73 |
| 2 | `Model_Medium_GenerateToken` | 82.87 | 25 | 3394.55 |
| 3 | `Model_Medium_Forward` | 82.87 | 25 | 3394.55 |
| 4 | `Model_Medium_Creation` | 26.42 | 1 | 27058.86 |
| 5 | `Model_Small_Inference` | 18.92 | 1 | 19371.27 |
| 6 | `Model_Small_GenerateToken` | 18.92 | 25 | 774.85 |
| 7 | `Model_Small_Forward` | 18.91 | 25 | 774.53 |
| 8 | `Model_Small_Creation` | 3.61 | 1 | 3697.93 |
| 9 | `TensorAdd_10000` | 0.38 | 1 | 390.86 |
| 10 | `TensorAdd_Iteration` | 0.38 | 10 | 39.09 |
| 11 | `TensorMul_10000` | 0.38 | 1 | 390.86 |
| 12 | `TensorMul_Iteration` | 0.38 | 10 | 39.09 |
| 13 | `BroadcastAdd_100x100` | 0.38 | 1 | 390.86 |
| 14 | `BroadcastAdd_Iteration` | 0.38 | 10 | 39.09 |
| 15 | `MatMul_128x128` | 0.07 | 1 | 74.02 |

## Analysis

See COMPREHENSIVE_HOT_PATHS_ANALYSIS.md for detailed recommendations.
