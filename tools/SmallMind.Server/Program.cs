using Microsoft.AspNetCore.Mvc;
using SmallMind;
using SmallMind.Server;
using SmallMind.Server.Models;
using SmallMind.Server.Services;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

var serverOptions = new ServerOptions();
builder.Configuration.GetSection("ServerOptions").Bind(serverOptions);

builder.Services.AddSingleton(serverOptions);
builder.Services.AddSingleton<RequestQueue>(sp => 
    new RequestQueue(serverOptions.MaxConcurrentRequests, serverOptions.MaxQueueDepth));
builder.Services.AddSingleton<ServerMetrics>();
builder.Services.AddSingleton<ISmallMindEngine>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Program>>();
    
    string? modelPath = serverOptions.ModelPath;
    
    if (string.IsNullOrEmpty(modelPath) && !string.IsNullOrEmpty(serverOptions.ModelId))
    {
        var registry = new SmallMind.ModelRegistry.ModelRegistry(serverOptions.CacheDir);
        modelPath = registry.GetModelFilePath(serverOptions.ModelId);
        
        if (modelPath == null)
        {
            throw new InvalidOperationException($"Model '{serverOptions.ModelId}' not found in registry");
        }
        
        logger.LogInformation("Loaded model '{ModelId}' from registry: {ModelPath}", serverOptions.ModelId, modelPath);
    }
    
    if (string.IsNullOrEmpty(modelPath))
    {
        throw new InvalidOperationException("Either ModelPath or ModelId must be specified");
    }
    
    if (!File.Exists(modelPath))
    {
        throw new FileNotFoundException($"Model file not found: {modelPath}");
    }
    
    logger.LogInformation("Loading model from: {ModelPath}", modelPath);
    
    var engineOptions = new SmallMindOptions
    {
        ModelPath = modelPath,
        MaxContextTokens = serverOptions.MaxContextTokens,
        EnableKvCache = true,
        RequestTimeoutMs = serverOptions.RequestTimeoutMs
    };
    
    var engine = SmallMindFactory.Create(engineOptions);
    logger.LogInformation("SmallMind engine initialized successfully");
    
    return engine;
});

var app = builder.Build();

string GetModelName(ServerOptions opts)
{
    return opts.ModelId ?? Path.GetFileName(opts.ModelPath ?? "smallmind");
}

string MapFinishReason(FinishReason reason)
{
    return reason switch
    {
        FinishReason.Completed => "stop",
        FinishReason.StopSequence => "stop",
        FinishReason.Length => "length",
        FinishReason.Cancelled => "cancelled",
        FinishReason.Timeout => "timeout",
        FinishReason.Error => "error",
        _ => "stop"
    };
}

app.MapGet("/healthz", () => Results.Ok(new { status = "healthy" }));

app.MapGet("/readyz", (ISmallMindEngine engine) =>
{
    try
    {
        var caps = engine.GetCapabilities();
        return Results.Ok(new { status = "ready", capabilities = new { streaming = caps.SupportsStreaming, embeddings = caps.SupportsEmbeddings } });
    }
    catch
    {
        return Results.StatusCode(503);
    }
});

app.MapGet("/v1/models", (ServerOptions opts) =>
{
    var modelName = GetModelName(opts);
    var response = new ModelListResponse
    {
        Data = new List<ModelInfo>
        {
            new ModelInfo
            {
                Id = modelName,
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                OwnedBy = "smallmind"
            }
        }
    };
    return Results.Ok(response);
});

app.MapPost("/v1/chat/completions", async (
    [FromBody] ChatCompletionRequest request,
    ISmallMindEngine engine,
    RequestQueue queue,
    ServerMetrics metrics,
    ServerOptions opts,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    using var activity = metrics.StartActivity("chat.completions");
    var sw = Stopwatch.StartNew();
    metrics.IncrementInflight();
    
    try
    {
        using var slot = await queue.EnqueueAsync(cancellationToken);
        
        if (!slot.Success)
        {
            return Results.Json(new ErrorResponse
            {
                Error = new ErrorDetail
                {
                    Message = "Queue full - too many concurrent requests",
                    Type = "server_error",
                    Code = "queue_full"
                }
            }, statusCode: 429);
        }
        
        var prompt = PromptBuilder.BuildPrompt(request.Messages);
        var stopSequences = request.GetStopSequences();
        
        var genOptions = new TextGenerationOptions
        {
            Temperature = request.Temperature ?? opts.DefaultTemperature,
            TopP = request.TopP ?? opts.DefaultTopP,
            TopK = opts.DefaultTopK,
            MaxOutputTokens = request.MaxTokens ?? opts.DefaultMaxTokens,
            StopSequences = stopSequences.Length > 0 ? stopSequences : Array.Empty<string>()
        };
        
        using var session = engine.CreateTextGenerationSession(genOptions);
        
        var genRequest = new TextGenerationRequest
        {
            Prompt = prompt.AsMemory(),
            Seed = request.Seed.HasValue ? (int)request.Seed.Value : null
        };
        
        if (request.Stream)
        {
            return await StreamChatCompletionAsync(session, genRequest, GetModelName(opts), 
                metrics, sw, cancellationToken);
        }
        else
        {
            var result = await Task.Run(() => session.Generate(genRequest, cancellationToken), cancellationToken);
            
            var response = new ChatCompletionResponse
            {
                Id = $"chatcmpl-{Guid.NewGuid():N}",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = GetModelName(opts),
                Choices = new List<ChatChoice>
                {
                    new ChatChoice
                    {
                        Index = 0,
                        Message = new ChatMessage
                        {
                            Role = "assistant",
                            Content = result.Text
                        },
                        FinishReason = MapFinishReason(result.FinishReason)
                    }
                },
                Usage = new UsageInfo
                {
                    PromptTokens = result.Usage.PromptTokens,
                    CompletionTokens = result.Usage.CompletionTokens,
                    TotalTokens = result.Usage.TotalTokens
                }
            };
            
            metrics.RecordRequest("chat.completions", sw.Elapsed.TotalMilliseconds, result.Usage.CompletionTokens, true);
            return Results.Ok(response);
        }
    }
    catch (Exception ex)
    {
        metrics.RecordRequest("chat.completions", sw.Elapsed.TotalMilliseconds, 0, false);
        
        return Results.Json(new ErrorResponse
        {
            Error = new ErrorDetail
            {
                Message = ex.Message,
                Type = "internal_error"
            }
        }, statusCode: 500);
    }
    finally
    {
        metrics.DecrementInflight();
    }
});

app.MapPost("/v1/completions", async (
    [FromBody] CompletionRequest request,
    ISmallMindEngine engine,
    RequestQueue queue,
    ServerMetrics metrics,
    ServerOptions opts,
    CancellationToken cancellationToken) =>
{
    using var activity = metrics.StartActivity("completions");
    var sw = Stopwatch.StartNew();
    metrics.IncrementInflight();
    
    try
    {
        using var slot = await queue.EnqueueAsync(cancellationToken);
        
        if (!slot.Success)
        {
            return Results.Json(new ErrorResponse
            {
                Error = new ErrorDetail
                {
                    Message = "Queue full - too many concurrent requests",
                    Type = "server_error",
                    Code = "queue_full"
                }
            }, statusCode: 429);
        }
        
        var stopSequences = request.GetStopSequences();
        
        var genOptions = new TextGenerationOptions
        {
            Temperature = request.Temperature ?? opts.DefaultTemperature,
            TopP = request.TopP ?? opts.DefaultTopP,
            TopK = opts.DefaultTopK,
            MaxOutputTokens = request.MaxTokens ?? opts.DefaultMaxTokens,
            StopSequences = stopSequences.Length > 0 ? stopSequences : Array.Empty<string>()
        };
        
        using var session = engine.CreateTextGenerationSession(genOptions);
        
        var genRequest = new TextGenerationRequest
        {
            Prompt = request.Prompt.AsMemory(),
            Seed = request.Seed.HasValue ? (int)request.Seed.Value : null
        };
        
        var result = await Task.Run(() => session.Generate(genRequest, cancellationToken), cancellationToken);
        
        var response = new CompletionResponse
        {
            Id = $"cmpl-{Guid.NewGuid():N}",
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Model = GetModelName(opts),
            Choices = new List<CompletionChoice>
            {
                new CompletionChoice
                {
                    Index = 0,
                    Text = result.Text,
                    FinishReason = MapFinishReason(result.FinishReason)
                }
            },
            Usage = new UsageInfo
            {
                PromptTokens = result.Usage.PromptTokens,
                CompletionTokens = result.Usage.CompletionTokens,
                TotalTokens = result.Usage.TotalTokens
            }
        };
        
        metrics.RecordRequest("completions", sw.Elapsed.TotalMilliseconds, result.Usage.CompletionTokens, true);
        return Results.Ok(response);
    }
    catch (Exception ex)
    {
        metrics.RecordRequest("completions", sw.Elapsed.TotalMilliseconds, 0, false);
        
        return Results.Json(new ErrorResponse
        {
            Error = new ErrorDetail
            {
                Message = ex.Message,
                Type = "internal_error"
            }
        }, statusCode: 500);
    }
    finally
    {
        metrics.DecrementInflight();
    }
});

app.MapPost("/v1/embeddings", async (
    [FromBody] SmallMind.Server.Models.EmbeddingRequest request,
    ISmallMindEngine engine,
    RequestQueue queue,
    ServerMetrics metrics,
    ServerOptions opts,
    CancellationToken cancellationToken) =>
{
    using var activity = metrics.StartActivity("embeddings");
    var sw = Stopwatch.StartNew();
    metrics.IncrementInflight();
    
    try
    {
        var caps = engine.GetCapabilities();
        if (!caps.SupportsEmbeddings)
        {
            return Results.Json(new ErrorResponse
            {
                Error = new ErrorDetail
                {
                    Message = "Embeddings not supported by this model",
                    Type = "not_supported_error"
                }
            }, statusCode: 400);
        }
        
        using var slot = await queue.EnqueueAsync(cancellationToken);
        
        if (!slot.Success)
        {
            return Results.Json(new ErrorResponse
            {
                Error = new ErrorDetail
                {
                    Message = "Queue full - too many concurrent requests",
                    Type = "server_error",
                    Code = "queue_full"
                }
            }, statusCode: 429);
        }
        
        var inputs = request.GetInputs();
        var embeddings = new List<EmbeddingData>();
        int totalTokens = 0;
        
        var embOptions = new EmbeddingOptions { Normalize = true };
        using var session = engine.CreateEmbeddingSession(embOptions);
        
        for (int i = 0; i < inputs.Length; i++)
        {
            var embRequest = new EmbeddingRequest
            {
                Input = inputs[i].AsMemory()
            };
            
            var result = await Task.Run(() => session.Embed(embRequest, cancellationToken), cancellationToken);
            
            embeddings.Add(new EmbeddingData
            {
                Index = i,
                Embedding = result.Vector
            });
            
            totalTokens += result.Usage.TotalTokens;
        }
        
        var response = new EmbeddingResponse
        {
            Data = embeddings,
            Model = GetModelName(opts),
            Usage = new UsageInfo
            {
                PromptTokens = totalTokens,
                CompletionTokens = 0,
                TotalTokens = totalTokens
            }
        };
        
        metrics.RecordRequest("embeddings", sw.Elapsed.TotalMilliseconds, 0, true);
        return Results.Ok(response);
    }
    catch (Exception ex)
    {
        metrics.RecordRequest("embeddings", sw.Elapsed.TotalMilliseconds, 0, false);
        
        return Results.Json(new ErrorResponse
        {
            Error = new ErrorDetail
            {
                Message = ex.Message,
                Type = "internal_error"
            }
        }, statusCode: 500);
    }
    finally
    {
        metrics.DecrementInflight();
    }
});

app.Run($"http://{serverOptions.Host}:{serverOptions.Port}");

static async Task<IResult> StreamChatCompletionAsync(
    ITextGenerationSession session,
    TextGenerationRequest request,
    string modelName,
    ServerMetrics metrics,
    Stopwatch sw,
    CancellationToken cancellationToken)
{
    var responseId = $"chatcmpl-{Guid.NewGuid():N}";
    var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    
    return Results.Stream(async (stream) =>
    {
        var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.AutoFlush = true;
        
        try
        {
            bool firstChunk = true;
            int tokensGenerated = 0;
            
            await foreach (var token in session.GenerateStreaming(request, cancellationToken))
            {
                if (firstChunk)
                {
                    var firstChunkData = new ChatCompletionChunk
                    {
                        Id = responseId,
                        Created = created,
                        Model = modelName,
                        Choices = new List<ChatChoiceDelta>
                        {
                            new ChatChoiceDelta
                            {
                                Index = 0,
                                Delta = new ChatMessageDelta
                                {
                                    Role = "assistant",
                                    Content = string.Empty
                                }
                            }
                        }
                    };
                    
                    await writer.WriteAsync("data: ");
                    await writer.WriteAsync(JsonSerializer.Serialize(firstChunkData));
                    await writer.WriteAsync("\n\n");
                    firstChunk = false;
                }
                
                if (!token.IsSpecial)
                {
                    var chunk = new ChatCompletionChunk
                    {
                        Id = responseId,
                        Created = created,
                        Model = modelName,
                        Choices = new List<ChatChoiceDelta>
                        {
                            new ChatChoiceDelta
                            {
                                Index = 0,
                                Delta = new ChatMessageDelta
                                {
                                    Content = token.TokenText
                                }
                            }
                        }
                    };
                    
                    await writer.WriteAsync("data: ");
                    await writer.WriteAsync(JsonSerializer.Serialize(chunk));
                    await writer.WriteAsync("\n\n");
                }
                
                tokensGenerated++;
            }
            
            var finalChunk = new ChatCompletionChunk
            {
                Id = responseId,
                Created = created,
                Model = modelName,
                Choices = new List<ChatChoiceDelta>
                {
                    new ChatChoiceDelta
                    {
                        Index = 0,
                        Delta = new ChatMessageDelta(),
                        FinishReason = "stop"
                    }
                }
            };
            
            await writer.WriteAsync("data: ");
            await writer.WriteAsync(JsonSerializer.Serialize(finalChunk));
            await writer.WriteAsync("\n\n");
            await writer.WriteAsync("data: [DONE]\n\n");
            
            metrics.RecordRequest("chat.completions.stream", sw.Elapsed.TotalMilliseconds, tokensGenerated, true);
        }
        catch (Exception)
        {
            metrics.RecordRequest("chat.completions.stream", sw.Elapsed.TotalMilliseconds, 0, false);
            throw;
        }
        finally
        {
            await writer.DisposeAsync();
        }
    }, contentType: "text/event-stream");
}
