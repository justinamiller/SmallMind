# Profile Comparison Report

**Previous Run:** 2026-02-03 02:36:36
**Current Run:** 2026-02-03 02:48:48

## 📊 Overall Performance Summary

| Metric | Previous | Current | Delta | Change % |
|--------|----------|---------|-------|----------|
| **Total Runtime** | 5591.91 ms | 10312.68 ms | +4720.77 ms | +84.4% |
| **Total Allocations** | 2566.93 MB | 2566.90 MB | -0.03 MB | -0.0% |
| **Methods Profiled** | 29 | 29 | +0 | - |

### 🎯 Performance Verdict

⚠️ **REGRESSED**: Overall performance degraded by 84.4%

## 🚀 Top 10 Improvements

| Method | Previous (ms) | Current (ms) | Delta (ms) | Change % |
|--------|---------------|--------------|------------|----------|
| `Model_Medium_Creation` | 107.09 | 82.17 | -24.92 | -23.3% |
| `MatMul_64x64` | 23.49 | 16.13 | -7.36 | -31.3% |
| `MatMul_128x128` | 9.85 | 7.04 | -2.81 | -28.5% |
| `Softmax_Iteration` | 2.21 | 0.40 | -1.81 | -81.9% |
| `Softmax_2048` | 2.01 | 0.27 | -1.74 | -86.6% |
| `TensorAdd_Iteration` | 2.93 | 2.28 | -0.65 | -22.2% |
| `TensorAdd_10000` | 2.94 | 2.29 | -0.65 | -22.1% |
| `BroadcastAdd_100x100` | 1.99 | 1.37 | -0.62 | -31.2% |
| `BroadcastAdd_Iteration` | 1.98 | 1.36 | -0.62 | -31.3% |
| `Softmax_512` | 0.11 | 0.08 | -0.03 | -27.3% |

## ⚠️ Top 10 Regressions

| Method | Previous (ms) | Current (ms) | Delta (ms) | Change % |
|--------|---------------|--------------|------------|----------|
| `Model_Medium_Forward` | 1221.40 | 2553.49 | +1332.09 | +109.1% |
| `Model_Medium_GenerateToken` | 1221.89 | 2553.92 | +1332.03 | +109.0% |
| `Model_Medium_Inference` | 1221.98 | 2553.97 | +1331.99 | +109.0% |
| `MatMul_512x512` | 119.89 | 414.73 | +294.84 | +245.9% |
| `MatMul_Iteration` | 124.23 | 341.18 | +216.95 | +174.6% |
| `GELU_1000000` | 59.22 | 178.07 | +118.85 | +200.7% |
| `GELU_Iteration` | 53.93 | 161.51 | +107.58 | +199.5% |
| `MatMul_256x256` | 29.17 | 53.01 | +23.84 | +81.7% |
| `GELU_100000` | 6.22 | 20.41 | +14.19 | +228.1% |
| `Model_Small_Creation` | 15.62 | 20.08 | +4.46 | +28.6% |

## 📋 Detailed Method Comparison

| Method | Prev Time (ms) | Curr Time (ms) | Time Δ | Time Δ% | Prev Alloc (MB) | Curr Alloc (MB) | Status |
|--------|----------------|----------------|--------|---------|-----------------|-----------------|--------|
| `Model_Medium_Forward` | 1221.40 | 2553.49 | +1332.09 | +109.1% | 734.48 | 734.48 | ⚠️ Regressed |
| `Model_Medium_GenerateToken` | 1221.89 | 2553.92 | +1332.03 | +109.0% | 734.48 | 734.48 | ⚠️ Regressed |
| `Model_Medium_Inference` | 1221.98 | 2553.97 | +1331.99 | +109.0% | 734.48 | 734.48 | ⚠️ Regressed |
| `MatMul_512x512` | 119.89 | 414.73 | +294.84 | +245.9% | 0.02 | 0.02 | ⚠️ Regressed |
| `MatMul_Iteration` | 124.23 | 341.18 | +216.95 | +174.6% | 0.02 | 0.02 | ⚠️ Regressed |
| `GELU_1000000` | 59.22 | 178.07 | +118.85 | +200.7% | 0.01 | 0.01 | ⚠️ Regressed |
| `GELU_Iteration` | 53.93 | 161.51 | +107.58 | +199.5% | 0.00 | 0.00 | ⚠️ Regressed |
| `Model_Medium_Creation` | 107.09 | 82.17 | -24.92 | -23.3% | 26.41 | 26.42 | ✅ Improved |
| `MatMul_256x256` | 29.17 | 53.01 | +23.84 | +81.7% | 0.01 | 0.01 | ⚠️ Regressed |
| `GELU_100000` | 6.22 | 20.41 | +14.19 | +228.1% | 0.00 | 0.00 | ⚠️ Regressed |
| `MatMul_64x64` | 23.49 | 16.13 | -7.36 | -31.3% | 0.07 | 0.07 | ✅ Improved |
| `Model_Small_Inference` | 453.56 | 447.73 | -5.83 | -1.3% | 110.35 | 110.33 | ➡️ Unchanged |
| `Model_Small_GenerateToken` | 453.49 | 447.66 | -5.83 | -1.3% | 110.35 | 110.33 | ➡️ Unchanged |
| `Model_Small_Forward` | 451.24 | 445.85 | -5.39 | -1.2% | 110.35 | 110.33 | ➡️ Unchanged |
| `Model_Small_Creation` | 15.62 | 20.08 | +4.46 | +28.6% | 3.61 | 3.61 | ⚠️ Regressed |
| `MatMul_128x128` | 9.85 | 7.04 | -2.81 | -28.5% | 0.01 | 0.01 | ✅ Improved |
| `Softmax_Iteration` | 2.21 | 0.40 | -1.81 | -81.9% | 0.00 | 0.00 | ✅ Improved |
| `Softmax_2048` | 2.01 | 0.27 | -1.74 | -86.6% | 0.00 | 0.00 | ✅ Improved |
| `TensorAdd_Iteration` | 2.93 | 2.28 | -0.65 | -22.2% | 0.38 | 0.38 | ✅ Improved |
| `TensorAdd_10000` | 2.94 | 2.29 | -0.65 | -22.1% | 0.38 | 0.38 | ✅ Improved |
| `BroadcastAdd_100x100` | 1.99 | 1.37 | -0.62 | -31.2% | 0.38 | 0.39 | ✅ Improved |
| `BroadcastAdd_Iteration` | 1.98 | 1.36 | -0.62 | -31.3% | 0.38 | 0.39 | ✅ Improved |
| `GELU_10000` | 1.43 | 2.03 | +0.60 | +42.0% | 0.00 | 0.00 | ⚠️ Regressed |
| `TensorMul_10000` | 0.54 | 1.07 | +0.53 | +98.1% | 0.38 | 0.38 | ⚠️ Regressed |
| `TensorMul_Iteration` | 0.53 | 1.06 | +0.53 | +100.0% | 0.38 | 0.38 | ⚠️ Regressed |
| `GELU_1000` | 0.55 | 1.01 | +0.46 | +83.6% | 0.00 | 0.00 | ⚠️ Regressed |
| `Softmax_256` | 2.26 | 2.37 | +0.11 | +4.9% | 0.00 | 0.00 | ➡️ Unchanged |
| `Softmax_512` | 0.11 | 0.08 | -0.03 | -27.3% | 0.00 | 0.00 | ✅ Improved |
| `Softmax_1024` | 0.16 | 0.14 | -0.02 | -12.5% | 0.00 | 0.00 | ✅ Improved |

## 🔬 Model Size Comparison

### Small vs Medium Model Performance

| Model | Previous (ms) | Current (ms) | Delta | Change % | Alloc (MB) |
|-------|---------------|--------------|-------|----------|------------|
| **Small** (128 dim, 2 layers) | 453.56 | 447.73 | -5.83 | -1.3% | 110.33 |
| **Medium** (256 dim, 4 layers) | 1221.98 | 2553.97 | +1331.99 | +109.0% | 734.48 |

### Scaling Analysis

- **Medium/Small Time Ratio:** 5.70x
- **Medium Model Parameters:** ~7.3x more parameters than Small
- **Computational Efficiency:** 1.28x (ideal: 1.0x, higher is better)

### Inference Throughput (25 tokens)

| Model | Tokens/Second | Latency/Token (ms) | Memory/Token (MB) |
|-------|---------------|-------------------|-------------------|
| **Small** | 55.84 | 17.91 | 4.41 |
| **Medium** | 9.79 | 102.16 | 29.38 |

### SIMD Operation Performance

| Operation | Previous (ms) | Current (ms) | Delta | Change % | GFLOPS |
|-----------|---------------|--------------|-------|----------|--------|
| `MatMul_128x128` | 9.85 | 7.04 | -2.81 | -28.5% | 0.60 |
| `MatMul_256x256` | 29.17 | 53.01 | +23.84 | +81.7% | 0.63 |
| `MatMul_512x512` | 119.89 | 414.73 | +294.84 | +245.9% | 0.65 |

