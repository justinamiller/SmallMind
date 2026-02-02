# Performance Profile Report

**Generated:** 2026-02-02 15:25:19
**Total Runtime:** 4448.20 ms
**Total Allocations:** 1082.23 MB
**Methods Profiled:** 10

## System Information

- **OS:** Ubuntu 24.04.3 LTS
- **Architecture:** X64
- **CPU Cores:** 4
- **.NET Version:** 10.0.2
- **GC Mode:** Workstation

## 🔥 Hot Paths (by Time)

Methods consuming the most CPU time:

| Rank | Method | Total Time (ms) | % of Total | Calls | Avg Time (ms) | Min (ms) | Max (ms) |
|------|--------|----------------|-----------|-------|---------------|----------|----------|
| 1 | `InferenceComplete` | 4419.213 | 99.35% | 3 | 1473.071 | 988.966 | 2168.502 |
| 2 | `GenerateToken` | 4418.711 | 99.34% | 150 | 29.458 | 10.310 | 80.371 |
| 3 | `ForwardPass` | 4408.953 | 99.12% | 150 | 29.393 | 10.282 | 73.362 |
| 4 | `ModelCreation` | 24.507 | 0.55% | 1 | 24.507 | 24.507 | 24.507 |
| 5 | `BuilderSetup` | 22.799 | 0.51% | 1 | 22.799 | 23.143 | 23.059 |
| 6 | `SampleToken` | 9.126 | 0.21% | 150 | 0.061 | 0.007 | 7.001 |
| 7 | `Softmax` | 2.570 | 0.06% | 150 | 0.017 | 0.003 | 1.502 |
| 8 | `MultinomialSample` | 0.241 | 0.01% | 150 | 0.002 | 0.001 | 0.005 |
| 9 | `ApplyTemperature` | 0.193 | 0.00% | 150 | 0.001 | 0.001 | 0.011 |
| 10 | `PrepareInput` | 0.167 | 0.00% | 3 | 0.056 | 0.003 | 0.161 |

## 💾 Top Allocators (by Memory)

Methods allocating the most memory:

| Rank | Method | Total Alloc (MB) | % of Total | Calls | Avg Alloc (KB) |
|------|--------|------------------|-----------|-------|----------------|
| 1 | `InferenceComplete` | 1078.609 | 99.67% | 3 | 368165.263 |
| 2 | `GenerateToken` | 1078.609 | 99.67% | 150 | 7363.305 |
| 3 | `ForwardPass` | 1078.601 | 99.66% | 150 | 7363.252 |
| 4 | `ModelCreation` | 3.618 | 0.33% | 1 | 3705.008 |
| 5 | `BuilderSetup` | 3.618 | 0.33% | 1 | 3705.008 |
| 6 | `SampleToken` | 0.008 | 0.00% | 150 | 0.053 |
| 7 | `PrepareInput` | 0.000 | 0.00% | 3 | 0.000 |
| 8 | `ApplyTemperature` | 0.000 | 0.00% | 150 | 0.000 |
| 9 | `Softmax` | 0.000 | 0.00% | 150 | 0.000 |
| 10 | `MultinomialSample` | 0.000 | 0.00% | 150 | 0.000 |

## 📞 Most Called Methods

Methods called most frequently:

| Rank | Method | Calls | Total Time (ms) | Avg Time (μs) |
|------|--------|-------|----------------|---------------|
| 1 | `GenerateToken` | 150 | 4418.711 | 29458.073 |
| 2 | `ForwardPass` | 150 | 4408.953 | 29393.021 |
| 3 | `SampleToken` | 150 | 9.126 | 60.841 |
| 4 | `ApplyTemperature` | 150 | 0.193 | 1.285 |
| 5 | `Softmax` | 150 | 2.570 | 17.133 |
| 6 | `MultinomialSample` | 150 | 0.241 | 1.609 |
| 7 | `InferenceComplete` | 3 | 4419.213 | 1473071.138 |
| 8 | `PrepareInput` | 3 | 0.167 | 55.814 |
| 9 | `ModelCreation` | 1 | 24.507 | 24506.790 |
| 10 | `BuilderSetup` | 1 | 22.799 | 22798.934 |

## 🌲 Call Hierarchy

Parent-child relationships for hot paths:

### `InferenceComplete`

*Entry point method*

### `GenerateToken`

**Called by:**
- `InferenceComplete` (150 times)

### `ForwardPass`

**Called by:**
- `GenerateToken` (150 times)

### `ModelCreation`

*Entry point method*

### `BuilderSetup`

**Called by:**
- `ModelCreation` (1 times)

## 💡 Performance Insights

- **Top 5 hot paths** account for **298.9%** of total runtime
- **5 methods** allocate more than 1 MB per call on average

