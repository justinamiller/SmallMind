# Enhanced Performance Profile Report

**Generated:** 2026-02-03 02:23:06

## Summary

- **Total Runtime:** 4272.80 ms
- **Total Allocations:** 2599.16 MB
- **Methods Profiled:** 29

## 🔥 Hot Paths (Top 30)

| Rank | Method | Time (ms) | Calls | Avg (ms) | Alloc (MB) |
|------|--------|-----------|-------|----------|------------|
| 1 | `Model_Medium_Inference` | 984.91 | 1 | 984.914 | 743.02 |
| 2 | `Model_Medium_GenerateToken` | 982.75 | 25 | 39.310 | 743.02 |
| 3 | `Model_Medium_Forward` | 982.34 | 25 | 39.294 | 743.02 |
| 4 | `Model_Small_Inference` | 303.30 | 1 | 303.301 | 112.55 |
| 5 | `Model_Small_GenerateToken` | 303.26 | 25 | 12.130 | 112.55 |
| 6 | `Model_Small_Forward` | 300.93 | 25 | 12.037 | 112.55 |
| 7 | `MatMul_512x512` | 79.51 | 1 | 79.506 | 0.01 |
| 8 | `MatMul_Iteration` | 78.86 | 12 | 6.571 | 0.03 |
| 9 | `Model_Medium_Creation` | 69.58 | 1 | 69.576 | 26.43 |
| 10 | `GELU_1000000` | 52.85 | 1 | 52.852 | 0.01 |
| 11 | `GELU_Iteration` | 47.45 | 20 | 2.372 | 0.00 |
| 12 | `MatMul_64x64` | 24.24 | 1 | 24.242 | 0.07 |
| 13 | `MatMul_256x256` | 16.53 | 1 | 16.525 | 0.01 |
| 14 | `Model_Small_Creation` | 15.65 | 1 | 15.649 | 3.60 |
| 15 | `MatMul_128x128` | 5.68 | 1 | 5.682 | 0.01 |
| 16 | `GELU_100000` | 5.48 | 1 | 5.482 | 0.00 |
| 17 | `TensorAdd_10000` | 3.00 | 1 | 2.995 | 0.38 |
| 18 | `TensorAdd_Iteration` | 2.98 | 10 | 0.298 | 0.38 |
| 19 | `Softmax_256` | 2.25 | 1 | 2.246 | 0.00 |
| 20 | `Softmax_Iteration` | 2.23 | 20 | 0.111 | 0.00 |
| 21 | `Softmax_2048` | 2.09 | 1 | 2.087 | 0.00 |
| 22 | `BroadcastAdd_100x100` | 1.97 | 1 | 1.966 | 0.38 |
| 23 | `BroadcastAdd_Iteration` | 1.95 | 10 | 0.195 | 0.38 |
| 24 | `GELU_10000` | 1.16 | 1 | 1.155 | 0.00 |
| 25 | `TensorMul_10000` | 0.57 | 1 | 0.574 | 0.38 |
| 26 | `TensorMul_Iteration` | 0.56 | 10 | 0.056 | 0.38 |
| 27 | `GELU_1000` | 0.50 | 1 | 0.502 | 0.00 |
| 28 | `Softmax_1024` | 0.16 | 1 | 0.157 | 0.00 |
| 29 | `Softmax_512` | 0.07 | 1 | 0.067 | 0.00 |

## 💾 Top Allocators

| Rank | Method | Total (MB) | Calls | Avg (KB) |
|------|--------|------------|-------|----------|
| 1 | `Model_Medium_Inference` | 743.02 | 1 | 760848.84 |
| 2 | `Model_Medium_GenerateToken` | 743.02 | 25 | 30433.95 |
| 3 | `Model_Medium_Forward` | 743.02 | 25 | 30433.95 |
| 4 | `Model_Small_Inference` | 112.55 | 1 | 115252.90 |
| 5 | `Model_Small_GenerateToken` | 112.55 | 25 | 4610.12 |
| 6 | `Model_Small_Forward` | 112.55 | 25 | 4610.12 |
| 7 | `Model_Medium_Creation` | 26.43 | 1 | 27063.60 |
| 8 | `Model_Small_Creation` | 3.60 | 1 | 3689.92 |
| 9 | `TensorAdd_10000` | 0.38 | 1 | 390.86 |
| 10 | `TensorAdd_Iteration` | 0.38 | 10 | 39.09 |
| 11 | `TensorMul_10000` | 0.38 | 1 | 390.86 |
| 12 | `TensorMul_Iteration` | 0.38 | 10 | 39.09 |
| 13 | `BroadcastAdd_100x100` | 0.38 | 1 | 390.86 |
| 14 | `BroadcastAdd_Iteration` | 0.38 | 10 | 39.09 |
| 15 | `MatMul_64x64` | 0.07 | 1 | 74.16 |

## Analysis

See COMPREHENSIVE_HOT_PATHS_ANALYSIS.md for detailed recommendations.
