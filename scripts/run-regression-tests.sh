#!/bin/bash
# Run all SmallMind regression tests
# Usage: ./scripts/run-regression-tests.sh [--performance]

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo "🧪 Running SmallMind Regression Tests"
echo "======================================"

# Parse arguments
RUN_PERF=false
if [ "$1" = "--performance" ] || [ "$1" = "--perf" ]; then
    RUN_PERF=true
    export RUN_PERF_TESTS=true
    echo "Performance tests: ENABLED"
else
    echo "Performance tests: DISABLED (use --performance to enable)"
fi
echo ""

# Run unit regression tests
echo "📋 Running correctness & determinism tests..."
dotnet test tests/SmallMind.Tests/SmallMind.Tests.csproj \
    --filter "Category=Regression" \
    --configuration Release \
    --verbosity normal \
    --no-build

if [ $? -eq 0 ]; then
    echo -e "${GREEN}✓ Correctness & determinism tests passed${NC}"
else
    echo -e "${RED}✗ Correctness & determinism tests failed${NC}"
    exit 1
fi

# Run performance tests if enabled
if [ "$RUN_PERF" = true ]; then
    echo ""
    echo "⚡ Running performance & allocation tests..."
    dotnet test tests/SmallMind.PerfTests/SmallMind.PerfTests.csproj \
        --filter "Category=Performance" \
        --configuration Release \
        --verbosity normal \
        --no-build
    
    if [ $? -eq 0 ]; then
        echo -e "${GREEN}✓ Performance & allocation tests passed${NC}"
    else
        echo -e "${RED}✗ Performance & allocation tests failed${NC}"
        exit 1
    fi
fi

echo ""
echo -e "${GREEN}✅ All regression tests passed!${NC}"
