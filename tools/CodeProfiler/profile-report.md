# Performance Profile Report

**Generated:** 2026-02-02 18:01:33
**Total Runtime:** 2050.20 ms
**Total Allocations:** 1081.96 MB
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
| 1 | `InferenceComplete` | 2024.904 | 98.77% | 3 | 674.968 | 424.083 | 1051.621 |
| 2 | `GenerateToken` | 2024.093 | 98.73% | 150 | 13.494 | 3.470 | 81.093 |
| 3 | `ForwardPass` | 1953.875 | 95.30% | 150 | 13.026 | 3.434 | 49.923 |
| 4 | `SampleToken` | 69.269 | 3.38% | 150 | 0.462 | 0.005 | 63.422 |
| 5 | `ModelCreation` | 20.637 | 1.01% | 1 | 20.637 | 20.638 | 20.638 |
| 6 | `BuilderSetup` | 19.004 | 0.93% | 1 | 19.004 | 19.228 | 19.178 |
| 7 | `Softmax` | 3.780 | 0.18% | 150 | 0.025 | 0.002 | 1.537 |
| 8 | `ApplyTemperature` | 0.600 | 0.03% | 150 | 0.004 | 0.001 | 0.026 |
| 9 | `MultinomialSample` | 0.403 | 0.02% | 150 | 0.003 | 0.000 | 0.006 |
| 10 | `PrepareInput` | 0.175 | 0.01% | 3 | 0.058 | 0.005 | 0.165 |

## 💾 Top Allocators (by Memory)

Methods allocating the most memory:

| Rank | Method | Total Alloc (MB) | % of Total | Calls | Avg Alloc (KB) |
|------|--------|------------------|-----------|-------|----------------|
| 1 | `InferenceComplete` | 1078.346 | 99.67% | 3 | 368075.417 |
| 2 | `GenerateToken` | 1078.346 | 99.67% | 150 | 7361.508 |
| 3 | `ForwardPass` | 1078.315 | 99.66% | 150 | 7361.295 |
| 4 | `ModelCreation` | 3.618 | 0.33% | 1 | 3704.320 |
| 5 | `BuilderSetup` | 3.618 | 0.33% | 1 | 3704.320 |
| 6 | `SampleToken` | 0.031 | 0.00% | 150 | 0.214 |
| 7 | `PrepareInput` | 0.000 | 0.00% | 3 | 0.000 |
| 8 | `ApplyTemperature` | 0.000 | 0.00% | 150 | 0.000 |
| 9 | `Softmax` | 0.000 | 0.00% | 150 | 0.000 |
| 10 | `MultinomialSample` | 0.000 | 0.00% | 150 | 0.000 |

## 📞 Most Called Methods

Methods called most frequently:

| Rank | Method | Calls | Total Time (ms) | Avg Time (μs) |
|------|--------|-------|----------------|---------------|
| 1 | `GenerateToken` | 150 | 2024.093 | 13493.956 |
| 2 | `ForwardPass` | 150 | 1953.875 | 13025.835 |
| 3 | `SampleToken` | 150 | 69.269 | 461.792 |
| 4 | `ApplyTemperature` | 150 | 0.600 | 3.997 |
| 5 | `Softmax` | 150 | 3.780 | 25.197 |
| 6 | `MultinomialSample` | 150 | 0.403 | 2.687 |
| 7 | `InferenceComplete` | 3 | 2024.904 | 674967.962 |
| 8 | `PrepareInput` | 3 | 0.175 | 58.288 |
| 9 | `ModelCreation` | 1 | 20.637 | 20637.176 |
| 10 | `BuilderSetup` | 1 | 19.004 | 19004.284 |

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

