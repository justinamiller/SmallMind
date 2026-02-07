#!/bin/bash

# JIT Mode Matrix Test Script
# Tests MatMul performance under different JIT compilation modes

BENCHMARK="dotnet run --project benchmarks/MatMulBenchmark.csproj -c Release -- --size 512 --warmup 20 --iters 100"

echo "========================================="
echo "MatMul JIT Mode Matrix Test"
echo "========================================="
echo ""

echo "Test 1: TieredCompilation=0 (Full optimization, no tiering)"
echo "-----------------------------------------------------------"
DOTNET_TieredCompilation=0 $BENCHMARK
echo ""
echo ""

echo "Test 2: TieredCompilation=1 (Default, with tiering)"
echo "-----------------------------------------------------------"
DOTNET_TieredCompilation=1 $BENCHMARK
echo ""
echo ""

echo "Test 3: TieredCompilation=1 + TieredPGO=0 (Tiering without PGO)"
echo "-----------------------------------------------------------"
DOTNET_TieredCompilation=1 DOTNET_TieredPGO=0 $BENCHMARK
echo ""
echo ""

echo "Test 4: TieredCompilation=1 + TieredPGO=1 (Default, with PGO)"
echo "-----------------------------------------------------------"
DOTNET_TieredCompilation=1 DOTNET_TieredPGO=1 $BENCHMARK
echo ""
echo ""

echo "========================================="
echo "JIT Mode Matrix Test Complete"
echo "========================================="
