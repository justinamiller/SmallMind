# Enhanced Performance Profile Report

**Generated:** 2026-02-13 16:27:34

## Summary

- **Total Runtime:** 1702.74 ms
- **Total Allocations:** 263.12 MB
- **Methods Profiled:** 29

## 🔥 Hot Paths (Top 30)

| Rank | Method | Time (ms) | Calls | Avg (ms) | Alloc (MB) |
|------|--------|-----------|-------|----------|------------|
| 1 | `Model_Medium_Inference` | 386.02 | 1 | 386.022 | 53.12 |
| 2 | `Model_Medium_GenerateToken` | 385.96 | 25 | 15.438 | 53.12 |
| 3 | `Model_Medium_Forward` | 385.61 | 25 | 15.424 | 53.11 |
| 4 | `Model_Small_Inference` | 129.07 | 1 | 129.073 | 14.40 |
| 5 | `Model_Small_GenerateToken` | 129.03 | 25 | 5.161 | 14.39 |
| 6 | `Model_Small_Forward` | 126.59 | 25 | 5.064 | 14.38 |
| 7 | `Model_Medium_Creation` | 66.01 | 1 | 66.007 | 51.43 |
| 8 | `MatMul_512x512` | 23.77 | 1 | 23.766 | 0.00 |
| 9 | `Model_Small_Creation` | 18.83 | 1 | 18.835 | 6.87 |
| 10 | `MatMul_Iteration` | 18.46 | 12 | 1.538 | 0.01 |
| 11 | `MatMul_64x64` | 10.96 | 1 | 10.959 | 0.01 |
| 12 | `GELU_1000000` | 4.34 | 1 | 4.339 | 0.01 |
| 13 | `MatMul_256x256` | 3.72 | 1 | 3.719 | 0.00 |
| 14 | `GELU_Iteration` | 2.86 | 20 | 0.143 | 0.00 |
| 15 | `TensorAdd_10000` | 1.84 | 1 | 1.835 | 0.38 |
| 16 | `TensorAdd_Iteration` | 1.83 | 10 | 0.183 | 0.38 |
| 17 | `Softmax_256` | 1.82 | 1 | 1.824 | 0.00 |
| 18 | `GELU_1000` | 1.49 | 1 | 1.490 | 0.00 |
| 19 | `BroadcastAdd_100x100` | 1.34 | 1 | 1.342 | 0.38 |
| 20 | `BroadcastAdd_Iteration` | 1.33 | 10 | 0.133 | 0.38 |
| 21 | `GELU_100000` | 0.50 | 1 | 0.495 | 0.00 |
| 22 | `TensorMul_10000` | 0.43 | 1 | 0.431 | 0.38 |
| 23 | `TensorMul_Iteration` | 0.42 | 10 | 0.042 | 0.38 |
| 24 | `MatMul_128x128` | 0.26 | 1 | 0.264 | 0.00 |
| 25 | `Softmax_Iteration` | 0.10 | 20 | 0.005 | 0.00 |
| 26 | `Softmax_2048` | 0.06 | 1 | 0.064 | 0.00 |
| 27 | `Softmax_1024` | 0.03 | 1 | 0.033 | 0.00 |
| 28 | `GELU_10000` | 0.03 | 1 | 0.032 | 0.00 |
| 29 | `Softmax_512` | 0.02 | 1 | 0.019 | 0.00 |

## 💾 Top Allocators

| Rank | Method | Total (MB) | Calls | Avg (KB) |
|------|--------|------------|-------|----------|
| 1 | `Model_Medium_Inference` | 53.12 | 1 | 54390.27 |
| 2 | `Model_Medium_GenerateToken` | 53.12 | 25 | 2175.61 |
| 3 | `Model_Medium_Forward` | 53.11 | 25 | 2175.29 |
| 4 | `Model_Medium_Creation` | 51.43 | 1 | 52662.45 |
| 5 | `Model_Small_Inference` | 14.40 | 1 | 14743.05 |
| 6 | `Model_Small_GenerateToken` | 14.39 | 25 | 589.40 |
| 7 | `Model_Small_Forward` | 14.38 | 25 | 589.08 |
| 8 | `Model_Small_Creation` | 6.87 | 1 | 7032.99 |
| 9 | `TensorAdd_10000` | 0.38 | 1 | 390.86 |
| 10 | `TensorAdd_Iteration` | 0.38 | 10 | 39.09 |
| 11 | `TensorMul_10000` | 0.38 | 1 | 390.86 |
| 12 | `TensorMul_Iteration` | 0.38 | 10 | 39.09 |
| 13 | `BroadcastAdd_100x100` | 0.38 | 1 | 390.86 |
| 14 | `BroadcastAdd_Iteration` | 0.38 | 10 | 39.09 |
| 15 | `GELU_1000000` | 0.01 | 1 | 8.16 |

## Analysis

See COMPREHENSIVE_HOT_PATHS_ANALYSIS.md for detailed recommendations.
