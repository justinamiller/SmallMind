# SmallMind Performance Benchmarks
# PowerShell script to run performance benchmarks

Write-Host "=== SmallMind Performance Benchmarks ===" -ForegroundColor Cyan
Write-Host ""

# Navigate to benchmark directory
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Split-Path -Parent $ScriptDir
$BenchmarkDir = Join-Path $RepoRoot "src\SmallMind.Benchmarks"

Set-Location $BenchmarkDir

# Check configuration
$Configuration = if ($args.Count -gt 0) { $args[0] } else { "Release" }

if ($Configuration -ne "Release") {
    Write-Host "WARNING: Not running in Release mode!" -ForegroundColor Yellow
    Write-Host "Results may not be representative." -ForegroundColor Yellow
    Write-Host ""
}

# Run benchmarks
Write-Host "Running benchmarks in $Configuration mode..." -ForegroundColor Green
dotnet run --configuration $Configuration

Write-Host ""
Write-Host "=== Benchmark Complete ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Reports generated in: $RepoRoot\artifacts\perf\" -ForegroundColor Green
Write-Host "- perf-results-latest.json"
Write-Host "- perf-results-latest.md"
Write-Host ""
Write-Host "To view the markdown report:"
Write-Host "  Get-Content $RepoRoot\artifacts\perf\perf-results-latest.md"
