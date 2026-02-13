# Release Checklist

Use this checklist before publishing a new SmallMind release.

## Pre-Release

- [ ] **CI green on all OS targets**: Ubuntu, Windows, macOS matrix passes
- [ ] **CodeQL green**: No critical or high severity findings
- [ ] **Golden tests pass**: All deterministic output tests succeed
- [ ] **Unit tests pass**: Full test suite (`SmallMind.Tests`) on all platforms
- [ ] **Integration tests pass**: `SmallMind.IntegrationTests` pass
- [ ] **Perf smoke pass**: Linux perf benchmarks complete without regression

## Documentation

- [ ] **Compatibility matrix updated**: `docs/compatibility-matrix.md` reflects any new tensor types, formats, or limits
- [ ] **PERFORMANCE_RESULTS_SUMMARY.md updated**: Latest benchmark numbers included
- [ ] **README updated**: Version badges, feature list, breaking changes noted
- [ ] **CHANGELOG updated**: All changes since last release documented

## Packages

- [ ] **Version number set**: Follow semantic versioning (`MAJOR.MINOR.PATCH`)
- [ ] **NuGet packages build**: `dotnet pack` succeeds for all library projects
- [ ] **Package metadata correct**: Description, license, project URL, repository URL

## Release

1. Create a git tag: `git tag v{VERSION}`
2. Push the tag: `git push origin v{VERSION}`
3. Create a GitHub Release from the tag with changelog notes
4. The `release.yml` workflow will:
   - Build and test the solution
   - Pack NuGet packages with the tag version
   - Push to NuGet.org (requires `NUGET_API_KEY` secret)
   - Upload packages as workflow artifacts

## Post-Release

- [ ] Verify NuGet packages are published and accessible
- [ ] Verify GitHub Release artifacts are available
- [ ] Announce release in relevant channels

## Packages Published

| Package | Description |
|---------|-------------|
| `SmallMind` | Stable public API |
| `SmallMind.Core` | Tensor operations, SIMD kernels |
| `SmallMind.Transformers` | Transformer model implementations |
| `SmallMind.Tokenizers` | BPE and character tokenization |
| `SmallMind.Runtime` | Inference engine and sessions |
| `SmallMind.Rag` | Retrieval-augmented generation |
| `SmallMind.Quantization` | Q4/Q6/Q8 quantization |
