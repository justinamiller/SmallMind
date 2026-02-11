#!/bin/bash
# Comprehensive MatMul benchmark runner for 60+ GFLOPS optimization
# Usage: ./run-matmul-benchmark.sh [--fast] [--unpacked-only] [--packed-only]

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$SCRIPT_DIR"

echo "Building MatMul Comprehensive Benchmark..."
dotnet build "$PROJECT_ROOT/benchmarks/MatMulComprehensiveBenchmark.csproj" --configuration Release --nologo

echo ""
echo "Running benchmarks..."
echo ""

dotnet run --project "$PROJECT_ROOT/benchmarks/MatMulComprehensiveBenchmark.csproj" \
    --configuration Release \
    --no-build \
    -- "$@"
