@echo off
REM Run all SmallMind regression tests
REM Usage: scripts\run-regression-tests.bat [--performance]

echo Running SmallMind Regression Tests
echo ======================================

REM Parse arguments
set RUN_PERF=false
if "%1"=="--performance" set RUN_PERF=true
if "%1"=="--perf" set RUN_PERF=true

if "%RUN_PERF%"=="true" (
    set RUN_PERF_TESTS=true
    echo Performance tests: ENABLED
) else (
    echo Performance tests: DISABLED ^(use --performance to enable^)
)
echo.

REM Run unit regression tests
echo Running correctness ^& determinism tests...
dotnet test tests\SmallMind.Tests\SmallMind.Tests.csproj --filter "Category=Regression" --configuration Release --verbosity normal --no-build
if errorlevel 1 (
    echo FAILED: Correctness ^& determinism tests
    exit /b 1
)
echo PASSED: Correctness ^& determinism tests
echo.

REM Run performance tests if enabled
if "%RUN_PERF%"=="true" (
    echo Running performance ^& allocation tests...
    dotnet test tests\SmallMind.PerfTests\SmallMind.PerfTests.csproj --filter "Category=Performance" --configuration Release --verbosity normal --no-build
    if errorlevel 1 (
        echo FAILED: Performance ^& allocation tests
        exit /b 1
    )
    echo PASSED: Performance ^& allocation tests
    echo.
)

echo.
echo All regression tests passed!
