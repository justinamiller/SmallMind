#!/bin/bash
# Quick demonstration of GFLOPS Comparison Benchmark
# Runs the benchmark on the current branch only (for testing)

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BENCHMARK_DIR="$SCRIPT_DIR/benchmarks/GFLOPSComparisonBenchmark"

echo "╔═══════════════════════════════════════════════════════════════════╗"
echo "║     SmallMind GFLOPS Comparison - Single Branch Test              ║"
echo "╚═══════════════════════════════════════════════════════════════════╝"
echo ""

echo "Building benchmark in Release mode..."
cd "$BENCHMARK_DIR"
dotnet build -c Release --no-restore > /dev/null 2>&1

if [ $? -ne 0 ]; then
    echo "❌ Build failed"
    exit 1
fi

echo "✓ Build successful"
echo ""
echo "Running benchmark (this may take a few minutes)..."
echo ""

dotnet run -c Release --no-build

echo ""
echo "╔═══════════════════════════════════════════════════════════════════╗"
echo "║                    Benchmark Complete!                             ║"
echo "╚═══════════════════════════════════════════════════════════════════╝"
echo ""
echo "Results saved to:"
echo "  • GFLOPS_COMPARISON_RESULTS.json"
echo "  • GFLOPS_COMPARISON_RESULTS.md"
