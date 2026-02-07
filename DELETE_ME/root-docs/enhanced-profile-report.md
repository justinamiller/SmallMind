# Enhanced Performance Profile Report

**Generated:** 2026-02-06 01:20:49

## Summary

- **Total Runtime:** 3210.79 ms
- **Total Allocations:** 337.79 MB
- **Methods Profiled:** 29

## 🔥 Hot Paths (Top 30)

| Rank | Method | Time (ms) | Calls | Avg (ms) | Alloc (MB) |
|------|--------|-----------|-------|----------|------------|
| 1 | `Model_Medium_Inference` | 549.84 | 1 | 549.839 | 82.87 |
| 2 | `Model_Medium_GenerateToken` | 549.75 | 25 | 21.990 | 82.87 |
| 3 | `Model_Medium_Forward` | 549.28 | 25 | 21.971 | 82.87 |
| 4 | `Model_Small_Inference` | 315.50 | 1 | 315.502 | 18.91 |
| 5 | `Model_Small_GenerateToken` | 315.43 | 25 | 12.617 | 18.91 |
| 6 | `Model_Small_Forward` | 312.62 | 25 | 12.505 | 18.91 |
| 7 | `MatMul_512x512` | 121.44 | 1 | 121.439 | 0.01 |
| 8 | `MatMul_Iteration` | 111.42 | 12 | 9.285 | 0.02 |
| 9 | `GELU_1000000` | 106.38 | 1 | 106.375 | 0.01 |
| 10 | `GELU_Iteration` | 98.66 | 20 | 4.933 | 0.00 |
| 11 | `Model_Medium_Creation` | 62.66 | 1 | 62.661 | 26.42 |
| 12 | `MatMul_128x128` | 25.28 | 1 | 25.278 | 0.07 |
| 13 | `MatMul_256x256` | 17.15 | 1 | 17.151 | 0.01 |
| 14 | `Model_Small_Creation` | 16.54 | 1 | 16.544 | 3.61 |
| 15 | `GELU_100000` | 15.30 | 1 | 15.304 | 0.00 |
| 16 | `MatMul_64x64` | 7.83 | 1 | 7.826 | 0.00 |
| 17 | `Softmax_Iteration` | 6.01 | 20 | 0.300 | 0.00 |
| 18 | `Softmax_2048` | 5.87 | 1 | 5.870 | 0.00 |
| 19 | `GELU_10000` | 5.74 | 1 | 5.736 | 0.00 |
| 20 | `TensorAdd_10000` | 4.14 | 1 | 4.140 | 0.38 |
| 21 | `TensorAdd_Iteration` | 4.12 | 10 | 0.412 | 0.38 |
| 22 | `GELU_1000` | 3.16 | 1 | 3.159 | 0.00 |
| 23 | `BroadcastAdd_100x100` | 1.97 | 1 | 1.967 | 0.39 |
| 24 | `BroadcastAdd_Iteration` | 1.95 | 10 | 0.195 | 0.39 |
| 25 | `Softmax_256` | 1.43 | 1 | 1.431 | 0.00 |
| 26 | `TensorMul_10000` | 0.55 | 1 | 0.546 | 0.38 |
| 27 | `TensorMul_Iteration` | 0.53 | 10 | 0.053 | 0.38 |
| 28 | `Softmax_1024` | 0.15 | 1 | 0.154 | 0.00 |
| 29 | `Softmax_512` | 0.09 | 1 | 0.088 | 0.00 |

## 💾 Top Allocators

| Rank | Method | Total (MB) | Calls | Avg (KB) |
|------|--------|------------|-------|----------|
| 1 | `Model_Medium_Inference` | 82.87 | 1 | 84854.43 |
| 2 | `Model_Medium_GenerateToken` | 82.87 | 25 | 3394.18 |
| 3 | `Model_Medium_Forward` | 82.87 | 25 | 3394.18 |
| 4 | `Model_Medium_Creation` | 26.42 | 1 | 27058.33 |
| 5 | `Model_Small_Inference` | 18.91 | 1 | 19367.20 |
| 6 | `Model_Small_GenerateToken` | 18.91 | 25 | 774.69 |
| 7 | `Model_Small_Forward` | 18.91 | 25 | 774.37 |
| 8 | `Model_Small_Creation` | 3.61 | 1 | 3697.93 |
| 9 | `BroadcastAdd_100x100` | 0.39 | 1 | 398.87 |
| 10 | `BroadcastAdd_Iteration` | 0.39 | 10 | 39.89 |
| 11 | `TensorAdd_10000` | 0.38 | 1 | 390.86 |
| 12 | `TensorAdd_Iteration` | 0.38 | 10 | 39.09 |
| 13 | `TensorMul_10000` | 0.38 | 1 | 390.86 |
| 14 | `TensorMul_Iteration` | 0.38 | 10 | 39.09 |
| 15 | `MatMul_128x128` | 0.07 | 1 | 74.09 |

## Analysis

See COMPREHENSIVE_HOT_PATHS_ANALYSIS.md for detailed recommendations.
