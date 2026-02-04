# Enhanced Performance Profile Report

**Generated:** 2026-02-04 00:59:34

## Summary

- **Total Runtime:** 4035.12 ms
- **Total Allocations:** 2549.56 MB
- **Methods Profiled:** 29

## 🔥 Hot Paths (Top 30)

| Rank | Method | Time (ms) | Calls | Avg (ms) | Alloc (MB) |
|------|--------|-----------|-------|----------|------------|
| 1 | `Model_Medium_Inference` | 928.21 | 1 | 928.209 | 729.82 |
| 2 | `Model_Medium_GenerateToken` | 928.13 | 25 | 37.125 | 729.82 |
| 3 | `Model_Medium_Forward` | 927.75 | 25 | 37.110 | 729.82 |
| 4 | `Model_Small_Inference` | 278.74 | 1 | 278.736 | 109.26 |
| 5 | `Model_Small_GenerateToken` | 278.71 | 25 | 11.148 | 109.26 |
| 6 | `Model_Small_Forward` | 276.53 | 25 | 11.061 | 109.26 |
| 7 | `MatMul_512x512` | 128.99 | 1 | 128.988 | 0.00 |
| 8 | `MatMul_Iteration` | 111.69 | 12 | 9.308 | 0.00 |
| 9 | `Model_Medium_Creation` | 49.31 | 1 | 49.309 | 26.40 |
| 10 | `GELU_1000000` | 36.23 | 1 | 36.226 | 0.01 |
| 11 | `GELU_Iteration` | 33.16 | 20 | 1.658 | 0.00 |
| 12 | `Model_Small_Creation` | 15.64 | 1 | 15.643 | 3.61 |
| 13 | `MatMul_256x256` | 13.35 | 1 | 13.350 | 0.00 |
| 14 | `MatMul_64x64` | 4.97 | 1 | 4.975 | 0.00 |
| 15 | `GELU_100000` | 3.18 | 1 | 3.178 | 0.00 |
| 16 | `TensorAdd_10000` | 2.92 | 1 | 2.917 | 0.38 |
| 17 | `TensorAdd_Iteration` | 2.91 | 10 | 0.291 | 0.38 |
| 18 | `Softmax_Iteration` | 2.16 | 20 | 0.108 | 0.00 |
| 19 | `MatMul_128x128` | 2.14 | 1 | 2.142 | 0.00 |
| 20 | `Softmax_256` | 2.04 | 1 | 2.037 | 0.00 |
| 21 | `Softmax_2048` | 2.03 | 1 | 2.031 | 0.00 |
| 22 | `BroadcastAdd_100x100` | 1.79 | 1 | 1.793 | 0.39 |
| 23 | `BroadcastAdd_Iteration` | 1.78 | 10 | 0.178 | 0.39 |
| 24 | `GELU_10000` | 0.83 | 1 | 0.827 | 0.00 |
| 25 | `GELU_1000` | 0.66 | 1 | 0.659 | 0.00 |
| 26 | `TensorMul_10000` | 0.54 | 1 | 0.544 | 0.38 |
| 27 | `TensorMul_Iteration` | 0.54 | 10 | 0.054 | 0.38 |
| 28 | `Softmax_1024` | 0.14 | 1 | 0.143 | 0.00 |
| 29 | `Softmax_512` | 0.06 | 1 | 0.061 | 0.00 |

## 💾 Top Allocators

| Rank | Method | Total (MB) | Calls | Avg (KB) |
|------|--------|------------|-------|----------|
| 1 | `Model_Medium_Inference` | 729.82 | 1 | 747332.01 |
| 2 | `Model_Medium_GenerateToken` | 729.82 | 25 | 29893.28 |
| 3 | `Model_Medium_Forward` | 729.82 | 25 | 29893.28 |
| 4 | `Model_Small_Inference` | 109.26 | 1 | 111881.88 |
| 5 | `Model_Small_GenerateToken` | 109.26 | 25 | 4475.27 |
| 6 | `Model_Small_Forward` | 109.26 | 25 | 4475.27 |
| 7 | `Model_Medium_Creation` | 26.40 | 1 | 27035.99 |
| 8 | `Model_Small_Creation` | 3.61 | 1 | 3697.93 |
| 9 | `BroadcastAdd_100x100` | 0.39 | 1 | 398.87 |
| 10 | `BroadcastAdd_Iteration` | 0.39 | 10 | 39.89 |
| 11 | `TensorAdd_10000` | 0.38 | 1 | 390.86 |
| 12 | `TensorAdd_Iteration` | 0.38 | 10 | 39.09 |
| 13 | `TensorMul_10000` | 0.38 | 1 | 390.86 |
| 14 | `TensorMul_Iteration` | 0.38 | 10 | 39.09 |
| 15 | `GELU_1000000` | 0.01 | 1 | 8.16 |

## Analysis

See COMPREHENSIVE_HOT_PATHS_ANALYSIS.md for detailed recommendations.
