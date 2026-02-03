# Enhanced Performance Profile Report

**Generated:** 2026-02-03 02:17:17

## Summary

- **Total Runtime:** 5391.06 ms
- **Total Allocations:** 3008.75 MB
- **Methods Profiled:** 29

## 🔥 Hot Paths (Top 30)

| Rank | Method | Time (ms) | Calls | Avg (ms) | Alloc (MB) |
|------|--------|-----------|-------|----------|------------|
| 1 | `Model_Medium_Inference` | 1170.44 | 1 | 1170.445 | 851.16 |
| 2 | `Model_Medium_GenerateToken` | 1170.36 | 25 | 46.815 | 851.16 |
| 3 | `Model_Medium_Forward` | 1169.94 | 25 | 46.798 | 851.16 |
| 4 | `Model_Small_Inference` | 401.39 | 1 | 401.391 | 140.94 |
| 5 | `Model_Small_GenerateToken` | 401.34 | 25 | 16.054 | 140.94 |
| 6 | `Model_Small_Forward` | 399.08 | 25 | 15.963 | 140.94 |
| 7 | `Model_Medium_Creation` | 123.86 | 1 | 123.865 | 26.42 |
| 8 | `MatMul_512x512` | 121.91 | 1 | 121.909 | 0.01 |
| 9 | `MatMul_Iteration` | 121.35 | 12 | 10.113 | 0.03 |
| 10 | `GELU_1000000` | 99.76 | 1 | 99.760 | 0.01 |
| 11 | `GELU_Iteration` | 89.72 | 20 | 4.486 | 0.00 |
| 12 | `MatMul_256x256` | 30.90 | 1 | 30.899 | 0.01 |
| 13 | `MatMul_64x64` | 26.63 | 1 | 26.629 | 0.07 |
| 14 | `Model_Small_Creation` | 15.05 | 1 | 15.046 | 3.60 |
| 15 | `MatMul_128x128` | 10.25 | 1 | 10.249 | 0.01 |
| 16 | `Softmax_Iteration` | 7.22 | 20 | 0.361 | 0.00 |
| 17 | `Softmax_2048` | 7.05 | 1 | 7.048 | 0.00 |
| 18 | `Softmax_256` | 6.19 | 1 | 6.194 | 0.00 |
| 19 | `GELU_100000` | 5.47 | 1 | 5.466 | 0.00 |
| 20 | `TensorAdd_10000` | 3.14 | 1 | 3.141 | 0.38 |
| 21 | `TensorAdd_Iteration` | 3.13 | 10 | 0.313 | 0.38 |
| 22 | `BroadcastAdd_100x100` | 1.94 | 1 | 1.942 | 0.39 |
| 23 | `BroadcastAdd_Iteration` | 1.92 | 10 | 0.192 | 0.38 |
| 24 | `GELU_10000` | 1.18 | 1 | 1.177 | 0.00 |
| 25 | `TensorMul_10000` | 0.55 | 1 | 0.549 | 0.38 |
| 26 | `TensorMul_Iteration` | 0.54 | 10 | 0.054 | 0.38 |
| 27 | `GELU_1000` | 0.48 | 1 | 0.483 | 0.00 |
| 28 | `Softmax_1024` | 0.19 | 1 | 0.189 | 0.00 |
| 29 | `Softmax_512` | 0.07 | 1 | 0.070 | 0.00 |

## 💾 Top Allocators

| Rank | Method | Total (MB) | Calls | Avg (KB) |
|------|--------|------------|-------|----------|
| 1 | `Model_Medium_Inference` | 851.16 | 1 | 871591.65 |
| 2 | `Model_Medium_GenerateToken` | 851.16 | 25 | 34863.67 |
| 3 | `Model_Medium_Forward` | 851.16 | 25 | 34863.67 |
| 4 | `Model_Small_Inference` | 140.94 | 1 | 144318.70 |
| 5 | `Model_Small_GenerateToken` | 140.94 | 25 | 5772.75 |
| 6 | `Model_Small_Forward` | 140.94 | 25 | 5772.75 |
| 7 | `Model_Medium_Creation` | 26.42 | 1 | 27050.56 |
| 8 | `Model_Small_Creation` | 3.60 | 1 | 3689.51 |
| 9 | `BroadcastAdd_100x100` | 0.39 | 1 | 398.87 |
| 10 | `TensorAdd_10000` | 0.38 | 1 | 390.86 |
| 11 | `TensorAdd_Iteration` | 0.38 | 10 | 39.09 |
| 12 | `TensorMul_10000` | 0.38 | 1 | 390.86 |
| 13 | `TensorMul_Iteration` | 0.38 | 10 | 39.09 |
| 14 | `BroadcastAdd_Iteration` | 0.38 | 10 | 39.09 |
| 15 | `MatMul_64x64` | 0.07 | 1 | 74.22 |

## Analysis

See COMPREHENSIVE_HOT_PATHS_ANALYSIS.md for detailed recommendations.
