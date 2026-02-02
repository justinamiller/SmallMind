# Security Policy

## Supported Versions

We provide security updates for the latest minor version of SmallMind.

| Version | Supported          |
| ------- | ------------------ |
| latest  | :white_check_mark: |
| < latest| :x:                |

## Reporting a Vulnerability

**Please do not report security vulnerabilities through public GitHub issues.**

Instead, please report security vulnerabilities using one of the following methods:

### Preferred: GitHub Security Advisories

1. Navigate to the [Security Advisories](https://github.com/justinamiller/SmallMind/security/advisories) page
2. Click "Report a vulnerability"
3. Fill out the form with details about the vulnerability

### Alternative: Private Email

If you prefer, you can email security concerns to the maintainer. Please include:

- Description of the vulnerability
- Steps to reproduce
- Potential impact
- Suggested fix (if available)

## Response Timeline

As an open-source project maintained by volunteers, we will make best efforts to:

- Acknowledge receipt of your vulnerability report within 5 business days
- Provide an initial assessment within 10 business days
- Work with you to understand and validate the issue
- Develop and test a fix
- Release a security update and credit you (unless you prefer to remain anonymous)

**Note:** Response times are not guaranteed but represent our best-effort commitment.

## Security Considerations for SmallMind

SmallMind is designed to run **locally** with **no external dependencies** or network communication. Key security features:

- **No remote code execution**: Models and data are loaded from local file system only
- **Resource limits**: Built-in budgets for tokens, time, and memory to prevent resource exhaustion
- **Input validation**: Safe model loading with format validation
- **Deterministic mode**: Reproducible outputs for security testing

### Known Attack Surfaces

When integrating SmallMind into your application, be aware of:

1. **Model files**: Only load models from trusted sources. Maliciously crafted model files could potentially exploit deserialization or parsing vulnerabilities.

2. **User prompts**: If accepting user input as prompts, implement appropriate sanitization and rate limiting in your application layer.

3. **Resource consumption**: Even with built-in budgets, ensure your application has additional safeguards against resource exhaustion.

4. **File system access**: SmallMind reads model files and training data from disk. Ensure appropriate file system permissions.

## Disclosure Policy

When a security issue is fixed:

- We will publish a security advisory describing the vulnerability, affected versions, and remediation
- We will credit the reporter (unless anonymity is requested)
- We will release a new version with the fix
- We will update this security policy if needed

Thank you for helping keep SmallMind and its users safe!
