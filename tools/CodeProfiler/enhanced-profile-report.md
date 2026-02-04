# Enhanced Performance Profile Report

**Generated:** 2026-02-04 04:30:56

## Summary

- **Total Runtime:** 2697.91 ms
- **Total Allocations:** 338.96 MB
- **Methods Profiled:** 29

## 🔥 Hot Paths (Top 30)

| Rank | Method | Time (ms) | Calls | Avg (ms) | Alloc (MB) |
|------|--------|-----------|-------|----------|------------|
| 1 | `Model_Medium_Inference` | 514.11 | 1 | 514.113 | 83.16 |
| 2 | `Model_Medium_GenerateToken` | 514.05 | 25 | 20.562 | 83.16 |
| 3 | `Model_Medium_Forward` | 513.72 | 25 | 20.549 | 83.16 |
| 4 | `Model_Small_Inference` | 250.49 | 1 | 250.485 | 19.00 |
| 5 | `Model_Small_GenerateToken` | 250.45 | 25 | 10.018 | 19.00 |
| 6 | `Model_Small_Forward` | 248.35 | 25 | 9.934 | 18.99 |
| 7 | `MatMul_512x512` | 86.33 | 1 | 86.331 | 0.02 |
| 8 | `MatMul_Iteration` | 76.41 | 12 | 6.367 | 0.04 |
| 9 | `Model_Medium_Creation` | 62.85 | 1 | 62.848 | 26.43 |
| 10 | `GELU_1000000` | 54.76 | 1 | 54.763 | 0.01 |
| 11 | `GELU_Iteration` | 49.70 | 20 | 2.485 | 0.01 |
| 12 | `MatMul_64x64` | 15.78 | 1 | 15.777 | 0.07 |
| 13 | `Model_Small_Creation` | 15.70 | 1 | 15.695 | 3.61 |
| 14 | `MatMul_256x256` | 13.84 | 1 | 13.845 | 0.01 |
| 15 | `GELU_100000` | 7.51 | 1 | 7.512 | 0.01 |
| 16 | `MatMul_128x128` | 4.89 | 1 | 4.893 | 0.01 |
| 17 | `TensorAdd_10000` | 2.66 | 1 | 2.662 | 0.38 |
| 18 | `TensorAdd_Iteration` | 2.65 | 10 | 0.265 | 0.38 |
| 19 | `GELU_10000` | 2.61 | 1 | 2.606 | 0.00 |
| 20 | `BroadcastAdd_100x100` | 1.85 | 1 | 1.846 | 0.38 |
| 21 | `BroadcastAdd_Iteration` | 1.84 | 10 | 0.184 | 0.38 |
| 22 | `Softmax_Iteration` | 1.61 | 20 | 0.080 | 0.00 |
| 23 | `GELU_1000` | 1.60 | 1 | 1.597 | 0.00 |
| 24 | `Softmax_2048` | 1.47 | 1 | 1.474 | 0.00 |
| 25 | `Softmax_256` | 1.29 | 1 | 1.292 | 0.00 |
| 26 | `TensorMul_10000` | 0.60 | 1 | 0.597 | 0.38 |
| 27 | `TensorMul_Iteration` | 0.59 | 10 | 0.059 | 0.38 |
| 28 | `Softmax_1024` | 0.15 | 1 | 0.149 | 0.00 |
| 29 | `Softmax_512` | 0.06 | 1 | 0.062 | 0.00 |

## 💾 Top Allocators

| Rank | Method | Total (MB) | Calls | Avg (KB) |
|------|--------|------------|-------|----------|
| 1 | `Model_Medium_Inference` | 83.16 | 1 | 85151.59 |
| 2 | `Model_Medium_GenerateToken` | 83.16 | 25 | 3406.06 |
| 3 | `Model_Medium_Forward` | 83.16 | 25 | 3406.06 |
| 4 | `Model_Medium_Creation` | 26.43 | 1 | 27066.75 |
| 5 | `Model_Small_Inference` | 19.00 | 1 | 19454.71 |
| 6 | `Model_Small_GenerateToken` | 19.00 | 25 | 778.19 |
| 7 | `Model_Small_Forward` | 18.99 | 25 | 777.87 |
| 8 | `Model_Small_Creation` | 3.61 | 1 | 3697.73 |
| 9 | `TensorAdd_10000` | 0.38 | 1 | 390.86 |
| 10 | `TensorAdd_Iteration` | 0.38 | 10 | 39.09 |
| 11 | `TensorMul_10000` | 0.38 | 1 | 390.86 |
| 12 | `TensorMul_Iteration` | 0.38 | 10 | 39.09 |
| 13 | `BroadcastAdd_100x100` | 0.38 | 1 | 390.86 |
| 14 | `BroadcastAdd_Iteration` | 0.38 | 10 | 39.09 |
| 15 | `MatMul_64x64` | 0.07 | 1 | 74.22 |

## Analysis

See COMPREHENSIVE_HOT_PATHS_ANALYSIS.md for detailed recommendations.
