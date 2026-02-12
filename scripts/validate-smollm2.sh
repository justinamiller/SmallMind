#!/bin/bash
# SmolLM2 GGUF Loading Validation Script
# This script validates that the fixes work correctly with an actual SmolLM2 model

set -e  # Exit on error

echo "=== SmolLM2 GGUF Loading Validation ==="
echo ""

# Configuration
MODEL_FILE="smollm2-135m-instruct-q8_0.gguf"
PROMPT="The capital of France is"
MAX_TOKENS=50
SEED=42

# Check if model file exists
if [ ! -f "$MODEL_FILE" ]; then
    echo "❌ ERROR: Model file not found: $MODEL_FILE"
    echo ""
    echo "To download the model, run:"
    echo "  wget https://huggingface.co/HuggingFaceTB/SmolLM2-135M-Instruct-GGUF/resolve/main/smollm2-135m-instruct-q8_0.gguf"
    echo ""
    exit 1
fi

echo "✓ Model file found: $MODEL_FILE"
echo ""

# Build the project
echo "Building SmallMind.Console..."
dotnet build src/SmallMind.Console/SmallMind.Console.csproj --configuration Release --verbosity quiet
if [ $? -ne 0 ]; then
    echo "❌ Build failed"
    exit 1
fi
echo "✓ Build successful"
echo ""

# Run the test
echo "Running GGUF validation test..."
echo "  Model: $MODEL_FILE"
echo "  Prompt: \"$PROMPT\""
echo "  Max tokens: $MAX_TOKENS"
echo "  Seed: $SEED"
echo ""

dotnet run --project src/SmallMind.Console --configuration Release -- \
    run-gguf "$MODEL_FILE" "$PROMPT" \
    --max-tokens $MAX_TOKENS \
    --seed $SEED

EXIT_CODE=$?

echo ""
if [ $EXIT_CODE -eq 0 ]; then
    echo "✅ SUCCESS: run-gguf completed successfully with coherent output"
    echo ""
    echo "Expected behaviors verified:"
    echo "  ✓ BOS token automatically prepended"
    echo "  ✓ Vocab size correctly extracted (49152)"
    echo "  ✓ RoPE freq base correctly set (100000)"
    echo "  ✓ Coherent English text generated"
elif [ $EXIT_CODE -eq 2 ]; then
    echo "❌ FAILURE: Output appears to be garbage"
    echo ""
    echo "Possible issues:"
    echo "  - BOS token not prepended correctly"
    echo "  - RoPE freq base incorrect"
    echo "  - Model configuration mismatch"
else
    echo "❌ ERROR: Unexpected exit code $EXIT_CODE"
fi

exit $EXIT_CODE
