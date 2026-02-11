#!/bin/bash
# GFLOPS Comparison Script
# Runs benchmarks across main, PR #192, and PR #193 branches
# Generates comprehensive comparison report

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR" && pwd)"
BENCHMARK_DIR="$REPO_ROOT/benchmarks/GFLOPSComparisonBenchmark"
RESULTS_DIR="$REPO_ROOT/benchmark-results/gflops-comparison"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo "╔═══════════════════════════════════════════════════════════════════╗"
echo "║     SmallMind GFLOPS Comparison - Multi-Branch Benchmark          ║"
echo "╚═══════════════════════════════════════════════════════════════════╝"
echo ""

# Save current branch
CURRENT_BRANCH=$(git branch --show-current)
echo -e "${BLUE}Current branch: $CURRENT_BRANCH${NC}"
echo ""

# Check for uncommitted changes
if ! git diff-index --quiet HEAD --; then
    echo -e "${YELLOW}⚠️  Warning: You have uncommitted changes${NC}"
    echo "Stashing changes..."
    git stash push -m "GFLOPS comparison temporary stash"
    STASHED=true
else
    STASHED=false
fi

# Create results directory
mkdir -p "$RESULTS_DIR"

# Function to run benchmark on a branch
run_benchmark() {
    local branch_name=$1
    local label=$2
    
    echo ""
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo -e "${GREEN}Running benchmark on: $label ($branch_name)${NC}"
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    
    # Checkout branch
    echo "Checking out $branch_name..."
    git checkout "$branch_name" 2>/dev/null || {
        echo -e "${RED}Failed to checkout $branch_name${NC}"
        return 1
    }
    
    # Clean and build
    echo "Building in Release mode..."
    cd "$BENCHMARK_DIR"
    dotnet clean -c Release > /dev/null 2>&1
    dotnet build -c Release > /dev/null 2>&1
    
    if [ $? -ne 0 ]; then
        echo -e "${RED}Build failed for $branch_name${NC}"
        cd "$REPO_ROOT"
        return 1
    fi
    
    # Run benchmark
    echo "Running benchmark..."
    dotnet run -c Release --no-build
    
    # Copy results
    local safe_label=$(echo "$label" | tr ' ' '_' | tr '/' '_')
    cp GFLOPS_COMPARISON_RESULTS.json "$RESULTS_DIR/${safe_label}_results.json"
    cp GFLOPS_COMPARISON_RESULTS.md "$RESULTS_DIR/${safe_label}_results.md"
    
    echo -e "${GREEN}✓ Results saved to $RESULTS_DIR/${safe_label}_results.*${NC}"
    
    cd "$REPO_ROOT"
}

# Run benchmarks on each branch
echo -e "${BLUE}Starting benchmark runs...${NC}"
echo ""

# 1. Main branch (baseline)
run_benchmark "main" "Baseline_Main"

# 2. PR #192 branch
run_benchmark "copilot/push-smallmind-matmuls-to-60-gflops" "PR_192_GemmMicrokernels"

# 3. PR #193 branch
run_benchmark "copilot/optimize-matrix-multiplication" "PR_193_FixedIndexing"

# Return to original branch
echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo -e "${BLUE}Returning to original branch: $CURRENT_BRANCH${NC}"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
git checkout "$CURRENT_BRANCH"

# Restore stashed changes if any
if [ "$STASHED" = true ]; then
    echo "Restoring stashed changes..."
    git stash pop
fi

# Generate comparison report
echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo -e "${GREEN}Generating Comparison Report${NC}"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

# Create comparison report
REPORT_FILE="$RESULTS_DIR/COMPARISON_REPORT.md"

cat > "$REPORT_FILE" << EOF
# GFLOPS Comparison Report - PR #192 vs PR #193

**Generated:** $(date -u +"%Y-%m-%d %H:%M:%S UTC")

This report compares the performance of three implementations:
- **Baseline (Main)**: Current production code
- **PR #192**: Routes MatMulOps to GemmMicrokernels for zero allocations
- **PR #193**: Fixes GemmMicrokernels A-indexing bug

## Quick Summary

EOF

# Extract key metrics from JSON files using simple grep/sed (works without jq)
echo "Extracting metrics from results..."

for result_file in "$RESULTS_DIR"/*.json; do
    if [ -f "$result_file" ]; then
        label=$(basename "$result_file" .json | sed 's/_results$//')
        echo "Processing: $label"
        
        # Extract max GFLOPS - simple approach
        max_gflops=$(grep -o '"MaxGFLOPS":[^,]*' "$result_file" | head -1 | cut -d':' -f2 | tr -d ' ')
        avg_gflops=$(grep -o '"AvgGFLOPS":[^,]*' "$result_file" | head -1 | cut -d':' -f2 | tr -d ' ')
        zero_alloc=$(grep -o '"ZeroAllocationCount":[^,]*' "$result_file" | head -1 | cut -d':' -f2 | tr -d ' ')
        
        cat >> "$REPORT_FILE" << METRICS
### $label
- **Peak GFLOPS:** $max_gflops
- **Average GFLOPS:** $avg_gflops
- **Zero-Allocation Tests:** $zero_alloc

METRICS
    fi
done

cat >> "$REPORT_FILE" << 'EOF'

## Detailed Results

See individual result files for complete data:

- [Baseline (Main) Results](Baseline_Main_results.md)
- [PR #192 Results](PR_192_GemmMicrokernels_results.md)
- [PR #193 Results](PR_193_FixedIndexing_results.md)

## Comparison Analysis

### Performance Comparison

Compare the Peak and Average GFLOPS across implementations to identify the fastest approach.

### Memory Efficiency Comparison

Check the "Zero-Allocation Tests" count to see which implementation has better memory efficiency.

### LLM Workload Performance

Review the detailed markdown reports to see performance on specific LLM patterns:
- Single token decode (M=1)
- Batch processing (M=32)
- Prefill operations (M=256, M=512)

### Recommendations

Based on the results above:

1. **For Peak Performance:** Choose the implementation with highest Peak GFLOPS
2. **For Memory Efficiency:** Choose the implementation with most zero-allocation tests
3. **For Inference (M=1):** Check the detailed reports for single-token decode performance
4. **For Prefill:** Check the detailed reports for large M values

## Test Environment

All benchmarks were run on the same machine with identical conditions:
- .NET Release mode
- Same CPU, memory, OS
- Sequential execution to avoid thermal throttling

See individual reports for detailed system information.

EOF

echo -e "${GREEN}✓ Comparison report generated: $REPORT_FILE${NC}"

# Display summary
echo ""
echo "╔═══════════════════════════════════════════════════════════════════╗"
echo "║                    Benchmark Complete!                             ║"
echo "╚═══════════════════════════════════════════════════════════════════╝"
echo ""
echo "Results location: $RESULTS_DIR"
echo ""
echo "Files generated:"
echo "  • Baseline_Main_results.{json,md}"
echo "  • PR_192_GemmMicrokernels_results.{json,md}"
echo "  • PR_193_FixedIndexing_results.{json,md}"
echo "  • COMPARISON_REPORT.md"
echo ""
echo -e "${BLUE}Review the COMPARISON_REPORT.md file for analysis.${NC}"
