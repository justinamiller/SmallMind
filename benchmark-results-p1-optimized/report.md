# SmallMind Benchmark Results

**Generated:** 2026-02-03 19:37:12
**Generated (UTC):** 2026-02-03 19:37:12 UTC

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
| Repo Commit | dd71a5783811480658be92ae0ff01303b5a7de83 |

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
| ttft_ms | 0.88 | 1.04 | 1.37 | 1.52 | 1.69 | 1.75 | 1.10 | 0.22 |
| latency_ms | 203.03 | 213.00 | 249.53 | 286.36 | 321.26 | 327.38 | 224.81 | 29.04 |

#### Runtime Counters
| Counter | Avg | Peak |
|---------|-----|------|
| CPU Usage (%) | 83.43 | 90.90 |
| Working Set (MB) | 0.00 | 0.00 |
| GC Heap Size (MB) | 0.00 | 0.00 |
| Alloc Rate (MB/s) | 1474.29 | 1646.40 |
| Time in GC (%) | 1.14 | - |
| ThreadPool Threads | 10 | - |

### STEADY_TOKENS_PER_SEC

#### Metrics
| Metric | Min | P50 | P90 | P95 | P99 | Max | Mean | StdDev |
|--------|-----|-----|-----|-----|-----|-----|------|--------|
| steady_tokens_per_sec | 1052.11 | 1221.36 | 1242.99 | 1248.23 | 1256.02 | 1257.94 | 1196.37 | 55.16 |
| overall_tokens_per_sec | 1050.55 | 1219.11 | 1242.52 | 1247.51 | 1255.39 | 1257.31 | 1195.32 | 55.11 |

#### Runtime Counters
| Counter | Avg | Peak |
|---------|-----|------|
| CPU Usage (%) | 88.90 | 92.15 |
| Working Set (MB) | 0.00 | 0.00 |
| GC Heap Size (MB) | 0.00 | 0.00 |
| Alloc Rate (MB/s) | 1578.18 | 1655.41 |
| Time in GC (%) | 1.00 | - |
| ThreadPool Threads | 10 | - |

