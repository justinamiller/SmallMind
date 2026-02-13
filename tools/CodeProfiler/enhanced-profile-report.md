# Enhanced Performance Profile Report

**Generated:** 2026-02-13 16:01:42

## Summary

- **Total Runtime:** 2405.14 ms
- **Total Allocations:** 263.18 MB
- **Methods Profiled:** 29

## 🔥 Hot Paths (Top 30)

| Rank | Method | Time (ms) | Calls | Avg (ms) | Alloc (MB) |
|------|--------|-----------|-------|----------|------------|
| 1 | `Model_Medium_Inference` | 582.35 | 1 | 582.350 | 53.14 |
| 2 | `Model_Medium_GenerateToken` | 582.29 | 25 | 23.291 | 53.14 |
| 3 | `Model_Medium_Forward` | 581.97 | 25 | 23.279 | 53.13 |
| 4 | `Model_Small_Inference` | 149.01 | 1 | 149.008 | 14.39 |
| 5 | `Model_Small_GenerateToken` | 148.98 | 25 | 5.959 | 14.39 |
| 6 | `Model_Small_Forward` | 146.76 | 25 | 5.870 | 14.37 |
| 7 | `Model_Medium_Creation` | 75.42 | 1 | 75.424 | 51.44 |
| 8 | `MatMul_512x512` | 39.57 | 1 | 39.565 | 0.00 |
| 9 | `MatMul_Iteration` | 31.15 | 12 | 2.596 | 0.01 |
| 10 | `Model_Small_Creation` | 21.16 | 1 | 21.164 | 6.87 |
| 11 | `MatMul_64x64` | 12.47 | 1 | 12.471 | 0.01 |
| 12 | `MatMul_256x256` | 5.91 | 1 | 5.915 | 0.00 |
| 13 | `GELU_1000000` | 5.76 | 1 | 5.759 | 0.01 |
| 14 | `GELU_Iteration` | 4.06 | 20 | 0.203 | 0.01 |
| 15 | `TensorAdd_10000` | 2.45 | 1 | 2.455 | 0.38 |
| 16 | `TensorAdd_Iteration` | 2.45 | 10 | 0.245 | 0.38 |
| 17 | `BroadcastAdd_100x100` | 2.01 | 1 | 2.005 | 0.38 |
| 18 | `Softmax_256` | 2.00 | 1 | 1.999 | 0.00 |
| 19 | `BroadcastAdd_Iteration` | 1.96 | 10 | 0.196 | 0.38 |
| 20 | `Softmax_Iteration` | 1.77 | 20 | 0.088 | 0.00 |
| 21 | `Softmax_2048` | 1.64 | 1 | 1.645 | 0.00 |
| 22 | `GELU_1000` | 1.41 | 1 | 1.408 | 0.00 |
| 23 | `TensorMul_10000` | 0.65 | 1 | 0.649 | 0.38 |
| 24 | `TensorMul_Iteration` | 0.64 | 10 | 0.064 | 0.38 |
| 25 | `GELU_100000` | 0.62 | 1 | 0.620 | 0.00 |
| 26 | `MatMul_128x128` | 0.46 | 1 | 0.455 | 0.00 |
| 27 | `Softmax_1024` | 0.14 | 1 | 0.141 | 0.00 |
| 28 | `Softmax_512` | 0.05 | 1 | 0.047 | 0.00 |
| 29 | `GELU_10000` | 0.04 | 1 | 0.043 | 0.00 |

## 💾 Top Allocators

| Rank | Method | Total (MB) | Calls | Avg (KB) |
|------|--------|------------|-------|----------|
| 1 | `Model_Medium_Inference` | 53.14 | 1 | 54419.67 |
| 2 | `Model_Medium_GenerateToken` | 53.14 | 25 | 2176.47 |
| 3 | `Model_Medium_Forward` | 53.13 | 25 | 2176.15 |
| 4 | `Model_Medium_Creation` | 51.44 | 1 | 52670.49 |
| 5 | `Model_Small_Inference` | 14.39 | 1 | 14731.30 |
| 6 | `Model_Small_GenerateToken` | 14.39 | 25 | 589.25 |
| 7 | `Model_Small_Forward` | 14.37 | 25 | 588.61 |
| 8 | `Model_Small_Creation` | 6.87 | 1 | 7033.01 |
| 9 | `TensorAdd_10000` | 0.38 | 1 | 390.86 |
| 10 | `TensorAdd_Iteration` | 0.38 | 10 | 39.09 |
| 11 | `TensorMul_10000` | 0.38 | 1 | 390.86 |
| 12 | `TensorMul_Iteration` | 0.38 | 10 | 39.09 |
| 13 | `BroadcastAdd_100x100` | 0.38 | 1 | 390.86 |
| 14 | `BroadcastAdd_Iteration` | 0.38 | 10 | 39.09 |
| 15 | `MatMul_64x64` | 0.01 | 1 | 8.01 |

## Analysis

See COMPREHENSIVE_HOT_PATHS_ANALYSIS.md for detailed recommendations.
