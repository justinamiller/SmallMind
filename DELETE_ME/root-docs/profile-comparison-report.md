# Profile Comparison Report

**Previous Run:** 2026-02-03 02:36:36
**Current Run:** 2026-02-04 02:02:49

## 📊 Overall Performance Summary

| Metric | Previous | Current | Delta | Change % |
|--------|----------|---------|-------|----------|
| **Total Runtime** | 5927.60 ms | 9237.16 ms | +3309.56 ms | +55.8% |
| **Total Allocations** | 2550.03 MB | 338.61 MB | -2211.42 MB | -86.7% |
| **Methods Profiled** | 29 | 29 | +0 | - |

### 🎯 Performance Verdict

⚠️ **REGRESSED**: Overall performance degraded by 55.8%

## 🚀 Top 10 Improvements

| Method | Previous (ms) | Current (ms) | Delta (ms) | Change % |
|--------|---------------|--------------|------------|----------|
| `Model_Small_GenerateToken` | 531.59 | 443.88 | -87.71 | -16.5% |
| `Model_Small_Inference` | 531.64 | 443.94 | -87.70 | -16.5% |
| `Model_Small_Forward` | 529.00 | 441.38 | -87.62 | -16.6% |
| `Model_Medium_Creation` | 84.98 | 54.53 | -30.45 | -35.8% |
| `Model_Small_Creation` | 34.51 | 20.21 | -14.30 | -41.4% |
| `TensorAdd_10000` | 10.84 | 2.24 | -8.60 | -79.3% |
| `TensorAdd_Iteration` | 10.83 | 2.23 | -8.60 | -79.4% |
| `Softmax_2048` | 6.22 | 0.26 | -5.96 | -95.8% |
| `Softmax_Iteration` | 6.36 | 0.44 | -5.92 | -93.1% |
| `Softmax_256` | 7.21 | 2.47 | -4.74 | -65.7% |

## ⚠️ Top 10 Regressions

| Method | Previous (ms) | Current (ms) | Delta (ms) | Change % |
|--------|---------------|--------------|------------|----------|
| `MatMul_512x512` | 172.11 | 905.90 | +733.79 | +426.3% |
| `Model_Medium_Forward` | 1200.76 | 1862.82 | +662.06 | +55.1% |
| `Model_Medium_GenerateToken` | 1201.18 | 1863.22 | +662.04 | +55.1% |
| `Model_Medium_Inference` | 1201.28 | 1863.27 | +661.99 | +55.1% |
| `MatMul_Iteration` | 148.10 | 775.87 | +627.77 | +423.9% |
| `GELU_1000000` | 100.60 | 202.40 | +101.80 | +101.2% |
| `GELU_Iteration` | 90.44 | 186.08 | +95.64 | +105.7% |
| `MatMul_256x256` | 19.59 | 112.93 | +93.34 | +476.5% |
| `MatMul_128x128` | 3.54 | 13.29 | +9.75 | +275.4% |
| `GELU_100000` | 11.06 | 20.16 | +9.10 | +82.3% |

## 📋 Detailed Method Comparison

| Method | Prev Time (ms) | Curr Time (ms) | Time Δ | Time Δ% | Prev Alloc (MB) | Curr Alloc (MB) | Status |
|--------|----------------|----------------|--------|---------|-----------------|-----------------|--------|
| `MatMul_512x512` | 172.11 | 905.90 | +733.79 | +426.3% | 0.00 | 0.00 | ⚠️ Regressed |
| `Model_Medium_Forward` | 1200.76 | 1862.82 | +662.06 | +55.1% | 729.96 | 83.10 | ⚠️ Regressed |
| `Model_Medium_GenerateToken` | 1201.18 | 1863.22 | +662.04 | +55.1% | 729.96 | 83.10 | ⚠️ Regressed |
| `Model_Medium_Inference` | 1201.28 | 1863.27 | +661.99 | +55.1% | 729.97 | 83.10 | ⚠️ Regressed |
| `MatMul_Iteration` | 148.10 | 775.87 | +627.77 | +423.9% | 0.00 | 0.00 | ⚠️ Regressed |
| `GELU_1000000` | 100.60 | 202.40 | +101.80 | +101.2% | 0.01 | 0.01 | ⚠️ Regressed |
| `GELU_Iteration` | 90.44 | 186.08 | +95.64 | +105.7% | 0.00 | 0.00 | ⚠️ Regressed |
| `MatMul_256x256` | 19.59 | 112.93 | +93.34 | +476.5% | 0.00 | 0.00 | ⚠️ Regressed |
| `Model_Small_GenerateToken` | 531.59 | 443.88 | -87.71 | -16.5% | 109.26 | 19.00 | ✅ Improved |
| `Model_Small_Inference` | 531.64 | 443.94 | -87.70 | -16.5% | 109.26 | 19.00 | ✅ Improved |
| `Model_Small_Forward` | 529.00 | 441.38 | -87.62 | -16.6% | 109.26 | 19.00 | ✅ Improved |
| `Model_Medium_Creation` | 84.98 | 54.53 | -30.45 | -35.8% | 26.45 | 26.41 | ✅ Improved |
| `Model_Small_Creation` | 34.51 | 20.21 | -14.30 | -41.4% | 3.61 | 3.61 | ✅ Improved |
| `MatMul_128x128` | 3.54 | 13.29 | +9.75 | +275.4% | 0.00 | 0.00 | ⚠️ Regressed |
| `GELU_100000` | 11.06 | 20.16 | +9.10 | +82.3% | 0.01 | 0.00 | ⚠️ Regressed |
| `TensorAdd_10000` | 10.84 | 2.24 | -8.60 | -79.3% | 0.38 | 0.38 | ✅ Improved |
| `TensorAdd_Iteration` | 10.83 | 2.23 | -8.60 | -79.4% | 0.38 | 0.38 | ✅ Improved |
| `Softmax_2048` | 6.22 | 0.26 | -5.96 | -95.8% | 0.00 | 0.00 | ✅ Improved |
| `Softmax_Iteration` | 6.36 | 0.44 | -5.92 | -93.1% | 0.00 | 0.00 | ✅ Improved |
| `Softmax_256` | 7.21 | 2.47 | -4.74 | -65.7% | 0.00 | 0.00 | ✅ Improved |
| `BroadcastAdd_100x100` | 6.93 | 2.40 | -4.53 | -65.4% | 0.38 | 0.38 | ✅ Improved |
| `BroadcastAdd_Iteration` | 6.91 | 2.39 | -4.52 | -65.4% | 0.38 | 0.38 | ✅ Improved |
| `TensorMul_10000` | 0.60 | 1.97 | +1.37 | +228.3% | 0.38 | 0.38 | ⚠️ Regressed |
| `TensorMul_Iteration` | 0.59 | 1.95 | +1.36 | +230.5% | 0.38 | 0.38 | ⚠️ Regressed |
| `GELU_1000` | 2.28 | 1.02 | -1.26 | -55.3% | 0.00 | 0.00 | ✅ Improved |
| `GELU_10000` | 1.17 | 2.30 | +1.13 | +96.6% | 0.00 | 0.00 | ⚠️ Regressed |
| `MatMul_64x64` | 7.07 | 7.39 | +0.32 | +4.5% | 0.00 | 0.00 | ➡️ Unchanged |
| `Softmax_512` | 0.06 | 0.07 | +0.01 | +16.7% | 0.00 | 0.00 | ⚠️ Regressed |
| `Softmax_1024` | 0.15 | 0.15 | +0.00 | +0.0% | 0.00 | 0.00 | ➡️ Unchanged |

## 🔬 Model Size Comparison

### Small vs Medium Model Performance

| Model | Previous (ms) | Current (ms) | Delta | Change % | Alloc (MB) |
|-------|---------------|--------------|-------|----------|------------|
| **Small** (128 dim, 2 layers) | 531.64 | 443.94 | -87.70 | -16.5% | 19.00 |
| **Medium** (256 dim, 4 layers) | 1201.28 | 1863.27 | +661.99 | +55.1% | 83.10 |

### Scaling Analysis

- **Medium/Small Time Ratio:** 4.20x
- **Medium Model Parameters:** ~7.3x more parameters than Small
- **Computational Efficiency:** 1.74x (ideal: 1.0x, higher is better)

### Inference Throughput (25 tokens)

| Model | Tokens/Second | Latency/Token (ms) | Memory/Token (MB) |
|-------|---------------|-------------------|-------------------|
| **Small** | 56.31 | 17.76 | 0.76 |
| **Medium** | 13.42 | 74.53 | 3.32 |

### SIMD Operation Performance

| Operation | Previous (ms) | Current (ms) | Delta | Change % | GFLOPS |
|-----------|---------------|--------------|-------|----------|--------|
| `MatMul_128x128` | 3.54 | 13.29 | +9.75 | +275.4% | 0.32 |
| `MatMul_256x256` | 19.59 | 112.93 | +93.34 | +476.5% | 0.30 |
| `MatMul_512x512` | 172.11 | 905.90 | +733.79 | +426.3% | 0.30 |

