# Security Policy

## Supported Versions

We currently support the following versions with security updates:

| Version | Supported          |
| ------- | ------------------ |
| 0.3.x   | :white_check_mark: |
| 0.2.x   | :x:                |
| < 0.2   | :x:                |

## Reporting a Vulnerability

We take the security of SmallMind seriously. If you discover a security vulnerability, please follow these steps:

### Private Disclosure

**Please do not report security vulnerabilities through public GitHub issues.**

Instead, please report them via one of these methods:

1. **GitHub Security Advisories** (Preferred): 
   - Go to https://github.com/justinamiller/SmallMind/security/advisories
   - Click "Report a vulnerability"
   - Fill in the details of the vulnerability

2. **Email**: 
   - Send details to the repository owner
   - Include "SECURITY" in the subject line
   - Provide a detailed description of the vulnerability

### What to Include

When reporting a vulnerability, please include:

- Type of vulnerability (e.g., buffer overflow, injection, authentication bypass)
- Full paths of source file(s) related to the vulnerability
- Location of the affected source code (tag/branch/commit or direct URL)
- Step-by-step instructions to reproduce the issue
- Proof-of-concept or exploit code (if possible)
- Impact of the vulnerability, including how an attacker might exploit it

### Response Timeline

- **Initial Response**: Within 48 hours of report submission
- **Status Update**: Within 7 days with our assessment and planned fix timeline
- **Fix Release**: Depends on severity and complexity
  - Critical: Within 7 days
  - High: Within 30 days
  - Medium/Low: Next scheduled release

### Disclosure Policy

- Security vulnerabilities will be disclosed publicly after a fix is available
- We will credit reporters in the security advisory (unless they prefer to remain anonymous)
- We follow coordinated disclosure practices

## Security Best Practices for Users

When using SmallMind in production:

1. **Model Loading**: Only load models from trusted sources
2. **Input Validation**: Validate and sanitize all user inputs before generation
3. **Resource Limits**: Use `GenerationOptions.MaxNewTokens` and budget controls to prevent resource exhaustion
4. **Untrusted Data**: When loading `.gguf` files or other external formats, ensure they come from verified sources
5. **Quantized Models**: Verify checksums of quantized models before deployment
6. **File Permissions**: Restrict write access to model directories
7. **Dependencies**: Keep .NET runtime updated to the latest LTS version

## Known Security Considerations

### Model Poisoning
- SmallMind loads models that may have been trained on malicious data
- Always verify model sources and use models from trusted providers
- Consider model validation and testing before production deployment

### Resource Exhaustion
- Generation without token limits can consume excessive CPU/memory
- Always use `MaxNewTokens`, `MaxExecutionTime`, and `MaxMemory` budgets
- Monitor resource usage in production environments

### Prompt Injection
- Like all LLMs, SmallMind is susceptible to prompt injection attacks
- Implement application-level input validation and output filtering
- Use `DomainReasoner` constraints to limit generation scope

### Data Privacy
- SmallMind runs locally and makes no external API calls
- However, model weights may encode training data patterns
- For sensitive applications, train custom models on controlled datasets

## Security Updates

Security updates will be:
- Announced via GitHub Security Advisories
- Documented in CHANGELOG.md with a "Security" section
- Tagged with version bumps following semantic versioning

## Security Testing

SmallMind includes:
- Input validation tests for guard clauses
- Exception handling for malformed inputs
- Resource limit enforcement tests
- Safe model loading with validation

We welcome security-focused contributions and testing.
