@echo off
REM SmallMind Comprehensive Benchmarking Script
REM Runs all profilers and benchmarks, generates consolidated report

setlocal enabledelayedexpansion

echo ===============================================================
echo   SmallMind Comprehensive Benchmark Runner
echo ===============================================================
echo.

REM Change to script directory
cd /d "%~dp0"

REM Parse arguments
set QUICK_MODE=0
set SKIP_BUILD=0
set OUTPUT_DIR=
set VERBOSE=

:parse_args
if "%~1"=="" goto end_parse
if /i "%~1"=="--quick" (
    set QUICK_MODE=1
    shift
    goto parse_args
)
if /i "%~1"=="--skip-build" (
    set SKIP_BUILD=1
    shift
    goto parse_args
)
if /i "%~1"=="--output" (
    set OUTPUT_DIR=%~2
    shift
    shift
    goto parse_args
)
if /i "%~1"=="-o" (
    set OUTPUT_DIR=%~2
    shift
    shift
    goto parse_args
)
if /i "%~1"=="--verbose" (
    set VERBOSE=--verbose
    shift
    goto parse_args
)
if /i "%~1"=="-v" (
    set VERBOSE=--verbose
    shift
    goto parse_args
)
if /i "%~1"=="--help" goto show_help
if /i "%~1"=="-h" goto show_help

echo Unknown option: %~1
echo Use --help for usage information
exit /b 1

:show_help
echo Usage: %~nx0 [options]
echo.
echo Options:
echo   --quick           Run in quick mode (fewer iterations)
echo   --skip-build      Skip building projects
echo   --output, -o DIR  Output directory (default: auto-generated)
echo   --verbose, -v     Show verbose output
echo   --help, -h        Show this help message
echo.
exit /b 0

:end_parse

REM Build the runner
if %SKIP_BUILD%==0 (
    echo Building BenchmarkRunner...
    cd tools\BenchmarkRunner
    dotnet build -c Release >nul 2>&1
    cd ..\..
    echo √ Build complete
    echo.
)

REM Build arguments
set ARGS=
if %QUICK_MODE%==1 set ARGS=%ARGS% --quick
if %SKIP_BUILD%==1 set ARGS=%ARGS% --skip-build
if not "%OUTPUT_DIR%"=="" set ARGS=%ARGS% --output "%OUTPUT_DIR%"
if not "%VERBOSE%"=="" set ARGS=%ARGS% %VERBOSE%

REM Run the benchmark runner
cd tools\BenchmarkRunner
dotnet run -c Release -- %ARGS%

echo.
echo √ Benchmarking complete!
echo.
echo To view the consolidated report:
if not "%OUTPUT_DIR%"=="" (
    echo   type "%OUTPUT_DIR%\CONSOLIDATED_BENCHMARK_REPORT.md"
) else (
    echo   dir /b /od ..\..\benchmark-results-*\CONSOLIDATED_BENCHMARK_REPORT.md
)

endlocal
