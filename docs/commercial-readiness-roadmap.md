# SmallMind Commercial Readiness Roadmap

## Vision Statement

Transform SmallMind from an educational "toy LLM" demonstration into a **production-ready internal library** that teams can confidently integrate into commercial products. This library will provide a stable, well-documented, performant API for CPU-based language model training and inference while maintaining its value as a learning tool. The library will support deterministic execution, streaming generation, cancellable operations, and reliable checkpointing—all built exclusively with core .NET libraries to ensure zero external dependencies and maximum portability.

---

## Milestone v0.2: Library & Packaging Foundations

**Goal:** Establish clean architecture, stable APIs, and efficient serialization for commercial consumption.

### Epic 2.1: Multi-Project Solution Architecture

**User Stories:**
- As a **library consumer**, I want to reference only the packages I need (Core, Transformers, Tokenizers, Runtime) so that I minimize dependencies in my application.
- As a **contributor**, I want a clear separation of concerns between tensor operations, model definitions, tokenization, and runtime generation so that the codebase is maintainable and testable.

**Acceptance Criteria:**
- ✅ Solution contains 5 projects:
  - `SmallMind.Core`: Tensor, autograd, matrix operations, SIMD kernels, optimizer
  - `SmallMind.Transformers`: TransformerModel, attention mechanisms, layer definitions
  - `SmallMind.Tokenizers`: ITokenizer, CharTokenizer, BpeTokenizer
  - `SmallMind.Runtime`: Text generation, sampling strategies, conversation sessions
  - `SmallMind.Cli`: Command-line interface consuming public APIs
- ✅ No circular dependencies between projects
- ✅ Clean namespace structure (SmallMind.Core.*, SmallMind.Transformers.*, etc.)
- ✅ All code files moved to appropriate projects
- ✅ Solution builds successfully with `dotnet build`

**Implementation Notes:**
- Remove Microsoft.Extensions.* NuGet packages; replace with manual implementations where necessary (e.g., simple console logging instead of ILogger)
- Move files systematically:
  - Core: Tensor.cs, MatrixOps.cs, Optimizer.cs, MemoryPool.cs, Simd/*, Training.cs (core training loop)
  - Transformers: Transformer.cs, NeuralNet.cs (attention, FFN, LayerNorm)
  - Tokenizers: Text/* (ITokenizer, CharTokenizer, BpeTokenizer, TokenizerFactory)
  - Runtime: Sampling.cs, ConversationSession.cs, text generation logic
  - Cli: Program.cs, CLI-specific logic
- Update project references (Core ← Transformers ← Runtime ← Cli; Tokenizers ← Runtime)
- Use internal visibility for implementation details; expose only necessary types

---

### Epic 2.2: Stable Public API Surface

**User Stories:**
- As a **library consumer**, I want well-defined interfaces for tokenization, generation, and checkpointing so that I can write testable, decoupled code.
- As a **maintainer**, I want a minimal public API surface so that I can evolve internals without breaking consumers.

**Acceptance Criteria:**
- ✅ Public interfaces defined:
  ```csharp
  namespace SmallMind.Tokenizers
  {
      public interface ITokenizer
      {
          List<int> Encode(string text);
          List<int> Encode(ReadOnlySpan<char> text);
          string Decode(IEnumerable<int> tokens);
          string Decode(ReadOnlySpan<int> tokens);
          int VocabSize { get; }
      }
  }

  namespace SmallMind.Runtime
  {
      public interface ITextGenerator
      {
          Task<string> GenerateAsync(
              string prompt,
              GenerationOptions options,
              CancellationToken cancellationToken = default);
          
          IAsyncEnumerable<int> GenerateStreamAsync(
              string prompt,
              GenerationOptions options,
              CancellationToken cancellationToken = default);
      }
      
      public class GenerationOptions
      {
          public int MaxTokens { get; set; } = 100;
          public float Temperature { get; set; } = 1.0f;
          public int TopK { get; set; } = 0;
          public int? Seed { get; set; }
      }
  }

  namespace SmallMind.Core
  {
      public interface ICheckpointStore
      {
          Task SaveAsync(ModelCheckpoint checkpoint, string path, CancellationToken cancellationToken = default);
          Task<ModelCheckpoint> LoadAsync(string path, CancellationToken cancellationToken = default);
      }
      
      public class ModelCheckpoint
      {
          public int FormatVersion { get; set; }
          public ModelMetadata Metadata { get; set; }
          public List<TensorData> Parameters { get; set; }
      }
  }
  ```
- ✅ CLI exclusively uses public APIs (no `internal` access)
- ✅ XML documentation on all public types and members
- ✅ Clear versioning attributes on public types

**Implementation Notes:**
- Mark implementation classes as `internal` where appropriate
- Use `InternalsVisibleTo` for test assemblies only
- Consider factory patterns for complex object creation (e.g., TransformerModelBuilder)
- Ensure ITokenizer interface is already present in Text/ITokenizer.cs; verify it's public

---

### Epic 2.3: Binary Checkpoint Format

**User Stories:**
- As a **model trainer**, I want fast, compact checkpoint saves so that I can iterate quickly during development.
- As a **production engineer**, I want reliable, version-aware checkpoint loading so that model deployments don't break due to format changes.

**Acceptance Criteria:**
- ✅ Binary checkpoint format specification:
  ```
  [Header]
  Magic: "SMND" (4 bytes)
  Version: uint32 (4 bytes) - format version
  Reserved: 8 bytes (for future use)
  
  [Metadata]
  MetadataLength: uint32 (4 bytes)
  MetadataJson: UTF-8 JSON string
    {
      "modelType": "TransformerModel",
      "vocabSize": 256,
      "blockSize": 128,
      "embedDim": 384,
      "numHeads": 6,
      "numLayers": 6,
      "ffnHiddenDim": 1536
    }
  
  [Parameters]
  NumParameters: uint32 (4 bytes)
  For each parameter:
    ShapeRank: uint32 (4 bytes)
    Shape: int32[ShapeRank] (4*ShapeRank bytes)
    DataLength: uint32 (4 bytes)
    Data: float[DataLength] (4*DataLength bytes, little-endian)
  ```
- ✅ `BinaryCheckpointStore` implementation:
  - SaveAsync: Writes binary format with validation
  - LoadAsync: Reads with version checking and clear error messages
  - Forward compatibility: Ignores reserved/unknown fields
- ✅ Performance:
  - Binary save/load ≥5x faster than JSON for typical model (~1MB checkpoints)
  - File size ≤50% of JSON equivalent
- ✅ Legacy support:
  - `JsonCheckpointStore` can still load old `.json` checkpoints (read-only)
  - OR: CLI tool `smallmind convert-checkpoint` to migrate JSON → binary
- ✅ Tests:
  - Round-trip: save binary → load → verify all tensors match
  - Version validation: loading newer format version fails gracefully
  - Corruption detection: invalid magic/checksum fails early

**Implementation Notes:**
- Use FileStream, BinaryWriter/BinaryReader
- Use Span<T> / Memory<T> for efficient writes
- Consider adding CRC32 checksum after metadata and after each tensor
- Store metadata as JSON for debuggability (humans can read metadata)
- Use little-endian throughout (BitConverter.IsLittleEndian check)
- Async I/O with cancellation token support

---

### Epic 2.4: NuGet Packaging & Documentation

**User Stories:**
- As a **developer**, I want to install SmallMind from NuGet with `dotnet add package SmallMind.Core` so that I can start using it immediately.
- As a **new user**, I want a 5-line code sample in the README so that I understand how to load a model and generate text.

**Acceptance Criteria:**
- ✅ NuGet metadata in all `.csproj` files:
  ```xml
  <PackageId>SmallMind.Core</PackageId>
  <Version>0.2.0</Version>
  <Authors>Justin Miller</Authors>
  <Description>Core tensor and autograd library for SmallMind LLM</Description>
  <PackageTags>llm;language-model;machine-learning;csharp</PackageTags>
  <PackageLicenseExpression>MIT</PackageLicenseExpression>
  <PackageProjectUrl>https://github.com/justinamiller/SmallMind</PackageProjectUrl>
  <RepositoryUrl>https://github.com/justinamiller/SmallMind</RepositoryUrl>
  <IsPackable>true</IsPackable>
  ```
- ✅ Versions synchronized across all packages (0.2.0)
- ✅ CHANGELOG.md updated with v0.2.0 release notes
- ✅ README.md updated:
  - Installation section (NuGet packages)
  - Minimal code sample:
    ```csharp
    using SmallMind.Core;
    using SmallMind.Runtime;
    using SmallMind.Tokenizers;

    // Load checkpoint
    var store = new BinaryCheckpointStore();
    var checkpoint = await store.LoadAsync("model.smnd");
    var model = TransformerModel.FromCheckpoint(checkpoint);

    // Generate text
    var tokenizer = new CharTokenizer();
    var generator = new TextGenerator(model, tokenizer);
    var result = await generator.GenerateAsync("Hello", new GenerationOptions { MaxTokens = 50 });
    Console.WriteLine(result);
    ```
- ✅ No external NuGet dependencies in any package (only System.*, Microsoft.NETCore.App references)

**Implementation Notes:**
- Verify `dotnet pack` produces valid `.nupkg` files
- Test installation from local NuGet source
- Update SmallMind.Cli.csproj to reference packages instead of project references (for packaging verification)

---

### Epic 2.5: Example Application

**User Stories:**
- As a **library consumer**, I want a minimal working example that shows cancellation token usage so that I can integrate generation into my app safely.

**Acceptance Criteria:**
- ✅ Project created: `/examples/MinimalGenerate/MinimalGenerate.csproj`
- ✅ Demonstrates:
  - Loading a binary checkpoint
  - Generating text with custom options
  - Using CancellationToken (e.g., timeout or Ctrl+C)
- ✅ Builds and runs independently with `dotnet run --project examples/MinimalGenerate`
- ✅ Includes README.md explaining the example

**Implementation Notes:**
- Keep example < 100 lines
- Use simple console I/O
- Handle cancellation gracefully with try/catch

---

## Milestone v0.3: Runtime & Inference Ergonomics

**Goal:** Deliver production-quality inference features with streaming, determinism, and resource control.

### Epic 3.1: Streaming Text Generation

**User Stories:**
- As a **frontend developer**, I want to stream tokens incrementally to the UI so that users see real-time generation progress.

**Acceptance Criteria:**
- ✅ `ITextGenerator.GenerateStreamAsync` returns `IAsyncEnumerable<int>` (tokens)
- ✅ Consumers can decode tokens on-the-fly with ITokenizer
- ✅ Example demonstrating streaming to console

**Implementation Notes:**
- Use `yield return` in async enumerator
- Ensure cancellation token is respected during streaming

---

### Epic 3.2: Deterministic Inference

**User Stories:**
- As a **QA engineer**, I want to reproduce exact generation results using a seed so that I can write regression tests.

**Acceptance Criteria:**
- ✅ `GenerationOptions.Seed` produces identical outputs across runs
- ✅ Unit test validates determinism (same seed → same output)

**Implementation Notes:**
- Seed Random properly in sampling logic
- Document seed behavior in XML comments

---

### Epic 3.3: Resource Limits & Monitoring

**User Stories:**
- As a **production engineer**, I want to set max memory and timeout limits so that runaway generation doesn't crash the service.

**Acceptance Criteria:**
- ✅ `GenerationOptions.MaxDurationMs` enforces timeout
- ✅ Memory pooling for tensors (MemoryPool) to limit allocations
- ✅ Generation throws OperationCanceledException on timeout

**Implementation Notes:**
- Use CancellationTokenSource.CancelAfter for timeout
- Monitor GC memory with GC.GetTotalMemory if needed

---

### Epic 3.4: KV-Cache for Inference

**User Stories:**
- As a **performance engineer**, I want to avoid recomputing keys/values for already-processed tokens so that multi-turn conversations are faster.

**Acceptance Criteria:**
- ✅ KVCache class stores key/value tensors per layer
- ✅ Inference reuses cached KV when generating next token
- ✅ 5-10x speedup on multi-turn generation (100+ tokens)

**Implementation Notes:**
- Cache at attention layer level
- Clear cache on new prompt
- Support max cache length (e.g., 2048 tokens)

---

## Milestone v0.4: Operability & Evaluation

**Goal:** Enable confident production deployment with metrics, evaluation, and operational tooling.

### Epic 4.1: Model Evaluation Metrics

**User Stories:**
- As a **ML engineer**, I want to compute perplexity on a validation set so that I can track model quality over training.

**Acceptance Criteria:**
- ✅ `ModelEvaluator` class computes perplexity, cross-entropy
- ✅ CLI command `smallmind evaluate --checkpoint model.smnd --data validation.txt`
- ✅ Outputs metrics to console (perplexity, loss)

**Implementation Notes:**
- Compute loss on validation set without gradients
- Support batching for efficiency

---

### Epic 4.2: Generation Metrics & Telemetry

**User Stories:**
- As a **production engineer**, I want to log token throughput, latency, and memory usage so that I can monitor service health.

**Acceptance Criteria:**
- ✅ `GenerationMetrics` class tracks:
  - Tokens per second
  - Time to first token (TTFT)
  - End-to-end latency
  - Peak memory usage
- ✅ Metrics accessible via `ITextGenerator.GetMetrics()`

**Implementation Notes:**
- Use Stopwatch for timing
- Log to structured output (e.g., JSON lines)

---

### Epic 4.3: Health Checks

**User Stories:**
- As a **DevOps engineer**, I want a `/health` endpoint or CLI command so that I can verify the model is loaded and ready.

**Acceptance Criteria:**
- ✅ `IModelHealthCheck` interface with `CheckHealthAsync()`
- ✅ Validates model is loaded, parameters non-null
- ✅ CLI command `smallmind health --checkpoint model.smnd`

**Implementation Notes:**
- Quick sanity checks (e.g., run single forward pass)
- Return structured health status (healthy/degraded/unhealthy)

---

### Epic 4.4: Checkpoint Versioning & Migration

**User Stories:**
- As a **maintainer**, I want a clear policy for checkpoint compatibility so that users know when checkpoints need migration.

**Acceptance Criteria:**
- ✅ Document checkpoint version policy:
  - v0.2.x: Format version 1 (binary)
  - v0.3.x: Format version 2 (adds optimizer state)
  - Forward compatibility: newer readers can load older checkpoints
  - CLI migration tool: `smallmind migrate-checkpoint --input old.json --output new.smnd`
- ✅ LoadAsync detects version and adapts (or fails with clear error)

**Implementation Notes:**
- Use version field in checkpoint header
- Maintain version changelog in docs

---

## Semantic Versioning Policy

**Version Format:** `MAJOR.MINOR.PATCH` (e.g., 0.2.0)

- **MAJOR (0 → 1):** Breaking changes to public API (e.g., interface signature changes, removal of public types)
  - While in 0.x: Breaking changes allowed between minor versions with clear migration guide
  - After 1.0: Breaking changes require major version bump
- **MINOR (0.2 → 0.3):** New features, non-breaking API additions (e.g., new interface methods with default implementations)
  - Checkpoint format may change (versioned)
  - Must provide migration path
- **PATCH (0.2.0 → 0.2.1):** Bug fixes, performance improvements, documentation updates
  - No API changes
  - Checkpoint format unchanged within MINOR version

**Pre-1.0 Stability:**
- v0.2.x: Library foundations (this milestone)
- v0.3.x: Runtime enhancements
- v0.4.x: Operability features
- v1.0.0: Stable public API, full backward compatibility guarantees

**Package Versioning:**
- All SmallMind.* packages share the same version number
- Release all packages together as a suite (even if unchanged)

---

## Checkpoint Backward Compatibility Policy

**Goals:**
1. Users should not lose trained models due to format changes
2. Newer library versions should load older checkpoints
3. Clear errors when checkpoint is too new or corrupted

**Compatibility Matrix:**

| Library Version | Checkpoint Format | Read v1 | Read v2 | Write Format |
|-----------------|-------------------|---------|---------|--------------|
| v0.2.x          | v1 (binary)       | ✅       | ❌       | v1           |
| v0.3.x          | v2 (with opt state)| ✅       | ✅       | v2           |
| v0.4.x          | v2                | ✅       | ✅       | v2           |

**Loading Behavior:**
- If checkpoint version > library version: Throw `CheckpointVersionException` with upgrade message
- If checkpoint version < library version: Load with migration (e.g., initialize missing fields)
- If checkpoint corrupted (bad magic, checksum): Throw `CheckpointCorruptedException`

**Migration Tools:**
- `smallmind convert-checkpoint`: Convert JSON (legacy) → binary v1
- `smallmind upgrade-checkpoint`: Upgrade v1 → v2 (when v2 released)

**Deprecation Policy:**
- Checkpoint format support maintained for 2 major versions
- Example: v3.0 can still read v1 and v2; v4.0 drops v1 support
- 6-month deprecation notice before dropping format support

---

## Success Metrics for v0.2

- ✅ Clean `dotnet build` from fresh clone (no external dependencies)
- ✅ All existing tests pass (394+ tests)
- ✅ CLI remains functional (train, generate, chat commands)
- ✅ Binary checkpoint save/load 5x faster than JSON
- ✅ Binary checkpoint file size ≤50% of JSON
- ✅ Public API surface documented (100% XML doc coverage on public types)
- ✅ Example application runs successfully
- ✅ CI pipeline passes on Windows and Linux

---

## Risks & Mitigations

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Breaking existing checkpoints during migration | Medium | High | Retain JsonCheckpointStore for legacy support; add conversion tool |
| Performance regression from removing Microsoft.Extensions.Logging | Low | Medium | Use simple console logging; add performance tests |
| API surface too large / hard to stabilize | Medium | Medium | Start with minimal interfaces; expand in v0.3/v0.4 based on feedback |
| Multi-project structure complicates build | Low | Low | Ensure CI tests full build; document build process |

---

## Timeline Estimate

**v0.2 Milestone:** 
- Epic 2.1 (Architecture): 1-2 days
- Epic 2.2 (Public API): 1 day
- Epic 2.3 (Binary Checkpoints): 2-3 days
- Epic 2.4 (Packaging & Docs): 1 day
- Epic 2.5 (Example): 0.5 day
- **Total: 5.5-7.5 days**

**v0.3 Milestone:** 3-4 days  
**v0.4 Milestone:** 2-3 days  
**Total to v0.4:** ~2-3 weeks

---

## Appendix: Implementation Checklist for v0.2

### Phase 1: Restructure
- [ ] Create project directories
- [ ] Create .csproj files for Core, Transformers, Tokenizers, Runtime
- [ ] Move source files to projects
- [ ] Update namespaces
- [ ] Update project references
- [ ] Remove Microsoft.Extensions.* packages
- [ ] Verify build

### Phase 2: API Design
- [ ] Define ITextGenerator interface
- [ ] Define ICheckpointStore interface
- [ ] Define GenerationOptions class
- [ ] Make ITokenizer public
- [ ] Add XML docs to interfaces
- [ ] Mark internal classes

### Phase 3: Binary Checkpoints
- [ ] Design binary format spec
- [ ] Implement BinaryCheckpointStore.SaveAsync
- [ ] Implement BinaryCheckpointStore.LoadAsync
- [ ] Add version validation
- [ ] Test round-trip
- [ ] Benchmark vs JSON
- [ ] Update Training class to use ICheckpointStore

### Phase 4: Packaging
- [ ] Add NuGet metadata to all projects
- [ ] Update CHANGELOG.md
- [ ] Update README.md
- [ ] Create examples/MinimalGenerate
- [ ] Test `dotnet pack`
- [ ] Test local NuGet install

### Phase 5: Testing & CI
- [ ] Add binary checkpoint tests
- [ ] Add tokenizer round-trip tests
- [ ] Run full test suite
- [ ] Verify CI on Windows
- [ ] Verify CI on Linux

---

**Next Steps:** Begin Phase 1 (Restructure) starting with project creation and file migration.
