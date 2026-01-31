# Benchmarks

This directory is reserved for performance benchmarking projects.

## Future Plans

Performance benchmarks will be added to:
- Measure training throughput (tokens/sec, batches/sec)
- Measure inference latency (time to first token, end-to-end latency)
- Compare different optimization strategies
- Track performance regression across versions
- Benchmark different model sizes and configurations

## Benchmark Framework

We plan to use BenchmarkDotNet for reliable, statistically sound benchmarks.

## Contribution

If you'd like to contribute benchmarks, please:
1. Use BenchmarkDotNet for consistency
2. Focus on real-world scenarios
3. Include both micro-benchmarks (individual operations) and macro-benchmarks (full workflows)
4. Document what is being measured and why it matters

## Current Performance Tracking

For now, performance metrics are available through the console app:
```bash
dotnet run --project ../src/SmallMind.Console -- --perf
```

See the main README for more details on performance tracking.
