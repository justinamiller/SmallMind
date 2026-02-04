# Enhanced Performance Profile Report

**Generated:** 2026-02-04 04:56:24

## Summary

- **Total Runtime:** 2934.95 ms
- **Total Allocations:** 337.82 MB
- **Methods Profiled:** 29

## 🔥 Hot Paths (Top 30)

| Rank | Method | Time (ms) | Calls | Avg (ms) | Alloc (MB) |
|------|--------|-----------|-------|----------|------------|
| 1 | `Model_Medium_Inference` | 511.67 | 1 | 511.667 | 82.88 |
| 2 | `Model_Medium_GenerateToken` | 511.59 | 25 | 20.464 | 82.88 |
| 3 | `Model_Medium_Forward` | 511.27 | 25 | 20.451 | 82.87 |
| 4 | `Model_Small_Inference` | 239.55 | 1 | 239.549 | 18.91 |
| 5 | `Model_Small_GenerateToken` | 239.51 | 25 | 9.581 | 18.91 |
| 6 | `Model_Small_Forward` | 237.29 | 25 | 9.492 | 18.90 |
| 7 | `MatMul_512x512` | 150.17 | 1 | 150.165 | 0.02 |
| 8 | `MatMul_Iteration` | 134.74 | 12 | 11.228 | 0.02 |
| 9 | `GELU_1000000` | 105.47 | 1 | 105.470 | 0.01 |
| 10 | `Model_Medium_Creation` | 95.48 | 1 | 95.478 | 26.43 |
| 11 | `GELU_Iteration` | 95.36 | 20 | 4.768 | 0.00 |
| 12 | `MatMul_128x128` | 27.51 | 1 | 27.512 | 0.07 |
| 13 | `MatMul_256x256` | 26.01 | 1 | 26.012 | 0.01 |
| 14 | `Model_Small_Creation` | 15.43 | 1 | 15.431 | 3.61 |
| 15 | `MatMul_64x64` | 7.53 | 1 | 7.526 | 0.00 |
| 16 | `GELU_100000` | 7.14 | 1 | 7.145 | 0.00 |
| 17 | `TensorAdd_10000` | 2.81 | 1 | 2.810 | 0.38 |
| 18 | `TensorAdd_Iteration` | 2.80 | 10 | 0.280 | 0.38 |
| 19 | `GELU_10000` | 2.63 | 1 | 2.633 | 0.00 |
| 20 | `BroadcastAdd_100x100` | 1.87 | 1 | 1.872 | 0.39 |
| 21 | `BroadcastAdd_Iteration` | 1.85 | 10 | 0.185 | 0.38 |
| 22 | `Softmax_Iteration` | 1.71 | 20 | 0.086 | 0.00 |
| 23 | `Softmax_2048` | 1.58 | 1 | 1.579 | 0.00 |
| 24 | `Softmax_256` | 1.36 | 1 | 1.365 | 0.00 |
| 25 | `GELU_1000` | 1.28 | 1 | 1.283 | 0.00 |
| 26 | `TensorMul_10000` | 0.56 | 1 | 0.560 | 0.38 |
| 27 | `TensorMul_Iteration` | 0.55 | 10 | 0.055 | 0.38 |
| 28 | `Softmax_1024` | 0.15 | 1 | 0.152 | 0.00 |
| 29 | `Softmax_512` | 0.06 | 1 | 0.064 | 0.00 |

## 💾 Top Allocators

| Rank | Method | Total (MB) | Calls | Avg (KB) |
|------|--------|------------|-------|----------|
| 1 | `Model_Medium_Inference` | 82.88 | 1 | 84870.02 |
| 2 | `Model_Medium_GenerateToken` | 82.88 | 25 | 3394.80 |
| 3 | `Model_Medium_Forward` | 82.87 | 25 | 3394.48 |
| 4 | `Model_Medium_Creation` | 26.43 | 1 | 27065.16 |
| 5 | `Model_Small_Inference` | 18.91 | 1 | 19360.37 |
| 6 | `Model_Small_GenerateToken` | 18.91 | 25 | 774.41 |
| 7 | `Model_Small_Forward` | 18.90 | 25 | 774.09 |
| 8 | `Model_Small_Creation` | 3.61 | 1 | 3697.52 |
| 9 | `BroadcastAdd_100x100` | 0.39 | 1 | 398.87 |
| 10 | `TensorAdd_10000` | 0.38 | 1 | 390.86 |
| 11 | `TensorAdd_Iteration` | 0.38 | 10 | 39.09 |
| 12 | `TensorMul_10000` | 0.38 | 1 | 390.86 |
| 13 | `TensorMul_Iteration` | 0.38 | 10 | 39.09 |
| 14 | `BroadcastAdd_Iteration` | 0.38 | 10 | 39.09 |
| 15 | `MatMul_128x128` | 0.07 | 1 | 74.02 |

## Analysis

See COMPREHENSIVE_HOT_PATHS_ANALYSIS.md for detailed recommendations.
