#!/bin/bash
set -e

echo "=== SmallMind Performance Benchmarks ==="
echo ""

# Navigate to benchmark directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(dirname "$SCRIPT_DIR")"
BENCHMARK_DIR="$REPO_ROOT/benchmarks/SmallMind.Benchmarks.Metrics"

cd "$BENCHMARK_DIR"

# Check if running in Release mode
if [ "${1:-Release}" != "Release" ]; then
    echo "WARNING: Not running in Release mode!"
    echo "Results may not be representative."
    echo ""
fi

# Run benchmarks
echo "Running benchmarks in ${1:-Release} mode..."
dotnet run --configuration "${1:-Release}"

echo ""
echo "=== Benchmark Complete ==="
echo ""
echo "Reports generated in: $REPO_ROOT/artifacts/perf/"
echo "- perf-results-latest.json"
echo "- perf-results-latest.md"
echo ""
echo "To view the markdown report:"
echo "  cat $REPO_ROOT/artifacts/perf/perf-results-latest.md"
