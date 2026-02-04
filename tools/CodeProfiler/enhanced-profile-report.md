# Enhanced Performance Profile Report

**Generated:** 2026-02-04 04:41:24

## Summary

- **Total Runtime:** 1686.71 ms
- **Total Allocations:** 339.00 MB
- **Methods Profiled:** 29

## 🔥 Hot Paths (Top 30)

| Rank | Method | Time (ms) | Calls | Avg (ms) | Alloc (MB) |
|------|--------|-----------|-------|----------|------------|
| 1 | `Model_Medium_Inference` | 337.03 | 1 | 337.027 | 83.16 |
| 2 | `Model_Medium_GenerateToken` | 336.98 | 25 | 13.479 | 83.16 |
| 3 | `Model_Medium_Forward` | 336.80 | 25 | 13.472 | 83.16 |
| 4 | `Model_Small_Inference` | 130.34 | 1 | 130.339 | 19.01 |
| 5 | `Model_Small_GenerateToken` | 130.32 | 25 | 5.213 | 19.01 |
| 6 | `Model_Small_Forward` | 128.74 | 25 | 5.150 | 19.00 |
| 7 | `MatMul_512x512` | 73.28 | 1 | 73.284 | 0.01 |
| 8 | `MatMul_Iteration` | 68.78 | 12 | 5.732 | 0.03 |
| 9 | `GELU_1000000` | 30.76 | 1 | 30.762 | 0.01 |
| 10 | `GELU_Iteration` | 27.59 | 20 | 1.379 | 0.00 |
| 11 | `Model_Medium_Creation` | 25.11 | 1 | 25.113 | 26.42 |
| 12 | `MatMul_64x64` | 16.08 | 1 | 16.085 | 0.07 |
| 13 | `MatMul_256x256` | 12.13 | 1 | 12.127 | 0.01 |
| 14 | `Model_Small_Creation` | 10.27 | 1 | 10.270 | 3.61 |
| 15 | `MatMul_128x128` | 4.38 | 1 | 4.384 | 0.01 |
| 16 | `GELU_100000` | 4.32 | 1 | 4.317 | 0.00 |
| 17 | `TensorAdd_10000` | 1.89 | 1 | 1.886 | 0.38 |
| 18 | `TensorAdd_Iteration` | 1.88 | 10 | 0.188 | 0.38 |
| 19 | `GELU_10000` | 1.70 | 1 | 1.700 | 0.00 |
| 20 | `GELU_1000` | 1.52 | 1 | 1.520 | 0.00 |
| 21 | `BroadcastAdd_100x100` | 1.29 | 1 | 1.287 | 0.39 |
| 22 | `BroadcastAdd_Iteration` | 1.28 | 10 | 0.128 | 0.39 |
| 23 | `Softmax_Iteration` | 1.27 | 20 | 0.063 | 0.00 |
| 24 | `Softmax_2048` | 1.13 | 1 | 1.127 | 0.00 |
| 25 | `Softmax_256` | 0.88 | 1 | 0.882 | 0.00 |
| 26 | `TensorMul_10000` | 0.37 | 1 | 0.372 | 0.38 |
| 27 | `TensorMul_Iteration` | 0.37 | 10 | 0.037 | 0.38 |
| 28 | `Softmax_1024` | 0.16 | 1 | 0.159 | 0.00 |
| 29 | `Softmax_512` | 0.06 | 1 | 0.061 | 0.00 |

## 💾 Top Allocators

| Rank | Method | Total (MB) | Calls | Avg (KB) |
|------|--------|------------|-------|----------|
| 1 | `Model_Medium_Inference` | 83.16 | 1 | 85159.70 |
| 2 | `Model_Medium_GenerateToken` | 83.16 | 25 | 3406.39 |
| 3 | `Model_Medium_Forward` | 83.16 | 25 | 3406.39 |
| 4 | `Model_Medium_Creation` | 26.42 | 1 | 27057.45 |
| 5 | `Model_Small_Inference` | 19.01 | 1 | 19468.76 |
| 6 | `Model_Small_GenerateToken` | 19.01 | 25 | 778.75 |
| 7 | `Model_Small_Forward` | 19.00 | 25 | 778.43 |
| 8 | `Model_Small_Creation` | 3.61 | 1 | 3697.93 |
| 9 | `BroadcastAdd_100x100` | 0.39 | 1 | 398.87 |
| 10 | `BroadcastAdd_Iteration` | 0.39 | 10 | 39.89 |
| 11 | `TensorAdd_10000` | 0.38 | 1 | 390.86 |
| 12 | `TensorAdd_Iteration` | 0.38 | 10 | 39.09 |
| 13 | `TensorMul_10000` | 0.38 | 1 | 390.86 |
| 14 | `TensorMul_Iteration` | 0.38 | 10 | 39.09 |
| 15 | `MatMul_64x64` | 0.07 | 1 | 74.16 |

## Analysis

See COMPREHENSIVE_HOT_PATHS_ANALYSIS.md for detailed recommendations.
