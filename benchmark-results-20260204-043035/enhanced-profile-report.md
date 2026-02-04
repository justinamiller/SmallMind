# Enhanced Performance Profile Report

**Generated:** 2026-02-04 03:32:44

## Summary

- **Total Runtime:** 3445.90 ms
- **Total Allocations:** 338.90 MB
- **Methods Profiled:** 29

## 🔥 Hot Paths (Top 30)

| Rank | Method | Time (ms) | Calls | Avg (ms) | Alloc (MB) |
|------|--------|-----------|-------|----------|------------|
| 1 | `Model_Medium_Inference` | 668.30 | 1 | 668.301 | 83.13 |
| 2 | `Model_Medium_GenerateToken` | 668.21 | 25 | 26.728 | 83.13 |
| 3 | `Model_Medium_Forward` | 667.83 | 25 | 26.713 | 83.13 |
| 4 | `Model_Small_Inference` | 299.67 | 1 | 299.671 | 19.01 |
| 5 | `Model_Small_GenerateToken` | 299.64 | 25 | 11.986 | 19.01 |
| 6 | `Model_Small_Forward` | 297.45 | 25 | 11.898 | 19.00 |
| 7 | `MatMul_512x512` | 108.16 | 1 | 108.162 | 0.02 |
| 8 | `MatMul_Iteration` | 101.76 | 12 | 8.480 | 0.04 |
| 9 | `GELU_1000000` | 91.96 | 1 | 91.955 | 0.01 |
| 10 | `GELU_Iteration` | 81.53 | 20 | 4.076 | 0.00 |
| 11 | `Model_Medium_Creation` | 57.45 | 1 | 57.451 | 26.43 |
| 12 | `MatMul_256x256` | 25.91 | 1 | 25.906 | 0.02 |
| 13 | `MatMul_64x64` | 25.71 | 1 | 25.711 | 0.07 |
| 14 | `Model_Small_Creation` | 15.87 | 1 | 15.874 | 3.61 |
| 15 | `MatMul_128x128` | 10.49 | 1 | 10.489 | 0.01 |
| 16 | `GELU_100000` | 7.11 | 1 | 7.113 | 0.00 |
| 17 | `TensorAdd_10000` | 2.73 | 1 | 2.728 | 0.38 |
| 18 | `TensorAdd_Iteration` | 2.72 | 10 | 0.272 | 0.38 |
| 19 | `GELU_10000` | 2.48 | 1 | 2.478 | 0.00 |
| 20 | `BroadcastAdd_100x100` | 1.88 | 1 | 1.884 | 0.39 |
| 21 | `BroadcastAdd_Iteration` | 1.86 | 10 | 0.186 | 0.38 |
| 22 | `Softmax_Iteration` | 1.69 | 20 | 0.084 | 0.00 |
| 23 | `Softmax_2048` | 1.53 | 1 | 1.530 | 0.00 |
| 24 | `Softmax_256` | 1.35 | 1 | 1.347 | 0.00 |
| 25 | `GELU_1000` | 1.28 | 1 | 1.278 | 0.00 |
| 26 | `TensorMul_10000` | 0.56 | 1 | 0.561 | 0.38 |
| 27 | `TensorMul_Iteration` | 0.55 | 10 | 0.055 | 0.38 |
| 28 | `Softmax_1024` | 0.17 | 1 | 0.169 | 0.00 |
| 29 | `Softmax_512` | 0.06 | 1 | 0.063 | 0.00 |

## 💾 Top Allocators

| Rank | Method | Total (MB) | Calls | Avg (KB) |
|------|--------|------------|-------|----------|
| 1 | `Model_Medium_Inference` | 83.13 | 1 | 85125.92 |
| 2 | `Model_Medium_GenerateToken` | 83.13 | 25 | 3405.04 |
| 3 | `Model_Medium_Forward` | 83.13 | 25 | 3405.04 |
| 4 | `Model_Medium_Creation` | 26.43 | 1 | 27061.63 |
| 5 | `Model_Small_Inference` | 19.01 | 1 | 19461.75 |
| 6 | `Model_Small_GenerateToken` | 19.01 | 25 | 778.47 |
| 7 | `Model_Small_Forward` | 19.00 | 25 | 778.15 |
| 8 | `Model_Small_Creation` | 3.61 | 1 | 3697.52 |
| 9 | `BroadcastAdd_100x100` | 0.39 | 1 | 398.87 |
| 10 | `TensorAdd_10000` | 0.38 | 1 | 390.86 |
| 11 | `TensorAdd_Iteration` | 0.38 | 10 | 39.09 |
| 12 | `TensorMul_10000` | 0.38 | 1 | 390.86 |
| 13 | `TensorMul_Iteration` | 0.38 | 10 | 39.09 |
| 14 | `BroadcastAdd_Iteration` | 0.38 | 10 | 39.09 |
| 15 | `MatMul_64x64` | 0.07 | 1 | 74.10 |

## Analysis

See COMPREHENSIVE_HOT_PATHS_ANALYSIS.md for detailed recommendations.
