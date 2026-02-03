# SmallMind Benchmark Results

**Generated:** 2026-02-03 19:27:03
**Generated (UTC):** 2026-02-03 19:27:03 UTC

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
| Repo Commit | 8956df712ae901050571d82fb74b20f5920181ae |

## Run Configuration

| Parameter | Value |
|-----------|-------|
| Model Path | `benchmark-model.smq` |
| Scenario | ttft,tokens_per_sec |
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
| ttft_ms | 1.22 | 1.35 | 1.59 | 1.64 | 1.74 | 1.78 | 1.39 | 0.14 |
| latency_ms | 319.22 | 326.76 | 337.92 | 339.73 | 342.69 | 343.58 | 328.61 | 6.97 |

#### Runtime Counters
| Counter | Avg | Peak |
|---------|-----|------|
| CPU Usage (%) | 73.94 | 74.99 |
| Working Set (MB) | 0.00 | 0.00 |
| GC Heap Size (MB) | 0.00 | 0.00 |
| Alloc Rate (MB/s) | 1036.39 | 1057.73 |
| Time in GC (%) | 0.60 | - |
| ThreadPool Threads | 8 | - |

### STEADY_TOKENS_PER_SEC

#### Metrics
| Metric | Min | P50 | P90 | P95 | P99 | Max | Mean | StdDev |
|--------|-----|-----|-----|-----|-----|-----|------|--------|
| steady_tokens_per_sec | 761.11 | 787.21 | 794.32 | 795.65 | 799.03 | 800.21 | 784.12 | 10.83 |
| overall_tokens_per_sec | 760.80 | 787.03 | 794.41 | 795.20 | 798.93 | 800.31 | 784.01 | 10.80 |

#### Runtime Counters
| Counter | Avg | Peak |
|---------|-----|------|
| CPU Usage (%) | 73.97 | 74.79 |
| Working Set (MB) | 0.00 | 0.00 |
| GC Heap Size (MB) | 0.00 | 0.00 |
| Alloc Rate (MB/s) | 1043.69 | 1061.21 |
| Time in GC (%) | 0.20 | - |
| ThreadPool Threads | 8 | - |

