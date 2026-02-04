# Enhanced Performance Profile Report

**Generated:** 2026-02-04 04:52:23

## Summary

- **Total Runtime:** 3484.85 ms
- **Total Allocations:** 338.91 MB
- **Methods Profiled:** 29

## 🔥 Hot Paths (Top 30)

| Rank | Method | Time (ms) | Calls | Avg (ms) | Alloc (MB) |
|------|--------|-----------|-------|----------|------------|
| 1 | `Model_Medium_Inference` | 669.71 | 1 | 669.712 | 83.14 |
| 2 | `Model_Medium_GenerateToken` | 669.64 | 25 | 26.785 | 83.14 |
| 3 | `Model_Medium_Forward` | 669.27 | 25 | 26.771 | 83.14 |
| 4 | `Model_Small_Inference` | 264.84 | 1 | 264.839 | 19.01 |
| 5 | `Model_Small_GenerateToken` | 264.81 | 25 | 10.592 | 19.01 |
| 6 | `Model_Small_Forward` | 262.64 | 25 | 10.506 | 19.00 |
| 7 | `Model_Medium_Creation` | 120.00 | 1 | 120.004 | 26.42 |
| 8 | `GELU_1000000` | 108.24 | 1 | 108.236 | 0.01 |
| 9 | `MatMul_Iteration` | 107.35 | 12 | 8.946 | 0.03 |
| 10 | `MatMul_512x512` | 105.80 | 1 | 105.796 | 0.02 |
| 11 | `GELU_Iteration` | 97.91 | 20 | 4.895 | 0.00 |
| 12 | `MatMul_64x64` | 25.15 | 1 | 25.148 | 0.07 |
| 13 | `MatMul_256x256` | 25.02 | 1 | 25.017 | 0.02 |
| 14 | `Model_Small_Creation` | 22.78 | 1 | 22.782 | 3.61 |
| 15 | `GELU_100000` | 15.99 | 1 | 15.990 | 0.00 |
| 16 | `MatMul_128x128` | 10.32 | 1 | 10.316 | 0.01 |
| 17 | `TensorAdd_10000` | 7.79 | 1 | 7.791 | 0.38 |
| 18 | `TensorAdd_Iteration` | 7.78 | 10 | 0.778 | 0.38 |
| 19 | `Softmax_256` | 6.35 | 1 | 6.355 | 0.00 |
| 20 | `BroadcastAdd_100x100` | 5.90 | 1 | 5.896 | 0.39 |
| 21 | `BroadcastAdd_Iteration` | 5.88 | 10 | 0.588 | 0.39 |
| 22 | `GELU_10000` | 5.60 | 1 | 5.600 | 0.00 |
| 23 | `Softmax_Iteration` | 1.77 | 20 | 0.088 | 0.00 |
| 24 | `Softmax_2048` | 1.63 | 1 | 1.632 | 0.00 |
| 25 | `GELU_1000` | 1.32 | 1 | 1.315 | 0.00 |
| 26 | `TensorMul_10000` | 0.58 | 1 | 0.582 | 0.38 |
| 27 | `TensorMul_Iteration` | 0.57 | 10 | 0.057 | 0.38 |
| 28 | `Softmax_1024` | 0.15 | 1 | 0.152 | 0.00 |
| 29 | `Softmax_512` | 0.06 | 1 | 0.062 | 0.00 |

## 💾 Top Allocators

| Rank | Method | Total (MB) | Calls | Avg (KB) |
|------|--------|------------|-------|----------|
| 1 | `Model_Medium_Inference` | 83.14 | 1 | 85130.48 |
| 2 | `Model_Medium_GenerateToken` | 83.14 | 25 | 3405.22 |
| 3 | `Model_Medium_Forward` | 83.14 | 25 | 3405.22 |
| 4 | `Model_Medium_Creation` | 26.42 | 1 | 27058.77 |
| 5 | `Model_Small_Inference` | 19.01 | 1 | 19462.15 |
| 6 | `Model_Small_GenerateToken` | 19.01 | 25 | 778.49 |
| 7 | `Model_Small_Forward` | 19.00 | 25 | 778.17 |
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
