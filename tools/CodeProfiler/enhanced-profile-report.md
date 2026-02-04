# Enhanced Performance Profile Report

**Generated:** 2026-02-04 01:19:58

## Summary

- **Total Runtime:** 3983.95 ms
- **Total Allocations:** 2549.45 MB
- **Methods Profiled:** 29

## 🔥 Hot Paths (Top 30)

| Rank | Method | Time (ms) | Calls | Avg (ms) | Alloc (MB) |
|------|--------|-----------|-------|----------|------------|
| 1 | `Model_Medium_Inference` | 836.88 | 1 | 836.881 | 729.80 |
| 2 | `Model_Medium_GenerateToken` | 836.81 | 25 | 33.472 | 729.80 |
| 3 | `Model_Medium_Forward` | 836.40 | 25 | 33.456 | 729.80 |
| 4 | `Model_Small_Inference` | 315.74 | 1 | 315.736 | 109.24 |
| 5 | `Model_Small_GenerateToken` | 315.68 | 25 | 12.627 | 109.23 |
| 6 | `Model_Small_Forward` | 313.08 | 25 | 12.523 | 109.23 |
| 7 | `MatMul_512x512` | 172.19 | 1 | 172.186 | 0.00 |
| 8 | `MatMul_Iteration` | 144.19 | 12 | 12.016 | 0.00 |
| 9 | `GELU_1000000` | 52.82 | 1 | 52.824 | 0.01 |
| 10 | `GELU_Iteration` | 47.96 | 20 | 2.398 | 0.00 |
| 11 | `Model_Medium_Creation` | 38.51 | 1 | 38.505 | 26.42 |
| 12 | `MatMul_256x256` | 19.57 | 1 | 19.572 | 0.00 |
| 13 | `Model_Small_Creation` | 17.48 | 1 | 17.480 | 3.61 |
| 14 | `MatMul_64x64` | 6.82 | 1 | 6.820 | 0.00 |
| 15 | `GELU_100000` | 5.56 | 1 | 5.557 | 0.00 |
| 16 | `MatMul_128x128` | 3.50 | 1 | 3.503 | 0.00 |
| 17 | `TensorAdd_10000` | 3.31 | 1 | 3.306 | 0.38 |
| 18 | `TensorAdd_Iteration` | 3.29 | 10 | 0.329 | 0.38 |
| 19 | `Softmax_Iteration` | 2.27 | 20 | 0.113 | 0.00 |
| 20 | `Softmax_256` | 2.21 | 1 | 2.214 | 0.00 |
| 21 | `Softmax_2048` | 2.11 | 1 | 2.110 | 0.00 |
| 22 | `BroadcastAdd_100x100` | 1.95 | 1 | 1.950 | 0.39 |
| 23 | `BroadcastAdd_Iteration` | 1.93 | 10 | 0.193 | 0.39 |
| 24 | `GELU_10000` | 1.15 | 1 | 1.152 | 0.00 |
| 25 | `GELU_1000` | 1.07 | 1 | 1.070 | 0.00 |
| 26 | `TensorMul_10000` | 0.63 | 1 | 0.633 | 0.38 |
| 27 | `TensorMul_Iteration` | 0.62 | 10 | 0.062 | 0.38 |
| 28 | `Softmax_1024` | 0.15 | 1 | 0.152 | 0.00 |
| 29 | `Softmax_512` | 0.06 | 1 | 0.064 | 0.00 |

## 💾 Top Allocators

| Rank | Method | Total (MB) | Calls | Avg (KB) |
|------|--------|------------|-------|----------|
| 1 | `Model_Medium_Inference` | 729.80 | 1 | 747317.16 |
| 2 | `Model_Medium_GenerateToken` | 729.80 | 25 | 29892.69 |
| 3 | `Model_Medium_Forward` | 729.80 | 25 | 29892.69 |
| 4 | `Model_Small_Inference` | 109.24 | 1 | 111863.63 |
| 5 | `Model_Small_GenerateToken` | 109.23 | 25 | 4474.23 |
| 6 | `Model_Small_Forward` | 109.23 | 25 | 4474.23 |
| 7 | `Model_Medium_Creation` | 26.42 | 1 | 27050.09 |
| 8 | `Model_Small_Creation` | 3.61 | 1 | 3696.56 |
| 9 | `BroadcastAdd_100x100` | 0.39 | 1 | 397.96 |
| 10 | `BroadcastAdd_Iteration` | 0.39 | 10 | 39.80 |
| 11 | `TensorAdd_10000` | 0.38 | 1 | 390.86 |
| 12 | `TensorAdd_Iteration` | 0.38 | 10 | 39.09 |
| 13 | `TensorMul_10000` | 0.38 | 1 | 390.86 |
| 14 | `TensorMul_Iteration` | 0.38 | 10 | 39.09 |
| 15 | `GELU_1000000` | 0.01 | 1 | 8.01 |

## Analysis

See COMPREHENSIVE_HOT_PATHS_ANALYSIS.md for detailed recommendations.
