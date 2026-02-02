# Contributing to SmallMind

Thank you for your interest in contributing to SmallMind! This project is an educational implementation of a language model in pure C#, and we welcome contributions that enhance its learning value.

## Branch Protection Policy

⚠️ **Important**: The `main` branch is protected. You cannot push directly to it.

**All changes to `main` must go through pull requests** that:
- ✅ Pass all automated tests (build, unit tests, integration tests)
- ✅ Receive at least one approval from a code reviewer
- ✅ Have all conversations resolved
- ✅ Be up-to-date with the base branch

For detailed information on branch protection setup, see [docs/BRANCH_PROTECTION_SETUP.md](docs/BRANCH_PROTECTION_SETUP.md).

## Getting Started

1. Fork the repository
2. Clone your fork: `git clone https://github.com/YOUR_USERNAME/SmallMind.git`
3. Create a new branch: `git checkout -b feature/your-feature-name`
4. Make your changes
5. Test your changes: `dotnet test`
6. Commit your changes: `git commit -m "Add your feature"`
7. Push to your fork: `git push origin feature/your-feature-name`
8. Open a Pull Request to the `main` branch
9. Wait for CI/CD checks to complete
10. Address any review feedback
11. Get approval from a maintainer
12. Merge when approved and all checks pass

## Development Setup

### Requirements
- .NET 10 SDK
- A code editor (Visual Studio, VS Code, Rider, etc.)

### Building the Project
```bash
dotnet build
```

### Running Tests
```bash
dotnet test
```

### Running the Console App
```bash
dotnet run --project src/SmallMind.Console
```

## Project Structure

```
SmallMind/
├── src/
│   ├── SmallMind/              # Core library
│   └── SmallMind.Console/      # Demo console application
├── tests/
│   └── SmallMind.Tests/        # Unit and integration tests
├── samples/                     # Example code
├── docs/                        # Documentation
└── benchmarks/                  # Performance benchmarks (future)
```

## Coding Guidelines

### General Principles
- **Pure C# only**: No external dependencies except System.* namespaces
- **Educational focus**: Code should be clear and well-commented
- **Performance matters**: This runs on CPU, so optimize where possible
- **Test coverage**: Add tests for new features

### Code Style
- Follow standard C# naming conventions
- Use meaningful variable names
- Add XML documentation comments for public APIs
- Keep methods focused and reasonably sized
- Use LINQ sparingly (prefer explicit loops for clarity)

### Performance Guidelines
- Minimize allocations in hot paths
- Use `Span<T>` and `Memory<T>` where appropriate
- Consider SIMD vectorization for array operations
- Profile before optimizing

## Types of Contributions

### Bug Fixes
- Fix issues without breaking existing functionality
- Add tests that demonstrate the bug is fixed
- Update documentation if needed

### New Features
Before implementing a major feature, please open an issue to discuss:
- Is it aligned with the educational goals?
- Does it maintain the "pure C#" principle?
- How will it affect performance?

Good feature ideas:
- Performance optimizations (SIMD, better algorithms)
- Additional model architectures
- Better tokenization strategies
- Training improvements (learning rate schedules, gradient clipping)
- Enhanced data loading capabilities
- Better visualization/logging

### Documentation
- Improve README clarity
- Add code examples
- Expand API documentation
- Write tutorials or guides

### Tests
- Increase test coverage
- Add performance benchmarks
- Improve test clarity

## Pull Request Process

### Before Opening a PR

1. **Ensure your branch is up-to-date**:
   ```bash
   git checkout main
   git pull origin main
   git checkout your-feature-branch
   git rebase main  # or git merge main
   ```

2. **Run all tests locally**:
   ```bash
   dotnet test
   ```

3. **Build in Release configuration**:
   ```bash
   dotnet build --configuration Release
   ```

### Opening Your PR

1. **Update tests**: Ensure all tests pass and add new tests for your changes
2. **Update documentation**: Update README.md and relevant docs
3. **Follow the code style**: Match the existing code style
4. **Keep it focused**: One feature or fix per PR
5. **Write a clear description**: Explain what changed and why

### PR Review Process

After opening your PR:

1. **Automated checks run**: GitHub Actions will build and test your code
2. **Wait for review**: A maintainer will review your changes
3. **Address feedback**: Make requested changes and push updates
4. **Get approval**: At least one approval is required
5. **Merge**: Once approved and checks pass, your PR can be merged

### What Happens During Review

- 🤖 **Automated Checks** (must pass):
  - Build succeeds
  - Unit tests pass
  - Integration tests pass
  - Code follows quality standards

- 👤 **Code Review** (at least 1 approval required):
  - Code quality and correctness
  - Test coverage
  - Documentation updates
  - Performance considerations

- 💬 **Discussion**:
  - All conversations must be resolved before merge
  - Ask questions if feedback is unclear
  - Explain your design decisions

### PR Checklist
- [ ] Code builds without errors
- [ ] All tests pass (`dotnet test`)
- [ ] New code has tests
- [ ] Documentation is updated
- [ ] Commit messages are clear
- [ ] No external dependencies added (unless System.*)
- [ ] Branch is up-to-date with `main`
- [ ] All GitHub Actions checks pass
- [ ] PR has been reviewed and approved

## Why Pull Requests Are Required

Direct pushes to `main` are disabled to ensure:

1. **Code Quality**: Every change is reviewed by at least one other developer
2. **Testing**: All automated tests must pass before merge
3. **Discussion**: Changes can be discussed and improved
4. **Documentation**: Changes are documented in the PR description
5. **Audit Trail**: All changes are traceable and reversible
6. **Stability**: The `main` branch always contains working, tested code

If you try to push directly to `main`, you'll see an error like:
```
remote: error: GH006: Protected branch update failed for refs/heads/main.
```

This is expected! Create a branch and open a PR instead.

## Testing

### Running All Tests
```bash
dotnet test
```

### Running Specific Tests
```bash
dotnet test --filter "FullyQualifiedName~SmallMind.Tests.DataLoaderTests"
```

### Adding New Tests
- Place tests in `tests/SmallMind.Tests/`
- Use xUnit framework
- Follow the existing test naming patterns
- Test both happy paths and edge cases

## Performance Considerations

When contributing performance improvements:
1. **Measure first**: Use profiling tools to identify bottlenecks
2. **Benchmark**: Compare before and after performance
3. **Document**: Explain why the change improves performance
4. **Trade-offs**: Note any code complexity increases

## Questions?

- Open an issue for questions about development
- Check existing documentation in the `docs/` folder
- Review the code - it's designed to be educational!

## Code of Conduct

Be respectful, constructive, and helpful. This is a learning project, so:
- Be patient with beginners
- Explain your reasoning
- Focus on the code, not the person
- Celebrate learning moments

## License

By contributing, you agree that your contributions will be licensed under the MIT License.
