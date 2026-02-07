# Enhanced Performance Profile Report

**Generated:** 2026-02-07 07:53:57

## Summary

- **Total Runtime:** 3353.36 ms
- **Total Allocations:** 263.11 MB
- **Methods Profiled:** 29

## 🔥 Hot Paths (Top 30)

| Rank | Method | Time (ms) | Calls | Avg (ms) | Alloc (MB) |
|------|--------|-----------|-------|----------|------------|
| 1 | `Model_Medium_Inference` | 459.93 | 1 | 459.928 | 53.12 |
| 2 | `Model_Medium_GenerateToken` | 459.88 | 25 | 18.395 | 53.10 |
| 3 | `Model_Medium_Forward` | 459.58 | 25 | 18.383 | 53.10 |
| 4 | `Model_Small_Inference` | 413.65 | 1 | 413.654 | 14.35 |
| 5 | `Model_Small_GenerateToken` | 413.59 | 25 | 16.544 | 14.35 |
| 6 | `Model_Small_Forward` | 411.14 | 25 | 16.446 | 14.33 |
| 7 | `MatMul_512x512` | 243.65 | 1 | 243.649 | 0.02 |
| 8 | `MatMul_Iteration` | 225.20 | 12 | 18.767 | 0.03 |
| 9 | `Model_Medium_Creation` | 74.11 | 1 | 74.111 | 51.45 |
| 10 | `MatMul_128x128` | 48.82 | 1 | 48.821 | 0.07 |
| 11 | `MatMul_256x256` | 45.65 | 1 | 45.648 | 0.01 |
| 12 | `Model_Small_Creation` | 22.92 | 1 | 22.922 | 6.86 |
| 13 | `MatMul_64x64` | 17.10 | 1 | 17.102 | 0.00 |
| 14 | `GELU_1000000` | 9.63 | 1 | 9.626 | 0.01 |
| 15 | `GELU_Iteration` | 7.98 | 20 | 0.399 | 0.01 |
| 16 | `TensorAdd_10000` | 6.17 | 1 | 6.167 | 0.38 |
| 17 | `TensorAdd_Iteration` | 6.15 | 10 | 0.615 | 0.38 |
| 18 | `Softmax_Iteration` | 6.14 | 20 | 0.307 | 0.00 |
| 19 | `Softmax_2048` | 6.02 | 1 | 6.020 | 0.00 |
| 20 | `GELU_1000` | 5.29 | 1 | 5.291 | 0.00 |
| 21 | `Softmax_256` | 4.25 | 1 | 4.252 | 0.00 |
| 22 | `BroadcastAdd_100x100` | 1.94 | 1 | 1.941 | 0.38 |
| 23 | `BroadcastAdd_Iteration` | 1.92 | 10 | 0.192 | 0.38 |
| 24 | `GELU_100000` | 1.23 | 1 | 1.233 | 0.01 |
| 25 | `TensorMul_10000` | 0.58 | 1 | 0.582 | 0.38 |
| 26 | `TensorMul_Iteration` | 0.57 | 10 | 0.057 | 0.38 |
| 27 | `Softmax_1024` | 0.14 | 1 | 0.138 | 0.00 |
| 28 | `Softmax_512` | 0.06 | 1 | 0.061 | 0.00 |
| 29 | `GELU_10000` | 0.06 | 1 | 0.057 | 0.00 |

## 💾 Top Allocators

| Rank | Method | Total (MB) | Calls | Avg (KB) |
|------|--------|------------|-------|----------|
| 1 | `Model_Medium_Inference` | 53.12 | 1 | 54390.47 |
| 2 | `Model_Medium_GenerateToken` | 53.10 | 25 | 2174.98 |
| 3 | `Model_Medium_Forward` | 53.10 | 25 | 2174.98 |
| 4 | `Model_Medium_Creation` | 51.45 | 1 | 52683.85 |
| 5 | `Model_Small_Inference` | 14.35 | 1 | 14698.74 |
| 6 | `Model_Small_GenerateToken` | 14.35 | 25 | 587.95 |
| 7 | `Model_Small_Forward` | 14.33 | 25 | 586.99 |
| 8 | `Model_Small_Creation` | 6.86 | 1 | 7026.15 |
| 9 | `TensorAdd_10000` | 0.38 | 1 | 390.86 |
| 10 | `TensorAdd_Iteration` | 0.38 | 10 | 39.09 |
| 11 | `TensorMul_10000` | 0.38 | 1 | 390.86 |
| 12 | `TensorMul_Iteration` | 0.38 | 10 | 39.09 |
| 13 | `BroadcastAdd_100x100` | 0.38 | 1 | 390.86 |
| 14 | `BroadcastAdd_Iteration` | 0.38 | 10 | 39.09 |
| 15 | `MatMul_128x128` | 0.07 | 1 | 73.89 |

## Analysis

See COMPREHENSIVE_HOT_PATHS_ANALYSIS.md for detailed recommendations.
