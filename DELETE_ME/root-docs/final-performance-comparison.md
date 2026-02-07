# Profile Comparison Report

**Previous Run:** 2026-02-03 02:36:36
**Current Run:** 2026-02-04 02:29:50

## 📊 Overall Performance Summary

| Metric | Previous | Current | Delta | Change % |
|--------|----------|---------|-------|----------|
| **Total Runtime** | 5927.60 ms | 2931.41 ms | -2996.19 ms | -50.5% |
| **Total Allocations** | 2550.03 MB | 338.49 MB | -2211.54 MB | -86.7% |
| **Methods Profiled** | 29 | 29 | +0 | - |

### 🎯 Performance Verdict

✅ **IMPROVED**: Overall performance improved by 50.5%

## 🚀 Top 10 Improvements

| Method | Previous (ms) | Current (ms) | Delta (ms) | Change % |
|--------|---------------|--------------|------------|----------|
| `Model_Medium_Inference` | 1201.28 | 557.34 | -643.94 | -53.6% |
| `Model_Medium_GenerateToken` | 1201.18 | 557.25 | -643.93 | -53.6% |
| `Model_Medium_Forward` | 1200.76 | 556.86 | -643.90 | -53.6% |
| `Model_Small_Inference` | 531.64 | 256.39 | -275.25 | -51.8% |
| `Model_Small_GenerateToken` | 531.59 | 256.36 | -275.23 | -51.8% |
| `Model_Small_Forward` | 529.00 | 254.82 | -274.18 | -51.8% |
| `MatMul_512x512` | 172.11 | 109.98 | -62.13 | -36.1% |
| `MatMul_Iteration` | 148.10 | 103.94 | -44.16 | -29.8% |
| `GELU_Iteration` | 90.44 | 58.56 | -31.88 | -35.2% |
| `Model_Medium_Creation` | 84.98 | 58.47 | -26.51 | -31.2% |

## ⚠️ Top 10 Regressions

| Method | Previous (ms) | Current (ms) | Delta (ms) | Change % |
|--------|---------------|--------------|------------|----------|
| `MatMul_128x128` | 3.54 | 21.32 | +17.78 | +502.3% |
| `MatMul_256x256` | 19.59 | 20.77 | +1.18 | +6.0% |
| `MatMul_64x64` | 7.07 | 8.04 | +0.97 | +13.7% |
| `GELU_10000` | 1.17 | 2.00 | +0.83 | +70.9% |
| `Softmax_1024` | 0.15 | 0.16 | +0.01 | +6.7% |

## 📋 Detailed Method Comparison

| Method | Prev Time (ms) | Curr Time (ms) | Time Δ | Time Δ% | Prev Alloc (MB) | Curr Alloc (MB) | Status |
|--------|----------------|----------------|--------|---------|-----------------|-----------------|--------|
| `Model_Medium_Inference` | 1201.28 | 557.34 | -643.94 | -53.6% | 729.97 | 83.07 | ✅ Improved |
| `Model_Medium_GenerateToken` | 1201.18 | 557.25 | -643.93 | -53.6% | 729.96 | 83.07 | ✅ Improved |
| `Model_Medium_Forward` | 1200.76 | 556.86 | -643.90 | -53.6% | 729.96 | 83.07 | ✅ Improved |
| `Model_Small_Inference` | 531.64 | 256.39 | -275.25 | -51.8% | 109.26 | 18.94 | ✅ Improved |
| `Model_Small_GenerateToken` | 531.59 | 256.36 | -275.23 | -51.8% | 109.26 | 18.94 | ✅ Improved |
| `Model_Small_Forward` | 529.00 | 254.82 | -274.18 | -51.8% | 109.26 | 18.93 | ✅ Improved |
| `MatMul_512x512` | 172.11 | 109.98 | -62.13 | -36.1% | 0.00 | 0.02 | ✅ Improved |
| `MatMul_Iteration` | 148.10 | 103.94 | -44.16 | -29.8% | 0.00 | 0.03 | ✅ Improved |
| `GELU_Iteration` | 90.44 | 58.56 | -31.88 | -35.2% | 0.00 | 0.00 | ✅ Improved |
| `Model_Medium_Creation` | 84.98 | 58.47 | -26.51 | -31.2% | 26.45 | 26.44 | ✅ Improved |
| `GELU_1000000` | 100.60 | 74.27 | -26.33 | -26.2% | 0.01 | 0.01 | ✅ Improved |
| `Model_Small_Creation` | 34.51 | 10.49 | -24.02 | -69.6% | 3.61 | 3.61 | ✅ Improved |
| `MatMul_128x128` | 3.54 | 21.32 | +17.78 | +502.3% | 0.00 | 0.07 | ⚠️ Regressed |
| `TensorAdd_Iteration` | 10.83 | 3.95 | -6.88 | -63.5% | 0.38 | 0.38 | ✅ Improved |
| `TensorAdd_10000` | 10.84 | 3.97 | -6.87 | -63.4% | 0.38 | 0.38 | ✅ Improved |
| `Softmax_256` | 7.21 | 1.02 | -6.19 | -85.9% | 0.00 | 0.00 | ✅ Improved |
| `BroadcastAdd_100x100` | 6.93 | 1.42 | -5.51 | -79.5% | 0.38 | 0.38 | ✅ Improved |
| `BroadcastAdd_Iteration` | 6.91 | 1.41 | -5.50 | -79.6% | 0.38 | 0.38 | ✅ Improved |
| `GELU_100000` | 11.06 | 5.72 | -5.34 | -48.3% | 0.01 | 0.00 | ✅ Improved |
| `Softmax_2048` | 6.22 | 1.58 | -4.64 | -74.6% | 0.00 | 0.00 | ✅ Improved |
| `Softmax_Iteration` | 6.36 | 1.73 | -4.63 | -72.8% | 0.00 | 0.00 | ✅ Improved |
| `MatMul_256x256` | 19.59 | 20.77 | +1.18 | +6.0% | 0.00 | 0.01 | ⚠️ Regressed |
| `MatMul_64x64` | 7.07 | 8.04 | +0.97 | +13.7% | 0.00 | 0.00 | ⚠️ Regressed |
| `GELU_10000` | 1.17 | 2.00 | +0.83 | +70.9% | 0.00 | 0.00 | ⚠️ Regressed |
| `GELU_1000` | 2.28 | 2.32 | +0.04 | +1.8% | 0.00 | 0.00 | ➡️ Unchanged |
| `TensorMul_10000` | 0.60 | 0.61 | +0.01 | +1.7% | 0.38 | 0.38 | ➡️ Unchanged |
| `TensorMul_Iteration` | 0.59 | 0.60 | +0.01 | +1.7% | 0.38 | 0.38 | ➡️ Unchanged |
| `Softmax_1024` | 0.15 | 0.16 | +0.01 | +6.7% | 0.00 | 0.00 | ⚠️ Regressed |
| `Softmax_512` | 0.06 | 0.06 | +0.00 | +0.0% | 0.00 | 0.00 | ➡️ Unchanged |

## 🔬 Model Size Comparison

### Small vs Medium Model Performance

| Model | Previous (ms) | Current (ms) | Delta | Change % | Alloc (MB) |
|-------|---------------|--------------|-------|----------|------------|
| **Small** (128 dim, 2 layers) | 531.64 | 256.39 | -275.25 | -51.8% | 18.94 |
| **Medium** (256 dim, 4 layers) | 1201.28 | 557.34 | -643.94 | -53.6% | 83.07 |

### Scaling Analysis

- **Medium/Small Time Ratio:** 2.17x
- **Medium Model Parameters:** ~7.3x more parameters than Small
- **Computational Efficiency:** 3.36x (ideal: 1.0x, higher is better)

### Inference Throughput (25 tokens)

| Model | Tokens/Second | Latency/Token (ms) | Memory/Token (MB) |
|-------|---------------|-------------------|-------------------|
| **Small** | 97.51 | 10.26 | 0.76 |
| **Medium** | 44.86 | 22.29 | 3.32 |

### SIMD Operation Performance

| Operation | Previous (ms) | Current (ms) | Delta | Change % | GFLOPS |
|-----------|---------------|--------------|-------|----------|--------|
| `MatMul_128x128` | 3.54 | 21.32 | +17.78 | +502.3% | 0.20 |
| `MatMul_256x256` | 19.59 | 20.77 | +1.18 | +6.0% | 1.62 |
| `MatMul_512x512` | 172.11 | 109.98 | -62.13 | -36.1% | 2.44 |

