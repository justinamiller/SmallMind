#!/bin/bash
# Example: Using SmallMind.Server with curl

BASE_URL="${SMALLMIND_URL:-http://localhost:8080}"

echo "SmallMind.Server Client Examples"
echo "================================="
echo "Base URL: $BASE_URL"
echo ""

# Example 1: List models
echo "1. List available models:"
echo "   curl $BASE_URL/v1/models"
echo ""
curl -s "$BASE_URL/v1/models" | jq '.'
echo ""
echo ""

# Example 2: Chat completion (non-streaming)
echo "2. Chat completion (non-streaming):"
echo '   curl -X POST $BASE_URL/v1/chat/completions \'
echo '     -H "Content-Type: application/json" \'
echo '     -d '"'"'{"model":"smallmind","messages":[{"role":"user","content":"Hello"}],"max_tokens":20}'"'"
echo ""
curl -s -X POST "$BASE_URL/v1/chat/completions" \
  -H "Content-Type: application/json" \
  -d '{
    "model": "smallmind",
    "messages": [
      {"role": "user", "content": "Hello, how are you?"}
    ],
    "max_tokens": 20,
    "temperature": 0.7
  }' | jq '.'
echo ""
echo ""

# Example 3: Chat completion (streaming)
echo "3. Chat completion (streaming):"
echo '   curl -X POST $BASE_URL/v1/chat/completions \'
echo '     -H "Content-Type: application/json" \'
echo '     -d '"'"'{"model":"smallmind","messages":[{"role":"user","content":"Count to 5"}],"stream":true}'"'"
echo ""
curl -s -X POST "$BASE_URL/v1/chat/completions" \
  -H "Content-Type: application/json" \
  -d '{
    "model": "smallmind",
    "messages": [
      {"role": "user", "content": "Count to 5"}
    ],
    "stream": true,
    "max_tokens": 30
  }'
echo ""
echo ""

# Example 4: Text completion
echo "4. Text completion:"
echo '   curl -X POST $BASE_URL/v1/completions \'
echo '     -H "Content-Type: application/json" \'
echo '     -d '"'"'{"model":"smallmind","prompt":"Once upon a time","max_tokens":20}'"'"
echo ""
curl -s -X POST "$BASE_URL/v1/completions" \
  -H "Content-Type: application/json" \
  -d '{
    "model": "smallmind",
    "prompt": "Once upon a time",
    "max_tokens": 20
  }' | jq '.'
echo ""
echo ""

echo "================================="
echo "Examples completed!"
