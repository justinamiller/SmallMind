# SmallMind Benchmark Results

**Generated:** 2026-02-02 05:31:14
**Generated (UTC):** 2026-02-02 05:31:14 UTC

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
| Repo Commit | 7e075c26b295f9336fe7d54f4b35423190d0ae0f |

## Run Configuration

| Parameter | Value |
|-----------|-------|
| Model Path | `benchmark-model.smq` |
| Scenario | all |
| Iterations | 10 |
| Warmup | 2 |
| Max New Tokens | 50 |
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
| ttft_ms | 2.23 | 2.79 | 3.98 | 4.24 | 4.44 | 4.50 | 3.03 | 0.78 |
| latency_ms | 110.29 | 131.07 | 207.33 | 210.83 | 213.63 | 214.33 | 151.97 | 43.30 |

#### Runtime Counters
| Counter | Avg | Peak |
|---------|-----|------|
| CPU Usage (%) | 24.57 | 49.11 |
| Working Set (MB) | 0.00 | 0.00 |
| GC Heap Size (MB) | 0.00 | 0.00 |
| Alloc Rate (MB/s) | 565.50 | 776.76 |
| Time in GC (%) | 1.50 | - |
| ThreadPool Threads | 5 | - |

### STEADY_TOKENS_PER_SEC

#### Metrics
| Metric | Min | P50 | P90 | P95 | P99 | Max | Mean | StdDev |
|--------|-----|-----|-----|-----|-----|-----|------|--------|
| steady_tokens_per_sec | 556.01 | 678.24 | 714.33 | 715.03 | 715.59 | 715.73 | 658.99 | 52.73 |
| overall_tokens_per_sec | 555.46 | 676.41 | 711.15 | 712.18 | 713.01 | 713.22 | 657.65 | 52.14 |

#### Runtime Counters
| Counter | Avg | Peak |
|---------|-----|------|
| CPU Usage (%) | 69.74 | 69.74 |
| Working Set (MB) | 0.00 | 0.00 |
| GC Heap Size (MB) | 0.00 | 0.00 |
| Alloc Rate (MB/s) | 1494.13 | 1494.13 |
| Time in GC (%) | 3.00 | - |
| ThreadPool Threads | 7 | - |

### END_TO_END_LATENCY

#### Metrics
| Metric | Min | P50 | P90 | P95 | P99 | Max | Mean | StdDev |
|--------|-----|-----|-----|-----|-----|-----|------|--------|
| latency_ms | 71.07 | 74.12 | 77.91 | 78.60 | 79.16 | 79.30 | 74.48 | 3.04 |

#### Runtime Counters
| Counter | Avg | Peak |
|---------|-----|------|
| CPU Usage (%) | 71.50 | 71.50 |
| Working Set (MB) | 0.00 | 0.00 |
| GC Heap Size (MB) | 0.00 | 0.00 |
| Alloc Rate (MB/s) | 1803.10 | 1803.10 |
| Time in GC (%) | 4.00 | - |
| ThreadPool Threads | 7 | - |

### MEMORY_FOOTPRINT

#### Memory Footprint
| Metric | Min (MB) | Max (MB) | Avg (MB) |
|--------|----------|----------|----------|
| Working Set | 69.11 | 70.23 | 69.43 |
| Private Memory | 274.16 | 274.98 | 274.29 |
| Managed Heap | 4.19 | 17.51 | 10.83 |

#### Runtime Counters
| Counter | Avg | Peak |
|---------|-----|------|
| CPU Usage (%) | 70.09 | 70.09 |
| Working Set (MB) | 0.00 | 0.00 |
| GC Heap Size (MB) | 0.00 | 0.00 |
| Alloc Rate (MB/s) | 1731.38 | 1731.38 |
| Time in GC (%) | 4.00 | - |
| ThreadPool Threads | 7 | - |

### GC_AND_ALLOCATIONS

#### GC & Allocations
| Metric | Value |
|--------|-------|
| Gen0 Collections | 83 |
| Gen1 Collections | 0 |
| Gen2 Collections | 0 |
| Total Allocated (MB) | 1330.21 |
| Allocations/Op (MB) | 133.02 |

#### Runtime Counters
| Counter | Avg | Peak |
|---------|-----|------|
| CPU Usage (%) | 71.52 | 71.52 |
| Working Set (MB) | 0.00 | 0.00 |
| GC Heap Size (MB) | 0.00 | 0.00 |
| Alloc Rate (MB/s) | 1763.87 | 1763.87 |
| Time in GC (%) | 2.00 | - |
| ThreadPool Threads | 7 | - |

### CONCURRENCY_THROUGHPUT_1

#### Metrics
| Metric | Min | P50 | P90 | P95 | P99 | Max | Mean | StdDev |
|--------|-----|-----|-----|-----|-----|-----|------|--------|
| latency_ms | 69.18 | 69.98 | 77.24 | 78.94 | 80.30 | 80.64 | 72.38 | 3.87 |
| requests_per_sec | 0.00 | 0.00 | 0.00 | 0.00 | 0.00 | 0.00 | 13.78 | 0.00 |
| tokens_per_sec | 0.00 | 0.00 | 0.00 | 0.00 | 0.00 | 0.00 | 688.79 | 0.00 |

#### Runtime Counters
| Counter | Avg | Peak |
|---------|-----|------|
| CPU Usage (%) | 0.00 | 0.00 |
| Working Set (MB) | 0.00 | 0.00 |
| GC Heap Size (MB) | 0.00 | 0.00 |
| Alloc Rate (MB/s) | 0.00 | 0.00 |
| Time in GC (%) | 0.00 | - |
| ThreadPool Threads | 0 | - |

