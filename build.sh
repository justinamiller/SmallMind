#!/usr/bin/env bash
# Build script for SmallMind
set -e

echo "========================================="
echo "SmallMind Build Script"
echo "========================================="

# Restore dependencies
echo ""
echo "Restoring dependencies..."
dotnet restore SmallMind.sln

# Build in Release configuration
echo ""
echo "Building SmallMind.sln in Release configuration..."
dotnet build SmallMind.sln -c Release --no-restore

# Run tests if they exist
echo ""
echo "Running tests..."
if dotnet test SmallMind.sln -c Release --no-build --verbosity minimal; then
    echo ""
    echo "========================================="
    echo "✅ Build and tests completed successfully!"
    echo "========================================="
else
    echo ""
    echo "========================================="
    echo "❌ Tests failed!"
    echo "========================================="
    exit 1
fi
