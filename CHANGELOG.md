# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
