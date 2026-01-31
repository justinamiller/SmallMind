# Troubleshooting Guide

Common issues and solutions for SmallMind.

## Table of Contents

- [Training Issues](#training-issues)
- [Inference Issues](#inference-issues)
- [Checkpoint Issues](#checkpoint-issues)
- [Performance Issues](#performance-issues)
- [Memory Issues](#memory-issues)
- [Configuration Issues](#configuration-issues)

## Training Issues

### Loss is NaN or Infinity

**Symptoms**:
```
[WRN] Numerical instability detected: Loss is NaN or Infinity at step 123
```

**Common Causes**:
1. Learning rate too high
2. Gradient explosion
3. Invalid input data

**Solutions**:

1. **Reduce learning rate**:
   ```csharp
   training.Train(steps: 1000, learningRate: 0.0001); // Try 10x smaller
   ```

2. **Enable gradient clipping** (if available):
   ```csharp
   var config = new TrainingConfig
   {
       EnableDiagnostics = true  // Logs gradient health
   };
   ```

3. **Check input data**:
   ```csharp
   // Ensure data contains no NaN/Inf
   foreach (var value in data)
   {
       if (float.IsNaN(value) || float.IsInfinity(value))
       {
           throw new Exception("Invalid data");
       }
   }
   ```

4. **Use Xavier initialization** (already default):
   ```csharp
   // Verify model is initialized correctly
   var model = new TransformerModel(..., seed: 42);
   ```

### Training is Very Slow

**Symptoms**: Training throughput < 100 tokens/sec

**Common Causes**:
1. Debug build instead of Release
2. Small batch size
3. Large model on limited hardware

**Solutions**:

1. **Use Release build**:
   ```bash
   dotnet build -c Release
   dotnet run -c Release
   ```
   Release builds are 5-10x faster than Debug.

2. **Increase batch size**:
   ```csharp
   var training = new Training(
       model, tokenizer, data,
       blockSize: 128,
       batchSize: 32  // Increase if memory allows
   );
   ```

3. **Reduce model size** for experiments:
   ```csharp
   var model = new TransformerModel(
       vocabSize: 256,
       blockSize: 64,   // Smaller context
       nEmbd: 32,       // Smaller embeddings
       nLayer: 2,       // Fewer layers
       nHead: 2
   );
   ```

4. **Disable diagnostics**:
   ```csharp
   var config = new TrainingConfig
   {
       EnableDiagnostics = false  // Faster training
   };
   ```

### Training Doesn't Converge

**Symptoms**: Loss doesn't decrease over many steps

**Common Causes**:
1. Learning rate too low
2. Model too small for task
3. Insufficient training data

**Solutions**:

1. **Increase learning rate**:
   ```csharp
   training.Train(steps: 1000, learningRate: 0.001); // Try 10x larger
   ```

2. **Use a larger model**:
   ```csharp
   var model = new TransformerModel(
       vocabSize: 256,
       blockSize: 128,  // Larger context
       nEmbd: 64,       // More parameters
       nLayer: 4
   );
   ```

3. **Add more training data**: Aim for at least 10K tokens for meaningful results.

4. **Train for more steps**: Small models may need 10K+ steps.

## Inference Issues

### Generation is Slow

**Symptoms**: Generation takes > 1 second per token

**Common Causes**:
1. Debug build
2. Large model
3. No SIMD acceleration

**Solutions**:

1. **Use Release build** (see above)

2. **Verify SIMD is enabled**:
   ```csharp
   using SmallMind.Simd;
   
   var caps = SimdCapabilities.Detect();
   Console.WriteLine($"SIMD: {caps.InstructionSet}");
   Console.WriteLine($"Vector size: {caps.VectorSize}");
   ```
   Expected output: "AVX2", "AVX", or "SSE" (not "None")

3. **Reduce temperature** for faster sampling:
   ```csharp
   var output = sampling.Generate(prompt, maxTokens: 100, temperature: 0.1);
   // Lower temperature = more greedy = faster
   ```

### Generated Text is Gibberish

**Symptoms**: Output doesn't make sense

**Common Causes**:
1. Model not trained enough
2. Temperature too high
3. Wrong checkpoint loaded

**Solutions**:

1. **Train longer**:
   ```csharp
   training.Train(steps: 10000, ...);  // More steps
   ```

2. **Reduce temperature**:
   ```csharp
   var output = sampling.Generate(prompt, maxTokens: 100, temperature: 0.5);
   // Lower = more deterministic
   ```

3. **Verify checkpoint**:
   ```csharp
   training.LoadCheckpoint("path/to/checkpoint.json");
   // Check that file exists and is correct version
   ```

### Generation Produces Same Output Every Time

**Symptoms**: Multiple generations with same prompt produce identical output

**Common Causes**:
1. Same seed used
2. Temperature = 0.0

**Solutions**:

1. **Use different seeds** or omit seed:
   ```csharp
   var sampling = new Sampling(model, tokenizer, blockSize);
   var output1 = sampling.Generate(prompt, maxTokens: 100, seed: 1);
   var output2 = sampling.Generate(prompt, maxTokens: 100, seed: 2);
   // Or omit seed for random behavior
   var output3 = sampling.Generate(prompt, maxTokens: 100);
   ```

2. **Increase temperature**:
   ```csharp
   var output = sampling.Generate(prompt, maxTokens: 100, temperature: 0.8);
   // Higher = more randomness
   ```

## Checkpoint Issues

### Cannot Load Checkpoint

**Symptoms**:
```
CheckpointException: Failed to load checkpoint from path/to/checkpoint.json
```

**Common Causes**:
1. File doesn't exist
2. Corrupted JSON
3. Version mismatch

**Solutions**:

1. **Verify file exists**:
   ```csharp
   if (!File.Exists(checkpointPath))
   {
       throw new FileNotFoundException("Checkpoint not found", checkpointPath);
   }
   ```

2. **Check JSON validity**:
   ```bash
   # Use jq or similar to validate
   cat checkpoint.json | jq .
   ```

3. **Check file permissions**:
   ```bash
   ls -la checkpoint.json
   chmod 644 checkpoint.json
   ```

4. **Try re-saving**:
   ```csharp
   // Save from a working model
   training.SaveCheckpoint("new_checkpoint.json");
   ```

### Checkpoint File is Huge

**Symptoms**: Checkpoint files are hundreds of MB

**Common Causes**:
1. Large model (expected)
2. Inefficient serialization

**Solutions**:

1. **This is normal for large models**: A model with 10M parameters will be ~40MB (float32).

2. **Compress checkpoints**:
   ```bash
   gzip checkpoint.json
   # Reduces size by ~80%
   ```

3. **Use smaller models** if disk space is limited.

## Performance Issues

### High Memory Usage

**Symptoms**: Process uses > 2GB RAM

**Common Causes**:
1. Large model
2. Memory leaks
3. Large batch size

**Solutions**:

1. **Monitor memory**:
   ```csharp
   Console.WriteLine($"Memory: {GC.GetTotalMemory(false) / 1024 / 1024}MB");
   Console.WriteLine($"GC: Gen0={GC.CollectionCount(0)}, Gen1={GC.CollectionCount(1)}, Gen2={GC.CollectionCount(2)}");
   ```

2. **Reduce batch size**:
   ```csharp
   var training = new Training(model, tokenizer, data,
       blockSize: 128,
       batchSize: 8  // Smaller batch
   );
   ```

3. **Check for leaks**:
   ```csharp
   // Ensure all IDisposable objects are disposed
   using var pool = new MemoryPool();
   using var index = new VectorIndex();
   ```

4. **Force GC** between training sessions:
   ```csharp
   training.Train(steps: 1000, ...);
   GC.Collect();
   GC.WaitForPendingFinalizers();
   GC.Collect();
   ```

### CPU Usage is Low

**Symptoms**: CPU usage < 50% during training

**Common Causes**:
1. Small model (doesn't saturate CPU)
2. I/O bound (checkpoint saving)
3. Thread pool starvation

**Solutions**:

1. **This is often normal** for small models.

2. **Increase model size**:
   ```csharp
   var model = new TransformerModel(
       vocabSize: 256,
       blockSize: 256,
       nEmbd: 128,
       nLayer: 6,
       nHead: 8
   );
   ```

3. **Configure thread pool**:
   ```csharp
   ThreadPool.SetMinThreads(
       Environment.ProcessorCount,
       Environment.ProcessorCount
   );
   ```

## Memory Issues

### OutOfMemoryException

**Symptoms**:
```
System.OutOfMemoryException: Insufficient memory to continue the execution of the program.
```

**Solutions**:

1. **Reduce model size** (see above)

2. **Reduce batch size** (see above)

3. **Use gradient checkpointing** (experimental):
   ```csharp
   var config = new TrainingConfig
   {
       UseGradientCheckpointing = true,
       CheckpointStrategy = CheckpointStrategy.EveryLayer
   };
   ```

4. **Increase system memory** or use a machine with more RAM.

## Configuration Issues

### DI Registration Fails

**Symptoms**:
```
InvalidOperationException: Unable to resolve service for type 'TransformerModel'
```

**Solutions**:

1. **Ensure services are registered**:
   ```csharp
   builder.Services.AddSmallMind(options =>
   {
       options.ModelPath = "model.json";
       // ... other options
   });
   ```

2. **Check service lifetime**:
   ```csharp
   // Model should be Singleton
   services.AddSingleton<TransformerModel>(sp => LoadModel());
   
   // Sampling should be Scoped or Transient
   services.AddScoped<Sampling>();
   ```

3. **Verify all dependencies** are registered.

### Configuration Not Loaded

**Symptoms**: Default values used instead of configured values

**Solutions**:

1. **Check appsettings.json**:
   ```json
   {
     "SmallMind": {
       "ModelPath": "./model.json"
     }
   }
   ```

2. **Ensure configuration is bound**:
   ```csharp
   builder.Services.Configure<SmallMindOptions>(
       builder.Configuration.GetSection("SmallMind"));
   ```

3. **Verify file is copied** to output directory:
   ```xml
   <ItemGroup>
     <Content Include="appsettings.json">
       <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
     </Content>
   </ItemGroup>
   ```

## Getting Help

If you're still stuck:

1. **Check logs** for detailed error messages
2. **Enable diagnostics**:
   ```csharp
   var config = new TrainingConfig { EnableDiagnostics = true };
   ```
3. **Search GitHub Issues**: [github.com/justinamiller/SmallMind/issues](https://github.com/justinamiller/SmallMind/issues)
4. **Ask in Discussions**: [github.com/justinamiller/SmallMind/discussions](https://github.com/justinamiller/SmallMind/discussions)
5. **Include minimal reproduction**: Code snippet + error message + environment details

## See Also

- [Configuration Guide](configuration.md)
- [Threading and Disposal Guide](threading-and-disposal.md)
- [Observability Guide](observability.md)
