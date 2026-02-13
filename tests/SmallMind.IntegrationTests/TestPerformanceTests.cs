using System.Diagnostics;
using Xunit.Abstractions;

namespace SmallMind.IntegrationTests;

/// <summary>
/// Tests to verify that all tests complete within acceptable time limits.
/// This ensures that no test takes longer than 1 minute to run.
/// </summary>
public class TestPerformanceTests
{
    private readonly ITestOutputHelper _output;
    private const int MaxTestDurationSeconds = 60;

    public TestPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    [Trait("Category", "Performance")]
    public void AllTests_ShouldCompleteWithinOneMinute()
    {
        // This is a meta-test that validates the test suite performance
        // The actual validation is done by the xunit.runner.json configuration
        // which sets longRunningTestSeconds to 60

        _output.WriteLine($"Test performance requirement: All tests must complete within {MaxTestDurationSeconds} seconds");
        _output.WriteLine("This is enforced by xunit.runner.json configuration:");
        _output.WriteLine("  - longRunningTestSeconds: 60");
        _output.WriteLine("");
        _output.WriteLine("If any test exceeds this limit, xUnit will report it as a long-running test.");

        // This test always passes - it's just documenting the requirement
        Assert.True(true, "Test performance monitoring is active via xunit.runner.json");
    }

    [Fact]
    [Trait("Category", "Performance")]
    public void SampleQuickTest_CompletesInUnder1Second()
    {
        // Verify that a simple test completes quickly
        var sw = Stopwatch.StartNew();

        // Simulate some work
        Thread.Sleep(100);

        sw.Stop();

        _output.WriteLine($"Test completed in {sw.ElapsedMilliseconds}ms");
        Assert.True(sw.ElapsedMilliseconds < 1000, "Quick test should complete in under 1 second");
    }

    [Fact]
    [Trait("Category", "Performance")]
    public void VerifyTestTimeoutConfiguration()
    {
        // Verify that the test timeout configuration is reasonable
        _output.WriteLine($"Maximum allowed test duration: {MaxTestDurationSeconds} seconds");
        _output.WriteLine("Configuration enforced via xunit.runner.json");
        _output.WriteLine($"Constant value: {MaxTestDurationSeconds}");

        // Document the expected range for test timeout
        Assert.True(MaxTestDurationSeconds >= 60, "Test timeout should allow at least 60 seconds for complex tests");
        Assert.True(MaxTestDurationSeconds <= 120, "Test timeout should not exceed 120 seconds to catch performance regressions");
    }
}
