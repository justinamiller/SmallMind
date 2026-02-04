# Performance Profile Report

**Generated:** 2026-02-03 02:16:53
**Total Runtime:** 2438.36 ms
**Total Allocations:** 1056.73 MB
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
| 1 | `InferenceComplete` | 2407.761 | 98.75% | 3 | 802.587 | 545.368 | 1145.521 |
| 2 | `GenerateToken` | 2407.232 | 98.72% | 150 | 16.048 | 3.757 | 76.502 |
| 3 | `ForwardPass` | 2336.819 | 95.84% | 150 | 15.579 | 3.725 | 51.924 |
| 4 | `SampleToken` | 64.601 | 2.65% | 150 | 0.431 | 0.004 | 59.588 |
| 5 | `ModelCreation` | 20.360 | 0.84% | 1 | 20.360 | 20.361 | 20.361 |
| 6 | `BuilderSetup` | 18.939 | 0.78% | 1 | 18.939 | 19.108 | 19.071 |
| 7 | `Softmax` | 3.201 | 0.13% | 150 | 0.021 | 0.002 | 1.387 |
| 8 | `ApplyTemperature` | 0.508 | 0.02% | 150 | 0.003 | 0.001 | 0.011 |
| 9 | `MultinomialSample` | 0.312 | 0.01% | 150 | 0.002 | 0.000 | 0.012 |
| 10 | `PrepareInput` | 0.173 | 0.01% | 3 | 0.058 | 0.004 | 0.166 |

## 💾 Top Allocators (by Memory)

Methods allocating the most memory:

| Rank | Method | Total Alloc (MB) | % of Total | Calls | Avg Alloc (KB) |
|------|--------|------------------|-----------|-------|----------------|
| 1 | `InferenceComplete` | 1053.112 | 99.66% | 3 | 359462.271 |
| 2 | `GenerateToken` | 1053.112 | 99.66% | 150 | 7189.245 |
| 3 | `ForwardPass` | 1053.104 | 99.66% | 150 | 7189.192 |
| 4 | `ModelCreation` | 3.618 | 0.34% | 1 | 3705.133 |
| 5 | `BuilderSetup` | 3.618 | 0.34% | 1 | 3705.133 |
| 6 | `SampleToken` | 0.008 | 0.00% | 150 | 0.053 |
| 7 | `PrepareInput` | 0.000 | 0.00% | 3 | 0.000 |
| 8 | `ApplyTemperature` | 0.000 | 0.00% | 150 | 0.000 |
| 9 | `Softmax` | 0.000 | 0.00% | 150 | 0.000 |
| 10 | `MultinomialSample` | 0.000 | 0.00% | 150 | 0.000 |

## 📞 Most Called Methods

Methods called most frequently:

| Rank | Method | Calls | Total Time (ms) | Avg Time (μs) |
|------|--------|-------|----------------|---------------|
| 1 | `GenerateToken` | 150 | 2407.232 | 16048.211 |
| 2 | `ForwardPass` | 150 | 2336.819 | 15578.796 |
| 3 | `SampleToken` | 150 | 64.601 | 430.670 |
| 4 | `ApplyTemperature` | 150 | 0.508 | 3.390 |
| 5 | `Softmax` | 150 | 3.201 | 21.339 |
| 6 | `MultinomialSample` | 150 | 0.312 | 2.082 |
| 7 | `InferenceComplete` | 3 | 2407.761 | 802586.834 |
| 8 | `PrepareInput` | 3 | 0.173 | 57.791 |
| 9 | `ModelCreation` | 1 | 20.360 | 20360.499 |
| 10 | `BuilderSetup` | 1 | 18.939 | 18939.364 |

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

### `SampleToken`

**Called by:**
- `GenerateToken` (150 times)

### `ModelCreation`

*Entry point method*

## 💡 Performance Insights

- **Top 5 methods** include nested operations (some time is counted in multiple scopes)
- **5 methods** allocate more than 1 MB per call on average

