# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.4.0] - 2026-02-12 - Pre-Production Release

### Changed
- **Repository Organization**: Complete restructuring for production readiness
  - Moved all documentation files to `docs/` folder
  - Consolidated all example projects into `examples/` folder (merged `samples/` and `benchmarks/`)
  - Moved build and utility scripts to `scripts/` folder
  - Root directory now contains only: README.md, LICENSE, CHANGELOG.md, and essential build files
  - Solution file properly organized with src, tests, tools, and examples folders

### Removed
- **DELETE_ME folder**: Removed obsolete build artifacts and old examples
- **Duplicate solution file**: Removed SmallMind.slnx, keeping only SmallMind.sln

### Fixed
- **ByteSizeFormatter visibility**: Changed from public to internal (implementation detail)
- **Solution organization**: Added missing tests and tools folder hierarchies
- **API boundary tests**: All tests passing with proper public API surface

### Documentation
- **README.md**: Complete rewrite with professional technical writing style
  - Clear quickstart section with installation and basic usage
  - Architecture overview with component diagram
  - Comprehensive supported models section with size limits and quantization table
  - Simplified structure focused on getting users productive quickly
- **Old README**: Preserved as `docs/README.old.md` for reference

## [Unreleased - Level 3.0 Enhancements]

### Added - Level 3.0 Enhancements

#### GGUF Loading Improvements
- **QKV Double-Read Fix**: Q/K/V tensors in GGUF models are now read exactly once during merging, eliminating redundant dequantization
  - Added tensor read counters and logging for transparency
  - Moved PENDING_/MERGE_QKV check before ReadAndDequantizeTensor for efficiency
  - For SmolLM2-135M: Q/K/V tensors read only during merge, not in main loop

#### GGUF Validation Command
- **run-gguf Command**: End-to-end GGUF model validation with coherence checking
  - Loads GGUF models directly using GgufModelLoader.LoadFromGguf
  - Creates InferenceSession and generates text
  - Reports load time, generation time, and tokens/sec metrics
  - Minimal coherence check validates English text output
  - Exit codes: 0 (coherent output), 1 (error/usage), 2 (garbage output)
  - Example: `dotnet run --project src/SmallMind.Console -- run-gguf model.gguf "prompt"`

#### Sampling Enhancements
- **Top-P (Nucleus) Sampling**: Added nucleus sampling support to legacy Sampling class
  - New `topP` parameter in Generate method (default: 1.0)
  - ApplyTopP method implements efficient nucleus sampling with probability filtering
  - Uses ArrayPool to minimize allocations in hot path
  - Re-normalizes filtered probability distribution for correctness

### Deprecated
- **Sampling Class**: Marked as obsolete in favor of InferenceSession
  - Obsolete warning directs users to InferenceSession for TopP, MinP, repetition penalties, and async streaming
  - Will be removed in v1.0

### Changed
- **GgufModelLoader.LoadWeights**: Optimized to avoid reading Q/K/V tensors twice
- **GgufModelLoader.MergeQKVWeights**: Now returns count of Q/K/V tensor reads for metrics

## [0.3.0] - 2026-02-01

### Added - Advanced Training & Usability Enhancements

#### Learning Rate Schedulers
- **ILearningRateScheduler**: Interface for learning rate scheduling strategies
- **ConstantLR**: No scheduling (baseline)
- **WarmupLR**: Linear warmup from 0 to target learning rate
- **CosineAnnealingLR**: Cosine decay with optional warmup (recommended for most cases)
- **StepDecayLR**: Decay learning rate by factor at regular intervals
- **ExponentialDecayLR**: Smooth exponential decay
- **OneCycleLR**: Triangular policy for fast convergence

#### Gradient Clipping
- **AdamW.gradClipValue**: Clip gradients by value to prevent exploding gradients
- **AdamW.ClipGradientsByNorm()**: Clip by global L2 norm (recommended for transformers)
- Integrated into optimizer step for automatic application

#### Model Builder Pattern
- **TransformerModelBuilder**: Fluent API for creating models
  - `WithVocabSize()`, `WithBlockSize()`, `WithEmbedDim()`, etc.
  - Preset configurations: `UseTinyConfig()`, `UseSmallConfig()`, `UseMediumConfig()`, `UseLargeConfig()`
  - Automatic validation of embedding dimension divisibility by number of heads
  - Example: `TransformerModelBuilder.Create().UseSmallConfig(vocabSize).Build()`

#### Documentation & Tutorials
- **Tutorial 1**: Loading Models and Generating Text
  - Binary vs JSON checkpoints
  - Character and BPE tokenization
  - Greedy, temperature, and top-K sampling
  - Async and streaming generation
- **Tutorial 2**: Concurrent Inference
  - Thread-safe model sharing patterns
  - Parallel batch processing
  - Performance benchmarking
  - Memory management and pooling
- **Tutorial 5**: Advanced Training
  - Learning rate schedule usage
  - Gradient clipping techniques
  - Training with validation
  - Optimizer configuration strategies
- **Tutorials README**: Quick start guide and overview

#### Sample Applications
- **MultiThreadedGeneration**: Production-quality concurrent inference demo
  - Scenario 1: Basic concurrent generation
  - Scenario 2: Batch processing with progress tracking
  - Scenario 3: Performance benchmark across concurrency levels
  - BatchProcessor class for parallel text generation
  - ProgressBar class for console feedback
  - Thread-safe shared model usage

#### NuGet Packaging
- Version synchronized to 0.3.0 across all packages
- Successfully tested `dotnet pack` for all libraries:
  - SmallMind.Core (62KB)
  - SmallMind.Transformers (16KB)
  - SmallMind.Tokenizers (11KB)
  - SmallMind.Runtime (19KB)

### Changed
- **AdamW optimizer**: Constructor now accepts `gradClipValue` parameter (default: 0.0, disabled)
- **AdamW**: Added public `ClipGradientsByNorm()` method for manual gradient clipping
- All library projects updated to version 0.3.0

### Improved
- **Developer Experience**: Builder pattern makes model creation more intuitive
- **Training Stability**: Gradient clipping prevents training divergence
- **Training Quality**: Learning rate schedules improve convergence
- **Documentation**: Comprehensive tutorials cover common use cases
- **Examples**: Multi-threaded sample demonstrates production patterns

## [0.2.0] - 2026-02-01

### Added - Commercial Readiness v0.2

#### Multi-Project Architecture
- **SmallMind.Core**: Core tensor operations, automatic differentiation, SIMD kernels, validation, and exceptions
- **SmallMind.Transformers**: GPT-style Transformer model with attention mechanisms and neural network layers
- **SmallMind.Tokenizers**: Text tokenization interfaces (ITokenizer, CharTokenizer, BpeTokenizer)
- **SmallMind.Runtime**: Text generation, sampling strategies, training orchestration, and conversation sessions
- Clean separation of concerns with no circular dependencies
- Proper namespacing (SmallMind.Core, SmallMind.Transformers, etc.)

#### Binary Checkpoint Format
- **BinaryCheckpointStore**: High-performance binary serialization for model checkpoints
  - Magic header "SMND" with format versioning
  - JSON metadata for debuggability + binary tensor data for performance
  - Little-endian float storage
  - Async I/O with CancellationToken support
  - Forward compatibility with version validation
  - 40-60% smaller file size vs JSON
  - 5-10x faster save/load vs JSON
- **JsonCheckpointStore**: Legacy JSON checkpoint support for backward compatibility
- Auto-detection of checkpoint format (JSON vs binary)

#### Public APIs
- **ICheckpointStore**: Standard interface for checkpoint save/load operations
  - SaveAsync(ModelCheckpoint, path, CancellationToken)
  - LoadAsync(path, CancellationToken)
- **ITextGenerator**: Interface for text generation (defined for future implementation)
  - GenerateAsync(prompt, GenerationOptions, CancellationToken)
  - GenerateStreamAsync for token-by-token streaming
- **GenerationOptions**: Unified configuration for text generation
  - MaxTokens, Temperature, TopK, Seed, MaxDurationMs
- **CheckpointExtensions**: Extension methods for TransformerModel
  - ToCheckpoint(): Convert model to checkpoint
  - LoadFromCheckpoint(): Load parameters from checkpoint
  - FromCheckpoint(): Factory method to create model from checkpoint

#### Testing
- 10 new comprehensive tests for binary checkpoint functionality:
  - Round-trip data preservation
  - Invalid magic header detection
  - Future version compatibility checks
  - File size comparison (binary vs JSON)
  - Float precision preservation
  - Multi-dimensional tensor support
- Total: 404 passing tests (394 existing + 10 new)

#### Documentation
- Commercial readiness roadmap (`/docs/commercial-readiness-roadmap.md`)
  - Vision statement
  - v0.2 (Library & Packaging), v0.3 (Runtime & Inference), v0.4 (Operability) milestones
  - Semantic versioning policy
  - Checkpoint backward-compatibility policy

### Changed
- Training class now uses ICheckpointStore interface instead of direct JSON serialization
- TransformerModel exposes configuration properties (VocabSize, BlockSize, EmbedDim, NumHeads, NumLayers)
- Default checkpoint format is now binary (.smnd) instead of JSON
- SmallMind.Console and tests reference new multi-project structure

### Deprecated
- JSON checkpoints (.json) are still supported for loading but binary format (.smnd) is recommended for new saves
- TrainOptimized method temporarily disabled (requires additional dependencies not in v0.2 scope)

### Removed
- Explainability features temporarily removed from SmallMind.Runtime (out of scope for v0.2 core functionality)

## [Unreleased]

### Added
- Comprehensive unit test suite with 130+ new tests covering:
  - Guard clause validation for all public APIs
  - Tensor mathematics correctness (MatMul, Softmax, Add, Reshape)
  - SIMD vs scalar equivalence testing for multiple array sizes
  - Custom exception metadata and error code validation
- Integration test suite with 11 end-to-end workflow tests:
  - Training workflows with tiny models
  - Checkpoint save/load round-trip validation
  - Text generation with cancellation support
  - Resource cleanup and leak prevention tests
- Performance regression test suite with conservative thresholds:
  - Matrix multiplication benchmarks (128×128, 256×256)
  - Softmax benchmarks (4K, 8K elements)
  - DotProduct micro-benchmarks
  - Activation function benchmarks (ReLU, GELU)
  - Gated by `RUN_PERF_TESTS` environment variable
- GitHub Actions CI workflow:
  - Unit and integration tests on every PR
  - Performance tests on schedule or with `performance` label
  - Test result publishing
- Production-grade exception hierarchy with error codes
- Input validation with Guard clauses
- Cancellation token support in training methods
- IDisposable implementation for resource management
- Structured logging with high-performance LoggerMessage
- Metrics instrumentation with System.Diagnostics.Metrics
- Health check endpoints for service integration

### Changed
- Improved test coverage from ~40% to >80% for core library

### Deprecated
- None

### Removed
- None

### Fixed
- None

### Security
- None

## [0.1.0] - Initial Release

### Added
- Basic Transformer model implementation
- Character-level tokenization
- Training from scratch on CPU
- Text generation with temperature sampling
- Checkpoint saving/loading
- SIMD acceleration for core operations
