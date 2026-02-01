# SmallMind Performance Regression Tests

This project contains performance regression tests for SmallMind's core operations.

## Purpose

These tests establish baseline performance metrics and fail only on significant performance regressions. They are designed to be:

- **Conservative**: Thresholds allow for variation across different hardware
- **Gated**: Only run when explicitly enabled to avoid slowing down regular test runs
- **Meaningful**: Focus on operations that matter for real-world performance

## Running the Tests

By default, these tests are **disabled** (they pass without running). To enable them:

### Option 1: Environment Variable

```bash
RUN_PERF_TESTS=true dotnet test tests/SmallMind.PerfTests
```

### Option 2: CI/CD Integration

Set the `RUN_PERF_TESTS` environment variable in your CI/CD pipeline for scheduled runs or performance-labeled PRs.

Example GitHub Actions:

```yaml
- name: Run Performance Tests
  if: contains(github.event.pull_request.labels.*.name, 'perf') || github.event_name == 'schedule'
  run: RUN_PERF_TESTS=true dotnet test tests/SmallMind.PerfTests
  env:
    RUN_PERF_TESTS: true
```

## Test Coverage

### Matrix Operations
- **MatMul 128×128**: < 10ms per operation
- **MatMul 256×256**: < 80ms per operation

### Activation Functions
- **ReLU (10M elements)**: < 50ms per operation
- **GELU (1M elements)**: < 30ms per operation

### Softmax
- **Softmax (4096 elements)**: < 2ms per operation
- **Softmax (8192 elements)**: < 5ms per operation

### Dot Product
- **DotProduct (4096 elements)**: < 50µs per operation

## Interpreting Results

If a test fails, it means performance has regressed significantly beyond the threshold. Possible causes:

1. **Code changes** introduced a performance regression
2. **Hardware differences** - thresholds may need adjustment for different environments
3. **System load** - other processes may have slowed down the test

## Adjusting Thresholds

Thresholds are intentionally conservative. If you find that tests fail consistently on your hardware despite good code, you can:

1. Measure baseline performance on your target hardware
2. Update thresholds in `PerformanceRegressionTests.cs`
3. Document the hardware and rationale in commit messages

## Best Practices

- Run perf tests in **isolated environments** (no other CPU-intensive tasks)
- Use **Release builds** for accurate measurements
- Run **multiple times** if results seem inconsistent
- Consider **hardware specs** when interpreting results (CPU model, clock speed, etc.)
