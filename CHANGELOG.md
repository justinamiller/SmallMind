# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
