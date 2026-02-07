# Test Performance Guidelines

## Overview

This document outlines the performance requirements for tests in the SmallMind project and how to ensure they are met.

## Performance Requirements

**All tests must complete within 60 seconds (1 minute).**

This requirement ensures:
- Fast feedback during development
- Efficient CI/CD pipelines
- Early detection of performance regressions
- Good developer experience

## Current Performance Status

As of the latest verification:

### Integration Tests (SmallMind.IntegrationTests)
- **Total tests**: 14
- **Total time**: ~1.1 seconds
- **Status**: ✅ All tests pass
- **Performance**: Excellent - all tests complete in under 2 seconds individually

### Unit Tests (SmallMind.Tests)
- **Total tests**: 837
- **Total time**: ~4.6 seconds
- **Status**: 816 passing, 21 failing (failures unrelated to performance)
- **Performance**: Excellent - slowest test takes ~2 seconds

## Configuration

Test performance is monitored through xUnit configuration:

### xunit.runner.json

Both test projects include a `xunit.runner.json` file with the following settings:

```json
{
  "longRunningTestSeconds": 60,
  "parallelizeAssembly": true,
  "parallelizeTestCollections": true
}
```

**Key Settings:**
- `longRunningTestSeconds: 60` - xUnit will report any test that takes longer than 60 seconds as a long-running test
- `parallelizeAssembly: true` - Enables parallel test execution for better performance
- `parallelizeTestCollections: true` - Allows test collections to run in parallel

## How to Verify Test Performance

### Running All Tests

```bash
# Run integration tests
dotnet test tests/SmallMind.IntegrationTests/SmallMind.IntegrationTests.csproj

# Run unit tests
dotnet test tests/SmallMind.Tests/SmallMind.Tests.csproj

# Run all tests
dotnet test
```

### Running with Detailed Timing

```bash
# Show detailed test execution times
dotnet test --logger "console;verbosity=detailed"
```

### Running Performance Tests Only

```bash
# Run only performance monitoring tests
dotnet test --filter "Category=Performance"
```

## Test Performance Monitoring

The `TestPerformanceTests` class in the integration test project includes tests that verify the performance configuration:

1. **AllTests_ShouldCompleteWithinOneMinute** - Documents the performance requirement
2. **SampleQuickTest_CompletesInUnder1Second** - Verifies quick test execution
3. **VerifyTestTimeoutConfiguration** - Validates timeout settings

## Best Practices for Test Performance

### DO:
✅ Keep tests focused and minimal  
✅ Use small test datasets  
✅ Mock expensive operations when testing business logic  
✅ Use `[Fact]` for simple tests that should complete quickly  
✅ Profile slow tests to identify bottlenecks  
✅ Consider using `[Theory]` with inline data for parameterized tests  

### DON'T:
❌ Train large models in tests  
❌ Process large datasets unnecessarily  
❌ Add `Thread.Sleep()` calls unless absolutely necessary  
❌ Make external network calls without mocking  
❌ Perform expensive I/O operations repeatedly  

## Handling Slow Tests

If a test legitimately needs more time (e.g., integration test with real training):

1. **Optimize first** - Try to reduce the test scope or use smaller models
2. **Use test fixtures** - Share expensive setup across multiple tests
3. **Consider moving to performance tests** - If the test is measuring performance, move it to a separate performance test suite

### Example: Optimizing a Training Test

```csharp
// ❌ SLOW - Training with large model
[Fact]
public void TrainModel_Succeeds()
{
    var model = new TransformerModel(
        vocabSize: 50000,
        blockSize: 2048,
        nEmbd: 768,
        nLayer: 12,
        nHead: 12
    );
    // This will take several minutes!
}

// ✅ FAST - Training with tiny model
[Fact]
public void TrainModel_Succeeds()
{
    var model = new TransformerModel(
        vocabSize: 33,
        blockSize: 16,
        nEmbd: 16,
        nLayer: 1,
        nHead: 2
    );
    // This completes in milliseconds!
}
```

## Monitoring in CI/CD

The test timeout is automatically enforced in CI/CD pipelines. If a test exceeds 60 seconds:

1. xUnit will report it as a long-running test
2. CI logs will show the warning
3. The test will still pass (unless it times out completely)
4. Developers should investigate and optimize the test

## Troubleshooting

### Test Takes Longer Than Expected

1. Check if test is using production-sized models/datasets
2. Verify no unnecessary `Thread.Sleep()` calls
3. Profile the test to identify bottlenecks
4. Consider mocking expensive operations
5. Review test dependencies and fixtures

### Long-Running Test Warning

If you see a warning about long-running tests:

```
[xUnit.net] Test 'MyTest' running for more than 60 seconds
```

Take action:
1. Investigate why the test is slow
2. Optimize the test implementation
3. Reduce test data size
4. Consider splitting into multiple smaller tests

## Summary

- ✅ **Requirement**: All tests must complete in under 60 seconds
- ✅ **Current Status**: All tests complete well within this limit
- ✅ **Monitoring**: Automated via xunit.runner.json configuration
- ✅ **Enforcement**: Warnings for tests exceeding 60 seconds

For questions or concerns about test performance, please open an issue on GitHub.
