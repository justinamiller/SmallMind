# Enhanced Performance Profile Report

**Generated:** 2026-02-04 02:36:17

## Summary

- **Total Runtime:** 9277.80 ms
- **Total Allocations:** 338.71 MB
- **Methods Profiled:** 29

## 🔥 Hot Paths (Top 30)

| Rank | Method | Time (ms) | Calls | Avg (ms) | Alloc (MB) |
|------|--------|-----------|-------|----------|------------|
| 1 | `Model_Medium_Inference` | 2186.63 | 1 | 2186.634 | 83.12 |
| 2 | `Model_Medium_GenerateToken` | 2186.56 | 25 | 87.463 | 83.12 |
| 3 | `Model_Medium_Forward` | 2186.21 | 25 | 87.448 | 83.11 |
| 4 | `MatMul_512x512` | 449.13 | 1 | 449.134 | 0.02 |
| 5 | `Model_Small_Inference` | 427.71 | 1 | 427.709 | 18.97 |
| 6 | `Model_Small_GenerateToken` | 427.63 | 25 | 17.105 | 18.97 |
| 7 | `Model_Small_Forward` | 425.27 | 25 | 17.011 | 18.96 |
| 8 | `MatMul_Iteration` | 390.54 | 12 | 32.545 | 0.02 |
| 9 | `GELU_1000000` | 163.03 | 1 | 163.034 | 0.01 |
| 10 | `GELU_Iteration` | 153.75 | 20 | 7.687 | 0.00 |
| 11 | `Model_Medium_Creation` | 139.84 | 1 | 139.840 | 26.42 |
| 12 | `MatMul_256x256` | 56.52 | 1 | 56.523 | 0.01 |
| 13 | `Model_Small_Creation` | 24.60 | 1 | 24.597 | 3.61 |
| 14 | `MatMul_128x128` | 20.00 | 1 | 20.005 | 0.07 |
| 15 | `GELU_100000` | 17.18 | 1 | 17.179 | 0.00 |
| 16 | `MatMul_64x64` | 4.97 | 1 | 4.968 | 0.00 |
| 17 | `GELU_1000` | 2.72 | 1 | 2.721 | 0.00 |
| 18 | `BroadcastAdd_100x100` | 2.19 | 1 | 2.191 | 0.39 |
| 19 | `BroadcastAdd_Iteration` | 2.16 | 10 | 0.216 | 0.39 |
| 20 | `GELU_10000` | 2.06 | 1 | 2.061 | 0.00 |
| 21 | `TensorAdd_10000` | 1.82 | 1 | 1.824 | 0.38 |
| 22 | `TensorAdd_Iteration` | 1.81 | 10 | 0.181 | 0.38 |
| 23 | `TensorMul_10000` | 1.69 | 1 | 1.692 | 0.38 |
| 24 | `TensorMul_Iteration` | 1.68 | 10 | 0.168 | 0.38 |
| 25 | `Softmax_256` | 1.35 | 1 | 1.347 | 0.00 |
| 26 | `Softmax_Iteration` | 0.34 | 20 | 0.017 | 0.00 |
| 27 | `Softmax_2048` | 0.23 | 1 | 0.232 | 0.00 |
| 28 | `Softmax_1024` | 0.10 | 1 | 0.102 | 0.00 |
| 29 | `Softmax_512` | 0.06 | 1 | 0.064 | 0.00 |

## 💾 Top Allocators

| Rank | Method | Total (MB) | Calls | Avg (KB) |
|------|--------|------------|-------|----------|
| 1 | `Model_Medium_Inference` | 83.12 | 1 | 85117.36 |
| 2 | `Model_Medium_GenerateToken` | 83.12 | 25 | 3404.69 |
| 3 | `Model_Medium_Forward` | 83.11 | 25 | 3404.38 |
| 4 | `Model_Medium_Creation` | 26.42 | 1 | 27049.35 |
| 5 | `Model_Small_Inference` | 18.97 | 1 | 19422.23 |
| 6 | `Model_Small_GenerateToken` | 18.97 | 25 | 776.89 |
| 7 | `Model_Small_Forward` | 18.96 | 25 | 776.57 |
| 8 | `Model_Small_Creation` | 3.61 | 1 | 3697.93 |
| 9 | `BroadcastAdd_100x100` | 0.39 | 1 | 398.87 |
| 10 | `BroadcastAdd_Iteration` | 0.39 | 10 | 39.89 |
| 11 | `TensorAdd_10000` | 0.38 | 1 | 390.86 |
| 12 | `TensorAdd_Iteration` | 0.38 | 10 | 39.09 |
| 13 | `TensorMul_10000` | 0.38 | 1 | 390.86 |
| 14 | `TensorMul_Iteration` | 0.38 | 10 | 39.09 |
| 15 | `MatMul_128x128` | 0.07 | 1 | 74.00 |

## Analysis

See COMPREHENSIVE_HOT_PATHS_ANALYSIS.md for detailed recommendations.
