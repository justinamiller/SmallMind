# SmallMind Benchmark Results

**Generated:** 2026-02-04 00:13:09
**Generated (UTC):** 2026-02-04 00:13:09 UTC

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
| Repo Commit | b1533bd1e9fa43aeefa7e830a643e50b647d84f0 |

## Run Configuration

| Parameter | Value |
|-----------|-------|
| Model Path | `/home/runner/work/SmallMind/SmallMind/benchmark-model.smq` |
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
| ttft_ms | 1.57 | 1.71 | 1.92 | 2.11 | 2.27 | 2.28 | 1.77 | 0.17 |
| latency_ms | 325.68 | 336.32 | 355.93 | 387.80 | 395.67 | 398.71 | 341.13 | 17.88 |

#### Runtime Counters
| Counter | Avg | Peak |
|---------|-----|------|
| CPU Usage (%) | 49.77 | 51.81 |
| Working Set (MB) | 0.00 | 0.00 |
| GC Heap Size (MB) | 0.00 | 0.00 |
| Alloc Rate (MB/s) | 970.35 | 1013.71 |
| Time in GC (%) | 1.10 | - |
| ThreadPool Threads | 7 | - |

### STEADY_TOKENS_PER_SEC

#### Metrics
| Metric | Min | P50 | P90 | P95 | P99 | Max | Mean | StdDev |
|--------|-----|-----|-----|-----|-----|-----|------|--------|
| steady_tokens_per_sec | 739.43 | 764.11 | 774.95 | 781.37 | 785.28 | 786.46 | 764.98 | 9.38 |
| overall_tokens_per_sec | 738.68 | 763.16 | 773.97 | 780.28 | 784.43 | 785.78 | 764.10 | 9.34 |

#### Runtime Counters
| Counter | Avg | Peak |
|---------|-----|------|
| CPU Usage (%) | 50.83 | 51.58 |
| Working Set (MB) | 0.00 | 0.00 |
| GC Heap Size (MB) | 0.00 | 0.00 |
| Alloc Rate (MB/s) | 1004.26 | 1014.10 |
| Time in GC (%) | 0.70 | - |
| ThreadPool Threads | 7 | - |

### END_TO_END_LATENCY

#### Metrics
| Metric | Min | P50 | P90 | P95 | P99 | Max | Mean | StdDev |
|--------|-----|-----|-----|-----|-----|-----|------|--------|
| latency_ms | 327.79 | 337.58 | 344.88 | 346.67 | 349.77 | 350.64 | 338.86 | 4.90 |

#### Runtime Counters
| Counter | Avg | Peak |
|---------|-----|------|
| CPU Usage (%) | 51.07 | 51.59 |
| Working Set (MB) | 0.00 | 0.00 |
| GC Heap Size (MB) | 0.00 | 0.00 |
| Alloc Rate (MB/s) | 994.19 | 1007.61 |
| Time in GC (%) | 0.70 | - |
| ThreadPool Threads | 7 | - |

### MEMORY_FOOTPRINT

#### Memory Footprint
| Metric | Min (MB) | Max (MB) | Avg (MB) |
|--------|----------|----------|----------|
| Working Set | 73.24 | 77.24 | 76.56 |
| Private Memory | 277.39 | 281.02 | 280.41 |
| Managed Heap | 3.12 | 18.22 | 10.91 |

#### Runtime Counters
| Counter | Avg | Peak |
|---------|-----|------|
| CPU Usage (%) | 51.21 | 52.41 |
| Working Set (MB) | 0.00 | 0.00 |
| GC Heap Size (MB) | 0.00 | 0.00 |
| Alloc Rate (MB/s) | 996.42 | 1005.44 |
| Time in GC (%) | 0.60 | - |
| ThreadPool Threads | 6 | - |

### GC_AND_ALLOCATIONS

#### GC & Allocations
| Metric | Value |
|--------|-------|
| Gen0 Collections | 633 |
| Gen1 Collections | 142 |
| Gen2 Collections | 0 |
| Total Allocated (MB) | 10099.39 |
| Allocations/Op (MB) | 336.65 |

#### Runtime Counters
| Counter | Avg | Peak |
|---------|-----|------|
| CPU Usage (%) | 51.13 | 51.55 |
| Working Set (MB) | 0.00 | 0.00 |
| GC Heap Size (MB) | 0.00 | 0.00 |
| Alloc Rate (MB/s) | 1003.72 | 1012.37 |
| Time in GC (%) | 5.73 | - |
| ThreadPool Threads | 6 | - |

### CONCURRENCY_THROUGHPUT_1

#### Metrics
| Metric | Min | P50 | P90 | P95 | P99 | Max | Mean | StdDev |
|--------|-----|-----|-----|-----|-----|-----|------|--------|
| latency_ms | 330.15 | 334.98 | 338.56 | 340.88 | 343.57 | 344.64 | 335.42 | 3.18 |
| requests_per_sec | 0.00 | 0.00 | 0.00 | 0.00 | 0.00 | 0.00 | 2.98 | 0.00 |
| tokens_per_sec | 0.00 | 0.00 | 0.00 | 0.00 | 0.00 | 0.00 | 762.95 | 0.00 |

#### Runtime Counters
| Counter | Avg | Peak |
|---------|-----|------|
| CPU Usage (%) | 51.11 | 51.72 |
| Working Set (MB) | 0.00 | 0.00 |
| GC Heap Size (MB) | 0.00 | 0.00 |
| Alloc Rate (MB/s) | 1003.49 | 1013.41 |
| Time in GC (%) | 0.30 | - |
| ThreadPool Threads | 6 | - |

