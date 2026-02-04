# Enhanced Performance Profile Report

**Generated:** 2026-02-04 02:28:08

## Summary

- **Total Runtime:** 2931.41 ms
- **Total Allocations:** 338.49 MB
- **Methods Profiled:** 29

## 🔥 Hot Paths (Top 30)

| Rank | Method | Time (ms) | Calls | Avg (ms) | Alloc (MB) |
|------|--------|-----------|-------|----------|------------|
| 1 | `Model_Medium_Inference` | 557.34 | 1 | 557.335 | 83.07 |
| 2 | `Model_Medium_GenerateToken` | 557.25 | 25 | 22.290 | 83.07 |
| 3 | `Model_Medium_Forward` | 556.86 | 25 | 22.274 | 83.07 |
| 4 | `Model_Small_Inference` | 256.39 | 1 | 256.392 | 18.94 |
| 5 | `Model_Small_GenerateToken` | 256.36 | 25 | 10.254 | 18.94 |
| 6 | `Model_Small_Forward` | 254.82 | 25 | 10.193 | 18.93 |
| 7 | `MatMul_512x512` | 109.98 | 1 | 109.985 | 0.02 |
| 8 | `MatMul_Iteration` | 103.94 | 12 | 8.661 | 0.03 |
| 9 | `GELU_1000000` | 74.27 | 1 | 74.269 | 0.01 |
| 10 | `GELU_Iteration` | 58.56 | 20 | 2.928 | 0.00 |
| 11 | `Model_Medium_Creation` | 58.47 | 1 | 58.470 | 26.44 |
| 12 | `MatMul_128x128` | 21.32 | 1 | 21.319 | 0.07 |
| 13 | `MatMul_256x256` | 20.77 | 1 | 20.773 | 0.01 |
| 14 | `Model_Small_Creation` | 10.49 | 1 | 10.489 | 3.61 |
| 15 | `MatMul_64x64` | 8.04 | 1 | 8.042 | 0.00 |
| 16 | `GELU_100000` | 5.72 | 1 | 5.721 | 0.00 |
| 17 | `TensorAdd_10000` | 3.97 | 1 | 3.967 | 0.38 |
| 18 | `TensorAdd_Iteration` | 3.95 | 10 | 0.395 | 0.38 |
| 19 | `GELU_1000` | 2.32 | 1 | 2.323 | 0.00 |
| 20 | `GELU_10000` | 2.00 | 1 | 1.995 | 0.00 |
| 21 | `Softmax_Iteration` | 1.73 | 20 | 0.086 | 0.00 |
| 22 | `Softmax_2048` | 1.58 | 1 | 1.584 | 0.00 |
| 23 | `BroadcastAdd_100x100` | 1.42 | 1 | 1.418 | 0.38 |
| 24 | `BroadcastAdd_Iteration` | 1.41 | 10 | 0.141 | 0.38 |
| 25 | `Softmax_256` | 1.02 | 1 | 1.024 | 0.00 |
| 26 | `TensorMul_10000` | 0.61 | 1 | 0.611 | 0.38 |
| 27 | `TensorMul_Iteration` | 0.60 | 10 | 0.060 | 0.38 |
| 28 | `Softmax_1024` | 0.16 | 1 | 0.161 | 0.00 |
| 29 | `Softmax_512` | 0.06 | 1 | 0.062 | 0.00 |

## 💾 Top Allocators

| Rank | Method | Total (MB) | Calls | Avg (KB) |
|------|--------|------------|-------|----------|
| 1 | `Model_Medium_Inference` | 83.07 | 1 | 85058.95 |
| 2 | `Model_Medium_GenerateToken` | 83.07 | 25 | 3402.36 |
| 3 | `Model_Medium_Forward` | 83.07 | 25 | 3402.36 |
| 4 | `Model_Medium_Creation` | 26.44 | 1 | 27071.34 |
| 5 | `Model_Small_Inference` | 18.94 | 1 | 19399.45 |
| 6 | `Model_Small_GenerateToken` | 18.94 | 25 | 775.66 |
| 7 | `Model_Small_Forward` | 18.93 | 25 | 775.34 |
| 8 | `Model_Small_Creation` | 3.61 | 1 | 3697.52 |
| 9 | `TensorAdd_10000` | 0.38 | 1 | 390.86 |
| 10 | `TensorAdd_Iteration` | 0.38 | 10 | 39.09 |
| 11 | `TensorMul_10000` | 0.38 | 1 | 390.86 |
| 12 | `TensorMul_Iteration` | 0.38 | 10 | 39.09 |
| 13 | `BroadcastAdd_100x100` | 0.38 | 1 | 390.86 |
| 14 | `BroadcastAdd_Iteration` | 0.38 | 10 | 39.09 |
| 15 | `MatMul_128x128` | 0.07 | 1 | 73.46 |

## Analysis

See COMPREHENSIVE_HOT_PATHS_ANALYSIS.md for detailed recommendations.
