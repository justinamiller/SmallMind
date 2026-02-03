# SmallMind Benchmark Results

**Generated:** 2026-02-03 19:05:28
**Generated (UTC):** 2026-02-03 19:05:28 UTC

## Environment

### Hardware & OS
| Property | Value |
|----------|-------|
| OS | Ubuntu 24.04.3 LTS |
| OS Version | Unix 6.11.0.1018 |
| Architecture | X64 |
| CPU Cores | 4 |

### .NET Runtime
| Property | Value |
|----------|-------|
| .NET Version | 10.0.2 |
| Runtime | .NET 10.0.2 |
| Build Config | Release |

### Engine Configuration
| Property | Value |
|----------|-------|
| Threads | 4 |
| Repo Commit | 48cfd767492131b95ea36922602e032141106f39 |

## Run Configuration

| Parameter | Value |
|-----------|-------|
| Model Path | `benchmark-model.smq` |
| Scenario | all |
| Iterations | 30 |
| Warmup | 5 |
| Max New Tokens | 256 |
| Prompt Profile | short |
| Temperature | 1 |
| Top-K | 1 |
| Top-P | 1 |
| Seed | 42 |
| Cold Start | False |

## Benchmark Results

### TTFT

#### Metrics
| Metric | Min | P50 | P90 | P95 | P99 | Max | Mean | StdDev |
|--------|-----|-----|-----|-----|-----|-----|------|--------|
| ttft_ms | 1.31 | 1.52 | 1.72 | 1.82 | 1.92 | 1.95 | 1.55 | 0.15 |
| latency_ms | 322.39 | 331.44 | 345.36 | 347.10 | 348.26 | 348.26 | 333.96 | 7.72 |

#### Runtime Counters
| Counter | Avg | Peak |
|---------|-----|------|
| CPU Usage (%) | 73.53 | 74.37 |
| Working Set (MB) | 0.00 | 0.00 |
| GC Heap Size (MB) | 0.00 | 0.00 |
| Alloc Rate (MB/s) | 1052.73 | 1074.18 |
| Time in GC (%) | 0.80 | - |
| ThreadPool Threads | 7 | - |

### STEADY_TOKENS_PER_SEC

#### Metrics
| Metric | Min | P50 | P90 | P95 | P99 | Max | Mean | StdDev |
|--------|-----|-----|-----|-----|-----|-----|------|--------|
| steady_tokens_per_sec | 745.66 | 783.41 | 792.08 | 793.29 | 796.10 | 796.99 | 777.73 | 15.32 |
| overall_tokens_per_sec | 744.49 | 783.11 | 791.79 | 792.96 | 795.34 | 796.09 | 777.17 | 15.48 |

#### Runtime Counters
| Counter | Avg | Peak |
|---------|-----|------|
| CPU Usage (%) | 74.17 | 75.00 |
| Working Set (MB) | 0.00 | 0.00 |
| GC Heap Size (MB) | 0.00 | 0.00 |
| Alloc Rate (MB/s) | 1072.73 | 1092.91 |
| Time in GC (%) | 0.30 | - |
| ThreadPool Threads | 7 | - |

### END_TO_END_LATENCY

#### Metrics
| Metric | Min | P50 | P90 | P95 | P99 | Max | Mean | StdDev |
|--------|-----|-----|-----|-----|-----|-----|------|--------|
| latency_ms | 320.79 | 325.14 | 331.17 | 336.56 | 343.69 | 345.30 | 326.63 | 5.24 |

#### Runtime Counters
| Counter | Avg | Peak |
|---------|-----|------|
| CPU Usage (%) | 74.19 | 75.09 |
| Working Set (MB) | 0.00 | 0.00 |
| GC Heap Size (MB) | 0.00 | 0.00 |
| Alloc Rate (MB/s) | 1080.68 | 1091.03 |
| Time in GC (%) | 0.50 | - |
| ThreadPool Threads | 7 | - |

### MEMORY_FOOTPRINT

#### Memory Footprint
| Metric | Min (MB) | Max (MB) | Avg (MB) |
|--------|----------|----------|----------|
| Working Set | 78.62 | 82.65 | 81.91 |
| Private Memory | 282.01 | 285.82 | 285.14 |
| Managed Heap | 2.90 | 25.13 | 13.95 |

#### Runtime Counters
| Counter | Avg | Peak |
|---------|-----|------|
| CPU Usage (%) | 73.79 | 74.34 |
| Working Set (MB) | 0.00 | 0.00 |
| GC Heap Size (MB) | 0.00 | 0.00 |
| Alloc Rate (MB/s) | 1081.55 | 1091.42 |
| Time in GC (%) | 0.20 | - |
| ThreadPool Threads | 7 | - |

### GC_AND_ALLOCATIONS

#### GC & Allocations
| Metric | Value |
|--------|-------|
| Gen0 Collections | 446 |
| Gen1 Collections | 103 |
| Gen2 Collections | 0 |
| Total Allocated (MB) | 10605.71 |
| Allocations/Op (MB) | 353.52 |

#### Runtime Counters
| Counter | Avg | Peak |
|---------|-----|------|
| CPU Usage (%) | 75.12 | 75.97 |
| Working Set (MB) | 0.00 | 0.00 |
| GC Heap Size (MB) | 0.00 | 0.00 |
| Alloc Rate (MB/s) | 1087.36 | 1091.62 |
| Time in GC (%) | 0.40 | - |
| ThreadPool Threads | 7 | - |

### CONCURRENCY_THROUGHPUT_1

#### Metrics
| Metric | Min | P50 | P90 | P95 | P99 | Max | Mean | StdDev |
|--------|-----|-----|-----|-----|-----|-----|------|--------|
| latency_ms | 322.33 | 325.97 | 332.67 | 336.98 | 339.77 | 340.60 | 327.66 | 4.52 |
| requests_per_sec | 0.00 | 0.00 | 0.00 | 0.00 | 0.00 | 0.00 | 3.05 | 0.00 |
| tokens_per_sec | 0.00 | 0.00 | 0.00 | 0.00 | 0.00 | 0.00 | 781.02 | 0.00 |

#### Runtime Counters
| Counter | Avg | Peak |
|---------|-----|------|
| CPU Usage (%) | 75.61 | 76.12 |
| Working Set (MB) | 0.00 | 0.00 |
| GC Heap Size (MB) | 0.00 | 0.00 |
| Alloc Rate (MB/s) | 1079.25 | 1089.62 |
| Time in GC (%) | 0.56 | - |
| ThreadPool Threads | 7 | - |

