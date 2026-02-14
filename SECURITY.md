# Security Policy

## Supported Versions

SmallMind follows [Semantic Versioning](https://semver.org/). Security updates are provided for the following versions:

| Version | Supported          |
| ------- | ------------------ |
| 1.x.x   | :white_check_mark: |
| < 1.0   | :x:                |

We recommend always using the latest stable release to ensure you have the most recent security patches and improvements.

## Reporting a Vulnerability

The SmallMind team takes security vulnerabilities seriously. We appreciate your efforts to responsibly disclose your findings and will make every effort to acknowledge your contributions.

### How to Report a Security Vulnerability

**Please do NOT report security vulnerabilities through public GitHub issues.**

Instead, please report security vulnerabilities through one of the following secure channels:

1. **GitHub Security Advisory** (Recommended):
   - Navigate to https://github.com/justinamiller/SmallMind/security/advisories
   - Click "Report a vulnerability"
   - Provide detailed information about the vulnerability

2. **Email**:
   - Send an email to: security@smallmind.dev
   - Include detailed information about the vulnerability (see below)

### Information to Include

To help us better understand and resolve the issue, please include as much of the following information as possible:

- **Type of vulnerability** (e.g., buffer overflow, SQL injection, cross-site scripting, path traversal, etc.)
- **Full paths of source file(s)** related to the manifestation of the vulnerability
- **Location of the affected source code** (tag/branch/commit or direct URL)
- **Step-by-step instructions to reproduce the vulnerability**
- **Proof-of-concept or exploit code** (if possible)
- **Impact of the vulnerability**, including how an attacker might exploit it
- **Any special configuration required** to reproduce the issue

### What to Expect

After you submit a vulnerability report, you can expect:

1. **Acknowledgment**: We will acknowledge receipt of your vulnerability report within **72 hours** (3 business days).

2. **Assessment**: Our security team will investigate and assess the vulnerability. We will provide an initial assessment within **7 days** of acknowledgment.

3. **Updates**: We will keep you informed about our progress in addressing the vulnerability. You can expect regular updates at least every **14 days** until the issue is resolved.

4. **Resolution Timeline**: We aim to resolve critical vulnerabilities within **30 days** of confirmation. The timeline may vary depending on the complexity of the issue:
   - Critical vulnerabilities: 7-30 days
   - High severity vulnerabilities: 30-60 days
   - Medium/Low severity vulnerabilities: 60-90 days

5. **Disclosure**: We will work with you to understand an appropriate disclosure timeline. We prefer coordinated disclosure and will credit you in our security advisory (unless you prefer to remain anonymous).

## Security Vulnerability Response Process

Our security vulnerability response process includes:

1. **Triage**: We evaluate the severity and impact of the reported vulnerability
2. **Patching**: We develop and test a fix for the vulnerability
3. **Release**: We release the security patch as soon as possible
4. **Disclosure**: We publish a security advisory with details about the vulnerability and the fix
5. **Credit**: We acknowledge the reporter (unless they prefer anonymity)

## Security Best Practices

When using SmallMind in your projects, we recommend following these security best practices:

### Model Files and Data Security

- **Validate Model Files**: Always verify the integrity and source of model files before loading them
- **Restrict File Paths**: Use absolute paths and validate that model files are within expected directories
- **Input Validation**: Sanitize and validate all user inputs before processing with the model
- **Rate Limiting**: Implement appropriate rate limiting when exposing SmallMind through an API

### Deployment Security

- **Least Privilege**: Run SmallMind with minimal required permissions
- **Network Security**: When using the HTTP server, implement appropriate network security measures (TLS, authentication, etc.)
- **Resource Limits**: Configure appropriate memory and CPU limits to prevent resource exhaustion attacks
- **Dependency Updates**: Keep SmallMind and all dependencies up to date with the latest security patches

### Code Security

- **Code Review**: Review and test any code changes that interact with SmallMind
- **Static Analysis**: Use static analysis tools (CodeQL, Roslyn analyzers) to detect potential security issues
- **Unit Testing**: Ensure comprehensive test coverage, including negative test cases for error handling

## Security Features

SmallMind includes several built-in security features:

- **Zero Native Dependencies**: Pure .NET implementation reduces attack surface
- **CodeQL Analysis**: Continuous security scanning with GitHub CodeQL
- **Dependabot**: Automated dependency vulnerability scanning and updates
- **OpenSSF Scorecard**: Regular security posture assessment
- **Input Validation**: Built-in validation for model parameters and configurations
- **Memory Safety**: Leverages .NET's memory safety guarantees

## Known Security Considerations

- **Resource Usage**: Language model inference is CPU and memory intensive. Implement appropriate resource limits in production environments to prevent denial-of-service conditions
- **Model Poisoning**: Be cautious about the source of model weights. Only use models from trusted sources
- **Output Sanitization**: LLM outputs should be sanitized before being displayed to users or used in sensitive contexts

## Security Updates

Security updates are distributed through:

- **GitHub Security Advisories**: https://github.com/justinamiller/SmallMind/security/advisories
- **GitHub Releases**: https://github.com/justinamiller/SmallMind/releases
- **NuGet Package Updates**: https://www.nuget.org/packages/SmallMind/

We strongly recommend subscribing to security advisories and enabling Dependabot alerts for your projects.

## Contact

For security-related questions or concerns that are not vulnerabilities, you can reach out through:

- GitHub Discussions: https://github.com/justinamiller/SmallMind/discussions
- General Email: support@smallmind.dev

For vulnerability reports, please use the secure channels described in the "Reporting a Vulnerability" section above.

## Attribution

We would like to thank all security researchers and users who have responsibly disclosed vulnerabilities to us. Your contributions help make SmallMind more secure for everyone.

---

**Note**: This security policy is subject to change. Please check back regularly for updates. Last updated: February 2026.
