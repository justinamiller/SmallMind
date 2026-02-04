# Model Size Performance Comparison

**Generated:** 2026-02-03 02:47:31

## 📊 Model Specifications

| Model | Dimensions | Layers | Heads | Parameters | Context |
|-------|-----------|--------|-------|------------|---------|
| **Small** | 128 | 2 | 4 | 470,528 | 64 |
| **Medium** | 256 | 4 | 8 | 3,454,464 | 128 |

## ⏱️ Performance Metrics

| Metric | Small Model | Medium Model | Ratio (Med/Small) |
|--------|-------------|--------------|-------------------|
| **Total Inference Time** | 447.73 ms | 2553.97 ms | 5.70x |
| **Avg Time per Token** | 17.91 ms | 102.16 ms | 5.70x |
| **Tokens per Second** | 55.84 | 9.79 | 0.18x |
| **Memory Allocated** | 110.33 MB | 734.48 MB | 6.66x |
| **Memory per Token** | 4.41 MB | 29.38 MB | 6.66x |

## 📈 Scaling Efficiency Analysis

- **Parameter Scaling:** Medium model has **7.34x** more parameters
- **Time Scaling:** Medium model takes **5.70x** longer
- **Memory Scaling:** Medium model uses **6.66x** more memory

### Computational Efficiency: **1.29x**

✅ **Excellent** - Nearly linear scaling with parameter count

*Ideal efficiency is 1.0x, meaning time scales linearly with parameters.*

## 🔬 Forward Pass Analysis

| Model | Avg Forward Time | Calls | Total Forward Time |
|-------|------------------|-------|--------------------|
| **Small** | 17.83 ms | 25 | 445.85 ms |
| **Medium** | 102.14 ms | 25 | 2553.49 ms |

