# Performance Profile Report

**Generated:** 2026-02-02 15:33:08
**Total Runtime:** 3951.04 ms
**Total Allocations:** 1082.26 MB
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
| 1 | `InferenceComplete` | 3922.678 | 99.28% | 3 | 1307.559 | 907.363 | 1810.236 |
| 2 | `GenerateToken` | 3922.226 | 99.27% | 150 | 26.148 | 7.591 | 84.086 |
| 3 | `ForwardPass` | 3917.261 | 99.15% | 150 | 26.115 | 7.583 | 84.029 |
| 4 | `ModelCreation` | 23.872 | 0.60% | 1 | 23.872 | 23.872 | 23.872 |
| 5 | `BuilderSetup` | 22.215 | 0.56% | 1 | 22.215 | 22.558 | 22.490 |
| 6 | `SampleToken` | 3.607 | 0.09% | 150 | 0.024 | 0.007 | 1.264 |
| 7 | `Softmax` | 2.222 | 0.06% | 150 | 0.015 | 0.003 | 0.940 |
| 8 | `MultinomialSample` | 0.229 | 0.01% | 150 | 0.002 | 0.001 | 0.010 |
| 9 | `ApplyTemperature` | 0.177 | 0.00% | 150 | 0.001 | 0.001 | 0.004 |
| 10 | `PrepareInput` | 0.162 | 0.00% | 3 | 0.054 | 0.002 | 0.157 |

## 💾 Top Allocators (by Memory)

Methods allocating the most memory:

| Rank | Method | Total Alloc (MB) | % of Total | Calls | Avg Alloc (KB) |
|------|--------|------------------|-----------|-------|----------------|
| 1 | `InferenceComplete` | 1078.645 | 99.67% | 3 | 368177.372 |
| 2 | `GenerateToken` | 1078.645 | 99.67% | 150 | 7363.547 |
| 3 | `ForwardPass` | 1078.637 | 99.66% | 150 | 7363.494 |
| 4 | `ModelCreation` | 3.618 | 0.33% | 1 | 3705.094 |
| 5 | `BuilderSetup` | 3.618 | 0.33% | 1 | 3705.094 |
| 6 | `SampleToken` | 0.008 | 0.00% | 150 | 0.053 |
| 7 | `PrepareInput` | 0.000 | 0.00% | 3 | 0.000 |
| 8 | `ApplyTemperature` | 0.000 | 0.00% | 150 | 0.000 |
| 9 | `Softmax` | 0.000 | 0.00% | 150 | 0.000 |
| 10 | `MultinomialSample` | 0.000 | 0.00% | 150 | 0.000 |

## 📞 Most Called Methods

Methods called most frequently:

| Rank | Method | Calls | Total Time (ms) | Avg Time (μs) |
|------|--------|-------|----------------|---------------|
| 1 | `GenerateToken` | 150 | 3922.226 | 26148.172 |
| 2 | `ForwardPass` | 150 | 3917.261 | 26115.075 |
| 3 | `SampleToken` | 150 | 3.607 | 24.044 |
| 4 | `ApplyTemperature` | 150 | 0.177 | 1.179 |
| 5 | `Softmax` | 150 | 2.222 | 14.815 |
| 6 | `MultinomialSample` | 150 | 0.229 | 1.525 |
| 7 | `InferenceComplete` | 3 | 3922.678 | 1307559.496 |
| 8 | `PrepareInput` | 3 | 0.162 | 53.967 |
| 9 | `ModelCreation` | 1 | 23.872 | 23871.607 |
| 10 | `BuilderSetup` | 1 | 22.215 | 22214.888 |

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

- **Top 5 methods** include nested operations (some time is counted in multiple scopes)
- **5 methods** allocate more than 1 MB per call on average

