#!/bin/bash
# Quick test script to verify SmallMind.Server functionality

echo "SmallMind.Server Test Script"
echo "=============================="
echo ""

# Check for model file
if [ ! -f "../../../benchmark-model.smq" ]; then
    echo "ERROR: Model file not found at ../../../benchmark-model.smq"
    exit 1
fi

echo "✓ Model file found"

# Start the server in background
echo "Starting server..."
dotnet run --no-build --no-restore -- \
    --ServerOptions:ModelPath=../../../benchmark-model.smq \
    --ServerOptions:Port=8765 &

SERVER_PID=$!
echo "Server started with PID: $SERVER_PID"

# Wait for server to start
echo "Waiting for server to initialize..."
sleep 5

# Test health endpoint
echo ""
echo "Testing /healthz endpoint..."
HEALTH_RESPONSE=$(curl -s http://localhost:8765/healthz)
echo "Response: $HEALTH_RESPONSE"

if echo "$HEALTH_RESPONSE" | grep -q "healthy"; then
    echo "✓ Health check passed"
else
    echo "✗ Health check failed"
    kill $SERVER_PID 2>/dev/null
    exit 1
fi

# Test readiness endpoint
echo ""
echo "Testing /readyz endpoint..."
READY_RESPONSE=$(curl -s http://localhost:8765/readyz)
echo "Response: $READY_RESPONSE"

if echo "$READY_RESPONSE" | grep -q "ready"; then
    echo "✓ Readiness check passed"
else
    echo "✗ Readiness check failed"
    kill $SERVER_PID 2>/dev/null
    exit 1
fi

# Test models endpoint
echo ""
echo "Testing /v1/models endpoint..."
MODELS_RESPONSE=$(curl -s http://localhost:8765/v1/models)
echo "Response: $MODELS_RESPONSE"

if echo "$MODELS_RESPONSE" | grep -q "benchmark-model"; then
    echo "✓ Models endpoint passed"
else
    echo "✗ Models endpoint failed"
    kill $SERVER_PID 2>/dev/null
    exit 1
fi

# Test chat completions (non-streaming)
echo ""
echo "Testing /v1/chat/completions endpoint..."
CHAT_RESPONSE=$(curl -s http://localhost:8765/v1/chat/completions \
    -H "Content-Type: application/json" \
    -d '{
        "model": "smallmind",
        "messages": [{"role": "user", "content": "Hello"}],
        "max_tokens": 10
    }')
echo "Response: $CHAT_RESPONSE"

if echo "$CHAT_RESPONSE" | grep -q "choices"; then
    echo "✓ Chat completions endpoint passed"
else
    echo "✗ Chat completions endpoint failed"
    kill $SERVER_PID 2>/dev/null
    exit 1
fi

# Cleanup
echo ""
echo "Shutting down server..."
kill $SERVER_PID 2>/dev/null
wait $SERVER_PID 2>/dev/null

echo ""
echo "=============================="
echo "All tests passed! ✓"
echo "=============================="
