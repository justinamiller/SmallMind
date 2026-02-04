#!/bin/bash
# Quick-start script for ITIL v4 Mastery Pack Demo
# Run from SmallMind root directory

set -e

echo "════════════════════════════════════════════════════════════════════════"
echo "  ITIL v4 Mastery Pack - Quick Start Demo"
echo "════════════════════════════════════════════════════════════════════════"
echo ""

# Check if we're in the right directory
if [ ! -d "data/pretrained/itil_v4_mastery" ]; then
    echo "Error: Please run this script from the SmallMind root directory"
    exit 1
fi

echo "✓ Found ITIL v4 pack"
echo ""

# Build the demo project
echo "🔨 Building demo application..."
cd examples/ItilPackDemo
dotnet build --configuration Release --no-restore

echo ""
echo "🚀 Running demo..."
echo ""

# Run the demo
dotnet run --configuration Release --no-build

echo ""
echo "════════════════════════════════════════════════════════════════════════"
echo "  Demo complete! Check the output above for results."
echo "════════════════════════════════════════════════════════════════════════"
