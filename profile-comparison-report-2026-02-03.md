# Profile Comparison Report

**Previous Run:** 2026-02-03 02:36:36
**Current Run:** 2026-02-03 23:07:42

## 📊 Overall Performance Summary

| Metric | Previous | Current | Delta | Change % |
|--------|----------|---------|-------|----------|
| **Total Runtime** | 5591.91 ms | 5927.60 ms | +335.69 ms | +6.0% |
| **Total Allocations** | 2566.93 MB | 2550.03 MB | -16.90 MB | -0.7% |
| **Methods Profiled** | 29 | 29 | +0 | - |

### 🎯 Performance Verdict

➡️ **STABLE**: Performance remained within 10% tolerance (+F1%)

## 🚀 Top 10 Improvements

| Method | Previous (ms) | Current (ms) | Delta (ms) | Change % |
|--------|---------------|--------------|------------|----------|
| `Model_Medium_Creation` | 107.09 | 84.98 | -22.11 | -20.6% |
| `MatMul_64x64` | 23.49 | 7.07 | -16.42 | -69.9% |
| `MatMul_256x256` | 29.17 | 19.59 | -9.58 | -32.8% |
| `MatMul_128x128` | 9.85 | 3.54 | -6.31 | -64.1% |
| `GELU_10000` | 1.43 | 1.17 | -0.26 | -18.2% |
| `Softmax_512` | 0.11 | 0.06 | -0.05 | -45.5% |
| `Softmax_1024` | 0.16 | 0.15 | -0.01 | -6.3% |

## ⚠️ Top 10 Regressions

| Method | Previous (ms) | Current (ms) | Delta (ms) | Change % |
|--------|---------------|--------------|------------|----------|
| `Model_Small_GenerateToken` | 453.49 | 531.59 | +78.10 | +17.2% |
| `Model_Small_Inference` | 453.56 | 531.64 | +78.08 | +17.2% |
| `Model_Small_Forward` | 451.24 | 529.00 | +77.76 | +17.2% |
| `MatMul_512x512` | 119.89 | 172.11 | +52.22 | +43.6% |
| `GELU_1000000` | 59.22 | 100.60 | +41.38 | +69.9% |
| `GELU_Iteration` | 53.93 | 90.44 | +36.51 | +67.7% |
| `MatMul_Iteration` | 124.23 | 148.10 | +23.87 | +19.2% |
| `Model_Small_Creation` | 15.62 | 34.51 | +18.89 | +120.9% |
| `TensorAdd_10000` | 2.94 | 10.84 | +7.90 | +268.7% |
| `TensorAdd_Iteration` | 2.93 | 10.83 | +7.90 | +269.6% |

## 📋 Detailed Method Comparison

| Method | Prev Time (ms) | Curr Time (ms) | Time Δ | Time Δ% | Prev Alloc (MB) | Curr Alloc (MB) | Status |
|--------|----------------|----------------|--------|---------|-----------------|-----------------|--------|
| `Model_Small_GenerateToken` | 453.49 | 531.59 | +78.10 | +17.2% | 110.35 | 109.26 | ⚠️ Regressed |
| `Model_Small_Inference` | 453.56 | 531.64 | +78.08 | +17.2% | 110.35 | 109.26 | ⚠️ Regressed |
| `Model_Small_Forward` | 451.24 | 529.00 | +77.76 | +17.2% | 110.35 | 109.26 | ⚠️ Regressed |
| `MatMul_512x512` | 119.89 | 172.11 | +52.22 | +43.6% | 0.02 | 0.00 | ⚠️ Regressed |
| `GELU_1000000` | 59.22 | 100.60 | +41.38 | +69.9% | 0.01 | 0.01 | ⚠️ Regressed |
| `GELU_Iteration` | 53.93 | 90.44 | +36.51 | +67.7% | 0.00 | 0.00 | ⚠️ Regressed |
| `MatMul_Iteration` | 124.23 | 148.10 | +23.87 | +19.2% | 0.02 | 0.00 | ⚠️ Regressed |
| `Model_Medium_Creation` | 107.09 | 84.98 | -22.11 | -20.6% | 26.41 | 26.45 | ✅ Improved |
| `Model_Medium_GenerateToken` | 1221.89 | 1201.18 | -20.71 | -1.7% | 734.48 | 729.96 | ➡️ Unchanged |
| `Model_Medium_Inference` | 1221.98 | 1201.28 | -20.70 | -1.7% | 734.48 | 729.97 | ➡️ Unchanged |
| `Model_Medium_Forward` | 1221.40 | 1200.76 | -20.64 | -1.7% | 734.48 | 729.96 | ➡️ Unchanged |
| `Model_Small_Creation` | 15.62 | 34.51 | +18.89 | +120.9% | 3.61 | 3.61 | ⚠️ Regressed |
| `MatMul_64x64` | 23.49 | 7.07 | -16.42 | -69.9% | 0.07 | 0.00 | ✅ Improved |
| `MatMul_256x256` | 29.17 | 19.59 | -9.58 | -32.8% | 0.01 | 0.00 | ✅ Improved |
| `TensorAdd_10000` | 2.94 | 10.84 | +7.90 | +268.7% | 0.38 | 0.38 | ⚠️ Regressed |
| `TensorAdd_Iteration` | 2.93 | 10.83 | +7.90 | +269.6% | 0.38 | 0.38 | ⚠️ Regressed |
| `MatMul_128x128` | 9.85 | 3.54 | -6.31 | -64.1% | 0.01 | 0.00 | ✅ Improved |
| `Softmax_256` | 2.26 | 7.21 | +4.95 | +219.0% | 0.00 | 0.00 | ⚠️ Regressed |
| `BroadcastAdd_100x100` | 1.99 | 6.93 | +4.94 | +248.2% | 0.38 | 0.38 | ⚠️ Regressed |
| `BroadcastAdd_Iteration` | 1.98 | 6.91 | +4.93 | +249.0% | 0.38 | 0.38 | ⚠️ Regressed |
| `GELU_100000` | 6.22 | 11.06 | +4.84 | +77.8% | 0.00 | 0.01 | ⚠️ Regressed |
| `Softmax_2048` | 2.01 | 6.22 | +4.21 | +209.5% | 0.00 | 0.00 | ⚠️ Regressed |
| `Softmax_Iteration` | 2.21 | 6.36 | +4.15 | +187.8% | 0.00 | 0.00 | ⚠️ Regressed |
| `GELU_1000` | 0.55 | 2.28 | +1.73 | +314.5% | 0.00 | 0.00 | ⚠️ Regressed |
| `GELU_10000` | 1.43 | 1.17 | -0.26 | -18.2% | 0.00 | 0.00 | ✅ Improved |
| `TensorMul_10000` | 0.54 | 0.60 | +0.06 | +11.1% | 0.38 | 0.38 | ⚠️ Regressed |
| `TensorMul_Iteration` | 0.53 | 0.59 | +0.06 | +11.3% | 0.38 | 0.38 | ⚠️ Regressed |
| `Softmax_512` | 0.11 | 0.06 | -0.05 | -45.5% | 0.00 | 0.00 | ✅ Improved |
| `Softmax_1024` | 0.16 | 0.15 | -0.01 | -6.3% | 0.00 | 0.00 | ✅ Improved |

## 🔬 Model Size Comparison

### Small vs Medium Model Performance

| Model | Previous (ms) | Current (ms) | Delta | Change % | Alloc (MB) |
|-------|---------------|--------------|-------|----------|------------|
| **Small** (128 dim, 2 layers) | 453.56 | 531.64 | +78.08 | +17.2% | 109.26 |
| **Medium** (256 dim, 4 layers) | 1221.98 | 1201.28 | -20.70 | -1.7% | 729.97 |

### Scaling Analysis

- **Medium/Small Time Ratio:** 2.26x
- **Medium Model Parameters:** ~7.3x more parameters than Small
- **Computational Efficiency:** 3.23x (ideal: 1.0x, higher is better)

### Inference Throughput (25 tokens)

| Model | Tokens/Second | Latency/Token (ms) | Memory/Token (MB) |
|-------|---------------|-------------------|-------------------|
| **Small** | 47.02 | 21.27 | 4.37 |
| **Medium** | 20.81 | 48.05 | 29.20 |

### SIMD Operation Performance

| Operation | Previous (ms) | Current (ms) | Delta | Change % | GFLOPS |
|-----------|---------------|--------------|-------|----------|--------|
| `MatMul_128x128` | 9.85 | 3.54 | -6.31 | -64.1% | 1.18 |
| `MatMul_256x256` | 29.17 | 19.59 | -9.58 | -32.8% | 1.71 |
| `MatMul_512x512` | 119.89 | 172.11 | +52.22 | +43.6% | 1.56 |

