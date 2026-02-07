# SmallMind Benchmark Results

**Generated:** 2026-02-04 00:42:04
**Generated (UTC):** 2026-02-04 00:42:04 UTC

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
| Repo Commit | bdefb2a1e76be78d65a3fc09835a061607c806f1 |

## Run Configuration

| Parameter | Value |
|-----------|-------|
| Model Path | `/home/runner/work/SmallMind/SmallMind/benchmark-model.smq` |
| Scenario | gc |
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

### GC_AND_ALLOCATIONS

#### GC & Allocations
| Metric | Value |
|--------|-------|
| Gen0 Collections | 632 |
| Gen1 Collections | 0 |
| Gen2 Collections | 0 |
| Total Allocated (MB) | 10098.72 |
| Allocations/Op (MB) | 336.62 |

#### Runtime Counters
| Counter | Avg | Peak |
|---------|-----|------|
| CPU Usage (%) | 49.52 | 50.41 |
| Working Set (MB) | 0.00 | 0.00 |
| GC Heap Size (MB) | 0.00 | 0.00 |
| Alloc Rate (MB/s) | 1057.02 | 1097.95 |
| Time in GC (%) | 0.33 | - |
| ThreadPool Threads | 7 | - |

