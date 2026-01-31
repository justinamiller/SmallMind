# Contributing to SmallMind

Thank you for your interest in contributing to SmallMind! This project is an educational implementation of a language model in pure C#, and we welcome contributions that enhance its learning value.

## Getting Started

1. Fork the repository
2. Clone your fork: `git clone https://github.com/YOUR_USERNAME/SmallMind.git`
3. Create a new branch: `git checkout -b feature/your-feature-name`
4. Make your changes
5. Test your changes: `dotnet test`
6. Commit your changes: `git commit -m "Add your feature"`
7. Push to your fork: `git push origin feature/your-feature-name`
8. Open a Pull Request

## Development Setup

### Requirements
- .NET 8 SDK
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

1. **Update tests**: Ensure all tests pass and add new tests for your changes
2. **Update documentation**: Update README.md and relevant docs
3. **Follow the code style**: Match the existing code style
4. **Keep it focused**: One feature or fix per PR
5. **Write a clear description**: Explain what changed and why

### PR Checklist
- [ ] Code builds without errors
- [ ] All tests pass (`dotnet test`)
- [ ] New code has tests
- [ ] Documentation is updated
- [ ] Commit messages are clear
- [ ] No external dependencies added (unless System.*)

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
