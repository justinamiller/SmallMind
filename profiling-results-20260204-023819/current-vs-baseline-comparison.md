# Profile Comparison Report

**Previous Run:** 2026-02-03 02:36:36
**Current Run:** 2026-02-04 02:36:44

## 📊 Overall Performance Summary

| Metric | Previous | Current | Delta | Change % |
|--------|----------|---------|-------|----------|
| **Total Runtime** | 5927.60 ms | 9277.78 ms | +3350.18 ms | +56.5% |
| **Total Allocations** | 2550.03 MB | 338.71 MB | -2211.32 MB | -86.7% |
| **Methods Profiled** | 29 | 29 | +0 | - |

### 🎯 Performance Verdict

⚠️ **REGRESSED**: Overall performance degraded by 56.5%

## 🚀 Top 10 Improvements

| Method | Previous (ms) | Current (ms) | Delta (ms) | Change % |
|--------|---------------|--------------|------------|----------|
| `Model_Small_GenerateToken` | 531.59 | 427.63 | -103.96 | -19.6% |
| `Model_Small_Inference` | 531.64 | 427.71 | -103.93 | -19.5% |
| `Model_Small_Forward` | 529.00 | 425.27 | -103.73 | -19.6% |
| `Model_Small_Creation` | 34.51 | 24.60 | -9.91 | -28.7% |
| `TensorAdd_10000` | 10.84 | 1.82 | -9.02 | -83.2% |
| `TensorAdd_Iteration` | 10.83 | 1.81 | -9.02 | -83.3% |
| `Softmax_Iteration` | 6.36 | 0.34 | -6.02 | -94.7% |
| `Softmax_2048` | 6.22 | 0.23 | -5.99 | -96.3% |
| `Softmax_256` | 7.21 | 1.35 | -5.86 | -81.3% |
| `BroadcastAdd_Iteration` | 6.91 | 2.16 | -4.75 | -68.7% |

## ⚠️ Top 10 Regressions

| Method | Previous (ms) | Current (ms) | Delta (ms) | Change % |
|--------|---------------|--------------|------------|----------|
| `Model_Medium_Forward` | 1200.76 | 2186.21 | +985.45 | +82.1% |
| `Model_Medium_GenerateToken` | 1201.18 | 2186.56 | +985.38 | +82.0% |
| `Model_Medium_Inference` | 1201.28 | 2186.63 | +985.35 | +82.0% |
| `MatMul_512x512` | 172.11 | 449.13 | +277.02 | +161.0% |
| `MatMul_Iteration` | 148.10 | 390.54 | +242.44 | +163.7% |
| `GELU_Iteration` | 90.44 | 153.75 | +63.31 | +70.0% |
| `GELU_1000000` | 100.60 | 163.03 | +62.43 | +62.1% |
| `Model_Medium_Creation` | 84.98 | 139.84 | +54.86 | +64.6% |
| `MatMul_256x256` | 19.59 | 56.52 | +36.93 | +188.5% |
| `MatMul_128x128` | 3.54 | 20.00 | +16.46 | +465.0% |

## 📋 Detailed Method Comparison

| Method | Prev Time (ms) | Curr Time (ms) | Time Δ | Time Δ% | Prev Alloc (MB) | Curr Alloc (MB) | Status |
|--------|----------------|----------------|--------|---------|-----------------|-----------------|--------|
| `Model_Medium_Forward` | 1200.76 | 2186.21 | +985.45 | +82.1% | 729.96 | 83.11 | ⚠️ Regressed |
| `Model_Medium_GenerateToken` | 1201.18 | 2186.56 | +985.38 | +82.0% | 729.96 | 83.12 | ⚠️ Regressed |
| `Model_Medium_Inference` | 1201.28 | 2186.63 | +985.35 | +82.0% | 729.97 | 83.12 | ⚠️ Regressed |
| `MatMul_512x512` | 172.11 | 449.13 | +277.02 | +161.0% | 0.00 | 0.02 | ⚠️ Regressed |
| `MatMul_Iteration` | 148.10 | 390.54 | +242.44 | +163.7% | 0.00 | 0.02 | ⚠️ Regressed |
| `Model_Small_GenerateToken` | 531.59 | 427.63 | -103.96 | -19.6% | 109.26 | 18.97 | ✅ Improved |
| `Model_Small_Inference` | 531.64 | 427.71 | -103.93 | -19.5% | 109.26 | 18.97 | ✅ Improved |
| `Model_Small_Forward` | 529.00 | 425.27 | -103.73 | -19.6% | 109.26 | 18.96 | ✅ Improved |
| `GELU_Iteration` | 90.44 | 153.75 | +63.31 | +70.0% | 0.00 | 0.00 | ⚠️ Regressed |
| `GELU_1000000` | 100.60 | 163.03 | +62.43 | +62.1% | 0.01 | 0.01 | ⚠️ Regressed |
| `Model_Medium_Creation` | 84.98 | 139.84 | +54.86 | +64.6% | 26.45 | 26.42 | ⚠️ Regressed |
| `MatMul_256x256` | 19.59 | 56.52 | +36.93 | +188.5% | 0.00 | 0.01 | ⚠️ Regressed |
| `MatMul_128x128` | 3.54 | 20.00 | +16.46 | +465.0% | 0.00 | 0.07 | ⚠️ Regressed |
| `Model_Small_Creation` | 34.51 | 24.60 | -9.91 | -28.7% | 3.61 | 3.61 | ✅ Improved |
| `TensorAdd_10000` | 10.84 | 1.82 | -9.02 | -83.2% | 0.38 | 0.38 | ✅ Improved |
| `TensorAdd_Iteration` | 10.83 | 1.81 | -9.02 | -83.3% | 0.38 | 0.38 | ✅ Improved |
| `GELU_100000` | 11.06 | 17.18 | +6.12 | +55.3% | 0.01 | 0.00 | ⚠️ Regressed |
| `Softmax_Iteration` | 6.36 | 0.34 | -6.02 | -94.7% | 0.00 | 0.00 | ✅ Improved |
| `Softmax_2048` | 6.22 | 0.23 | -5.99 | -96.3% | 0.00 | 0.00 | ✅ Improved |
| `Softmax_256` | 7.21 | 1.35 | -5.86 | -81.3% | 0.00 | 0.00 | ✅ Improved |
| `BroadcastAdd_Iteration` | 6.91 | 2.16 | -4.75 | -68.7% | 0.38 | 0.39 | ✅ Improved |
| `BroadcastAdd_100x100` | 6.93 | 2.19 | -4.74 | -68.4% | 0.38 | 0.39 | ✅ Improved |
| `MatMul_64x64` | 7.07 | 4.97 | -2.10 | -29.7% | 0.00 | 0.00 | ✅ Improved |
| `TensorMul_10000` | 0.60 | 1.69 | +1.09 | +181.7% | 0.38 | 0.38 | ⚠️ Regressed |
| `TensorMul_Iteration` | 0.59 | 1.68 | +1.09 | +184.7% | 0.38 | 0.38 | ⚠️ Regressed |
| `GELU_10000` | 1.17 | 2.06 | +0.89 | +76.1% | 0.00 | 0.00 | ⚠️ Regressed |
| `GELU_1000` | 2.28 | 2.72 | +0.44 | +19.3% | 0.00 | 0.00 | ⚠️ Regressed |
| `Softmax_1024` | 0.15 | 0.10 | -0.05 | -33.3% | 0.00 | 0.00 | ✅ Improved |
| `Softmax_512` | 0.06 | 0.06 | +0.00 | +0.0% | 0.00 | 0.00 | ➡️ Unchanged |

## 🔬 Model Size Comparison

### Small vs Medium Model Performance

| Model | Previous (ms) | Current (ms) | Delta | Change % | Alloc (MB) |
|-------|---------------|--------------|-------|----------|------------|
| **Small** (128 dim, 2 layers) | 531.64 | 427.71 | -103.93 | -19.5% | 18.97 |
| **Medium** (256 dim, 4 layers) | 1201.28 | 2186.63 | +985.35 | +82.0% | 83.12 |

### Scaling Analysis

- **Medium/Small Time Ratio:** 5.11x
- **Medium Model Parameters:** ~7.3x more parameters than Small
- **Computational Efficiency:** 1.43x (ideal: 1.0x, higher is better)

### Inference Throughput (25 tokens)

| Model | Tokens/Second | Latency/Token (ms) | Memory/Token (MB) |
|-------|---------------|-------------------|-------------------|
| **Small** | 58.45 | 17.11 | 0.76 |
| **Medium** | 11.43 | 87.47 | 3.32 |

### SIMD Operation Performance

| Operation | Previous (ms) | Current (ms) | Delta | Change % | GFLOPS |
|-----------|---------------|--------------|-------|----------|--------|
| `MatMul_128x128` | 3.54 | 20.00 | +16.46 | +465.0% | 0.21 |
| `MatMul_256x256` | 19.59 | 56.52 | +36.93 | +188.5% | 0.59 |
| `MatMul_512x512` | 172.11 | 449.13 | +277.02 | +161.0% | 0.60 |

