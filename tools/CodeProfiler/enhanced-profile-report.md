# Enhanced Performance Profile Report

**Generated:** 2026-02-04 04:58:59

## Summary

- **Total Runtime:** 2425.29 ms
- **Total Allocations:** 337.81 MB
- **Methods Profiled:** 29

## 🔥 Hot Paths (Top 30)

| Rank | Method | Time (ms) | Calls | Avg (ms) | Alloc (MB) |
|------|--------|-----------|-------|----------|------------|
| 1 | `Model_Medium_Inference` | 422.77 | 1 | 422.770 | 82.87 |
| 2 | `Model_Medium_GenerateToken` | 422.71 | 25 | 16.908 | 82.87 |
| 3 | `Model_Medium_Forward` | 422.43 | 25 | 16.897 | 82.87 |
| 4 | `Model_Small_Inference` | 234.94 | 1 | 234.940 | 18.91 |
| 5 | `Model_Small_GenerateToken` | 234.90 | 25 | 9.396 | 18.91 |
| 6 | `Model_Small_Forward` | 232.69 | 25 | 9.308 | 18.91 |
| 7 | `MatMul_512x512` | 104.07 | 1 | 104.075 | 0.02 |
| 8 | `MatMul_Iteration` | 97.19 | 12 | 8.099 | 0.03 |
| 9 | `Model_Medium_Creation` | 63.94 | 1 | 63.944 | 26.42 |
| 10 | `GELU_1000000` | 54.04 | 1 | 54.043 | 0.01 |
| 11 | `GELU_Iteration` | 48.67 | 20 | 2.434 | 0.00 |
| 12 | `MatMul_128x128` | 21.33 | 1 | 21.326 | 0.07 |
| 13 | `Model_Small_Creation` | 15.81 | 1 | 15.810 | 3.61 |
| 14 | `MatMul_256x256` | 15.09 | 1 | 15.086 | 0.01 |
| 15 | `MatMul_64x64` | 8.25 | 1 | 8.249 | 0.00 |
| 16 | `GELU_100000` | 7.18 | 1 | 7.175 | 0.00 |
| 17 | `TensorAdd_10000` | 2.82 | 1 | 2.820 | 0.38 |
| 18 | `TensorAdd_Iteration` | 2.81 | 10 | 0.281 | 0.38 |
| 19 | `GELU_10000` | 2.56 | 1 | 2.561 | 0.00 |
| 20 | `BroadcastAdd_100x100` | 1.94 | 1 | 1.936 | 0.38 |
| 21 | `BroadcastAdd_Iteration` | 1.93 | 10 | 0.193 | 0.38 |
| 22 | `Softmax_Iteration` | 1.77 | 20 | 0.089 | 0.00 |
| 23 | `Softmax_2048` | 1.64 | 1 | 1.635 | 0.00 |
| 24 | `Softmax_256` | 1.26 | 1 | 1.258 | 0.00 |
| 25 | `GELU_1000` | 1.23 | 1 | 1.230 | 0.00 |
| 26 | `TensorMul_10000` | 0.55 | 1 | 0.555 | 0.38 |
| 27 | `TensorMul_Iteration` | 0.54 | 10 | 0.054 | 0.38 |
| 28 | `Softmax_1024` | 0.16 | 1 | 0.164 | 0.00 |
| 29 | `Softmax_512` | 0.06 | 1 | 0.063 | 0.00 |

## 💾 Top Allocators

| Rank | Method | Total (MB) | Calls | Avg (KB) |
|------|--------|------------|-------|----------|
| 1 | `Model_Medium_Inference` | 82.87 | 1 | 84861.34 |
| 2 | `Model_Medium_GenerateToken` | 82.87 | 25 | 3394.45 |
| 3 | `Model_Medium_Forward` | 82.87 | 25 | 3394.45 |
| 4 | `Model_Medium_Creation` | 26.42 | 1 | 27058.89 |
| 5 | `Model_Small_Inference` | 18.91 | 1 | 19367.09 |
| 6 | `Model_Small_GenerateToken` | 18.91 | 25 | 774.68 |
| 7 | `Model_Small_Forward` | 18.91 | 25 | 774.36 |
| 8 | `Model_Small_Creation` | 3.61 | 1 | 3697.52 |
| 9 | `TensorAdd_10000` | 0.38 | 1 | 390.86 |
| 10 | `TensorAdd_Iteration` | 0.38 | 10 | 39.09 |
| 11 | `TensorMul_10000` | 0.38 | 1 | 390.86 |
| 12 | `TensorMul_Iteration` | 0.38 | 10 | 39.09 |
| 13 | `BroadcastAdd_100x100` | 0.38 | 1 | 390.86 |
| 14 | `BroadcastAdd_Iteration` | 0.38 | 10 | 39.09 |
| 15 | `MatMul_128x128` | 0.07 | 1 | 74.09 |

## Analysis

See COMPREHENSIVE_HOT_PATHS_ANALYSIS.md for detailed recommendations.
