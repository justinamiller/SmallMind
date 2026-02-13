using System.Text.Json.Serialization;

namespace SmallMind.Server.Models;

#region Chat Completion Models

public sealed class ChatCompletionRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("messages")]
    public List<ChatMessage> Messages { get; set; } = new();

    [JsonPropertyName("temperature")]
    public float? Temperature { get; set; }

    [JsonPropertyName("top_p")]
    public float? TopP { get; set; }

    [JsonPropertyName("max_tokens")]
    public int? MaxTokens { get; set; }

    [JsonPropertyName("stream")]
    public bool Stream { get; set; }

    [JsonPropertyName("stop")]
    public object? Stop { get; set; }

    [JsonPropertyName("seed")]
    public uint? Seed { get; set; }

    public string[] GetStopSequences()
    {
        if (Stop == null) return Array.Empty<string>();
        if (Stop is string str) return new[] { str };
        if (Stop is List<object> list)
        {
            var result = new string[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                result[i] = list[i]?.ToString() ?? string.Empty;
            }
            return result;
        }
        return Array.Empty<string>();
    }
}

public sealed class ChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

public sealed class ChatCompletionResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("object")]
    public string Object { get; set; } = "chat.completion";

    [JsonPropertyName("created")]
    public long Created { get; set; }

    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("choices")]
    public List<ChatChoice> Choices { get; set; } = new();

    [JsonPropertyName("usage")]
    public UsageInfo? Usage { get; set; }
}

public sealed class ChatCompletionChunk
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("object")]
    public string Object { get; set; } = "chat.completion.chunk";

    [JsonPropertyName("created")]
    public long Created { get; set; }

    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("choices")]
    public List<ChatChoiceDelta> Choices { get; set; } = new();
}

public sealed class ChatChoice
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("message")]
    public ChatMessage Message { get; set; } = new();

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

public sealed class ChatChoiceDelta
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("delta")]
    public ChatMessageDelta Delta { get; set; } = new();

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

public sealed class ChatMessageDelta
{
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }
}

#endregion

#region Completion Models

public sealed class CompletionRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("prompt")]
    public string Prompt { get; set; } = string.Empty;

    [JsonPropertyName("temperature")]
    public float? Temperature { get; set; }

    [JsonPropertyName("top_p")]
    public float? TopP { get; set; }

    [JsonPropertyName("max_tokens")]
    public int? MaxTokens { get; set; }

    [JsonPropertyName("stream")]
    public bool Stream { get; set; }

    [JsonPropertyName("stop")]
    public object? Stop { get; set; }

    [JsonPropertyName("seed")]
    public uint? Seed { get; set; }

    public string[] GetStopSequences()
    {
        if (Stop == null) return Array.Empty<string>();
        if (Stop is string str) return new[] { str };
        if (Stop is List<object> list)
        {
            var result = new string[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                result[i] = list[i]?.ToString() ?? string.Empty;
            }
            return result;
        }
        return Array.Empty<string>();
    }
}

public sealed class CompletionResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("object")]
    public string Object { get; set; } = "text_completion";

    [JsonPropertyName("created")]
    public long Created { get; set; }

    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("choices")]
    public List<CompletionChoice> Choices { get; set; } = new();

    [JsonPropertyName("usage")]
    public UsageInfo? Usage { get; set; }
}

public sealed class CompletionChoice
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

#endregion

#region Embedding Models

public sealed class EmbeddingRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("input")]
    public object Input { get; set; } = string.Empty;

    public string[] GetInputs()
    {
        if (Input is string str) return new[] { str };
        if (Input is List<object> list)
        {
            var result = new string[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                result[i] = list[i]?.ToString() ?? string.Empty;
            }
            return result;
        }
        return Array.Empty<string>();
    }
}

public sealed class EmbeddingResponse
{
    [JsonPropertyName("object")]
    public string Object { get; set; } = "list";

    [JsonPropertyName("data")]
    public List<EmbeddingData> Data { get; set; } = new();

    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("usage")]
    public UsageInfo Usage { get; set; } = new();
}

public sealed class EmbeddingData
{
    [JsonPropertyName("object")]
    public string Object { get; set; } = "embedding";

    [JsonPropertyName("embedding")]
    public float[] Embedding { get; set; } = Array.Empty<float>();

    [JsonPropertyName("index")]
    public int Index { get; set; }
}

#endregion

#region Model List Models

public sealed class ModelListResponse
{
    [JsonPropertyName("object")]
    public string Object { get; set; } = "list";

    [JsonPropertyName("data")]
    public List<ModelInfo> Data { get; set; } = new();
}

public sealed class ModelInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("object")]
    public string Object { get; set; } = "model";

    [JsonPropertyName("created")]
    public long Created { get; set; }

    [JsonPropertyName("owned_by")]
    public string OwnedBy { get; set; } = "smallmind";
}

#endregion

#region Common Models

public sealed class UsageInfo
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
}

public sealed class ErrorResponse
{
    [JsonPropertyName("error")]
    public ErrorDetail Error { get; set; } = new();
}

public sealed class ErrorDetail
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "invalid_request_error";

    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("param")]
    public string? Param { get; set; }
}

#endregion
