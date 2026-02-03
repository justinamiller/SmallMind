# Enhanced Performance Profile Report

**Generated:** 2026-02-03 01:56:21

## Summary

- **Total Runtime:** 4400.55 ms
- **Total Allocations:** 3033.91 MB
- **Methods Profiled:** 29

## 🔥 Hot Paths (Top 30)

| Rank | Method | Time (ms) | Calls | Avg (ms) | Alloc (MB) |
|------|--------|-----------|-------|----------|------------|
| 1 | `Model_Medium_Inference` | 1018.27 | 1 | 1018.273 | 857.83 |
| 2 | `Model_Medium_GenerateToken` | 1018.19 | 25 | 40.728 | 857.83 |
| 3 | `Model_Medium_Forward` | 1017.63 | 25 | 40.705 | 857.83 |
| 4 | `Model_Small_Inference` | 306.66 | 1 | 306.655 | 142.65 |
| 5 | `Model_Small_GenerateToken` | 306.60 | 25 | 12.264 | 142.65 |
| 6 | `Model_Small_Forward` | 304.15 | 25 | 12.166 | 142.65 |
| 7 | `MatMul_512x512` | 96.86 | 1 | 96.860 | 0.02 |
| 8 | `MatMul_Iteration` | 90.70 | 12 | 7.558 | 0.04 |
| 9 | `GELU_1000000` | 60.50 | 1 | 60.496 | 0.01 |
| 10 | `GELU_Iteration` | 54.65 | 20 | 2.732 | 0.00 |
| 11 | `Model_Medium_Creation` | 44.94 | 1 | 44.941 | 26.43 |
| 12 | `MatMul_256x256` | 18.10 | 1 | 18.095 | 0.01 |
| 13 | `Model_Small_Creation` | 15.73 | 1 | 15.725 | 3.60 |
| 14 | `MatMul_64x64` | 14.58 | 1 | 14.576 | 0.07 |
| 15 | `GELU_100000` | 6.71 | 1 | 6.708 | 0.01 |
| 16 | `MatMul_128x128` | 5.65 | 1 | 5.651 | 0.01 |
| 17 | `TensorAdd_10000` | 3.00 | 1 | 3.003 | 0.38 |
| 18 | `TensorAdd_Iteration` | 2.99 | 10 | 0.299 | 0.38 |
| 19 | `Softmax_256` | 2.46 | 1 | 2.458 | 0.00 |
| 20 | `Softmax_Iteration` | 2.44 | 20 | 0.122 | 0.00 |
| 21 | `Softmax_2048` | 2.24 | 1 | 2.236 | 0.00 |
| 22 | `BroadcastAdd_100x100` | 1.89 | 1 | 1.888 | 0.38 |
| 23 | `BroadcastAdd_Iteration` | 1.87 | 10 | 0.187 | 0.38 |
| 24 | `GELU_10000` | 1.42 | 1 | 1.417 | 0.00 |
| 25 | `GELU_1000` | 1.05 | 1 | 1.048 | 0.00 |
| 26 | `TensorMul_10000` | 0.52 | 1 | 0.525 | 0.38 |
| 27 | `TensorMul_Iteration` | 0.52 | 10 | 0.052 | 0.38 |
| 28 | `Softmax_1024` | 0.18 | 1 | 0.183 | 0.00 |
| 29 | `Softmax_512` | 0.08 | 1 | 0.080 | 0.00 |

## 💾 Top Allocators

| Rank | Method | Total (MB) | Calls | Avg (KB) |
|------|--------|------------|-------|----------|
| 1 | `Model_Medium_Inference` | 857.83 | 1 | 878420.69 |
| 2 | `Model_Medium_GenerateToken` | 857.83 | 25 | 35136.83 |
| 3 | `Model_Medium_Forward` | 857.83 | 25 | 35136.83 |
| 4 | `Model_Small_Inference` | 142.65 | 1 | 146068.84 |
| 5 | `Model_Small_GenerateToken` | 142.65 | 25 | 5842.75 |
| 6 | `Model_Small_Forward` | 142.65 | 25 | 5842.75 |
| 7 | `Model_Medium_Creation` | 26.43 | 1 | 27060.96 |
| 8 | `Model_Small_Creation` | 3.60 | 1 | 3689.72 |
| 9 | `TensorAdd_10000` | 0.38 | 1 | 390.86 |
| 10 | `TensorAdd_Iteration` | 0.38 | 10 | 39.09 |
| 11 | `TensorMul_10000` | 0.38 | 1 | 390.86 |
| 12 | `TensorMul_Iteration` | 0.38 | 10 | 39.09 |
| 13 | `BroadcastAdd_100x100` | 0.38 | 1 | 390.86 |
| 14 | `BroadcastAdd_Iteration` | 0.38 | 10 | 39.09 |
| 15 | `MatMul_64x64` | 0.07 | 1 | 74.11 |

## Analysis

See COMPREHENSIVE_HOT_PATHS_ANALYSIS.md for detailed recommendations.
