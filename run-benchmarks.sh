#!/bin/bash

# SmallMind Comprehensive Benchmarking Script
# Runs all profilers and benchmarks, generates consolidated report

set -e

echo "═══════════════════════════════════════════════════════════════"
echo "  SmallMind Comprehensive Benchmark Runner"
echo "═══════════════════════════════════════════════════════════════"
echo ""

# Change to repository root
cd "$(dirname "$0")"

# Parse arguments
QUICK_MODE=false
SKIP_BUILD=false
OUTPUT_DIR=""
VERBOSE=""

while [[ $# -gt 0 ]]; do
    case $1 in
        --quick)
            QUICK_MODE=true
            shift
            ;;
        --skip-build)
            SKIP_BUILD=true
            shift
            ;;
        --output|-o)
            OUTPUT_DIR="$2"
            shift 2
            ;;
        --verbose|-v)
            VERBOSE="--verbose"
            shift
            ;;
        --help|-h)
            echo "Usage: $0 [options]"
            echo ""
            echo "Options:"
            echo "  --quick           Run in quick mode (fewer iterations)"
            echo "  --skip-build      Skip building projects"
            echo "  --output, -o DIR  Output directory (default: auto-generated)"
            echo "  --verbose, -v     Show verbose output"
            echo "  --help, -h        Show this help message"
            echo ""
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            echo "Use --help for usage information"
            exit 1
            ;;
    esac
done

# Build the runner
if [ "$SKIP_BUILD" = false ]; then
    echo "Building BenchmarkRunner..."
    cd tools/BenchmarkRunner
    dotnet build -c Release > /dev/null 2>&1
    cd ../..
    echo "✓ Build complete"
    echo ""
fi

# Build arguments for BenchmarkRunner
ARGS=""
if [ "$QUICK_MODE" = true ]; then
    ARGS="$ARGS --quick"
fi
if [ "$SKIP_BUILD" = true ]; then
    ARGS="$ARGS --skip-build"
fi
if [ -n "$OUTPUT_DIR" ]; then
    ARGS="$ARGS --output $OUTPUT_DIR"
fi
if [ -n "$VERBOSE" ]; then
    ARGS="$ARGS $VERBOSE"
fi

# Run the benchmark runner
cd tools/BenchmarkRunner
dotnet run -c Release -- $ARGS

echo ""
echo "✓ Benchmarking complete!"
echo ""
echo "To view the consolidated report:"
if [ -n "$OUTPUT_DIR" ]; then
    echo "  cat $OUTPUT_DIR/CONSOLIDATED_BENCHMARK_REPORT.md"
else
    echo "  ls -lt ../../benchmark-results-*/CONSOLIDATED_BENCHMARK_REPORT.md | head -1"
fi

