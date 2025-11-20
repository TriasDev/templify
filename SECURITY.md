# Security Policy

## Supported Versions

We release patches for security vulnerabilities in the following versions:

| Version | Supported          |
| ------- | ------------------ |
| 1.x.x   | :white_check_mark: |

## Reporting a Vulnerability

The Templify team takes security bugs seriously. We appreciate your efforts to responsibly disclose your findings.

### How to Report

**Please do NOT report security vulnerabilities through public GitHub issues.**

Instead, please report security vulnerabilities through one of the following methods:

1. **GitHub Security Advisory** (Preferred)
   - Go to https://github.com/TriasDev/templify/security/advisories
   - Click "Report a vulnerability"
   - Fill in the details of the vulnerability

2. **Email**
   - Send an email to the project maintainers through GitHub
   - Include "SECURITY" in the subject line
   - Provide detailed information about the vulnerability

### What to Include

To help us better understand and resolve the issue, please include:

- **Description** of the vulnerability
- **Steps to reproduce** the issue
- **Potential impact** of the vulnerability
- **Suggested fix** (if available)
- **Your contact information** for follow-up questions

### Response Timeline

We will make every effort to respond according to the following timeline:

- **Initial Response**: Within 48 hours of receiving the report
- **Status Update**: Within 7 days with either:
  - Confirmation of the issue and estimated timeline for a fix
  - Explanation if the issue is not considered a vulnerability
- **Resolution**: Depends on severity:
  - **Critical**: Within 7 days
  - **High**: Within 30 days
  - **Medium/Low**: Next minor/patch release

### Disclosure Policy

- We will coordinate the public disclosure with you
- We will credit you in the security advisory (unless you prefer to remain anonymous)
- We ask that you do not publicly disclose the vulnerability until we have released a fix
- We will publish a security advisory on GitHub when the fix is released

## Security Update Process

When a security vulnerability is confirmed:

1. We will develop and test a fix
2. We will release a new patch version with the fix
3. We will publish a GitHub Security Advisory with details
4. We will update this SECURITY.md if needed

## Security Best Practices

When using Templify in your application:

### Input Validation

- **Validate template sources**: Only process templates from trusted sources
- **Sanitize user data**: Validate and sanitize data before passing to the template processor
- **Limit template size**: Set reasonable limits on template file sizes to prevent resource exhaustion

### Data Handling

- **Sensitive data**: Ensure sensitive information in templates is properly protected
- **Output validation**: Validate generated documents before distribution
- **Access control**: Implement proper access controls for template and data files

### Deployment

- **Keep dependencies updated**: Regularly update Templify and its dependencies (especially DocumentFormat.OpenXml)
- **Use latest stable version**: Always use the latest stable version of Templify
- **Monitor security advisories**: Watch the GitHub repository for security updates

### Example: Safe Usage

```csharp
using TriasDev.Templify;

public class SecureTemplateProcessor
{
    private const int MaxTemplateSizeBytes = 10 * 1024 * 1024; // 10 MB

    public ProcessingResult ProcessTemplate(Stream templateStream, Dictionary<string, object> data)
    {
        // Validate template size
        if (templateStream.Length > MaxTemplateSizeBytes)
        {
            throw new ArgumentException("Template file is too large");
        }

        // Sanitize data (example - adjust based on your needs)
        var sanitizedData = SanitizeData(data);

        // Process with Templify
        var processor = new DocumentTemplateProcessor();
        var outputStream = new MemoryStream();

        return processor.ProcessTemplate(templateStream, outputStream, sanitizedData);
    }

    private Dictionary<string, object> SanitizeData(Dictionary<string, object> data)
    {
        // Implement your data sanitization logic here
        // Remove/escape potentially dangerous content
        return data;
    }
}
```

## Known Security Considerations

### OpenXML Processing

Templify uses the DocumentFormat.OpenXml SDK to process Word documents. This library:
- Validates document structure
- Does not execute macros or embedded code
- Processes documents in a sandboxed manner

### No External Dependencies at Runtime

Templify has minimal runtime dependencies, reducing the attack surface:
- DocumentFormat.OpenXml (Microsoft official library)
- .NET runtime libraries

### Memory Considerations

Templify loads entire documents into memory for processing:
- Suitable for documents up to ~50MB
- Very large documents may cause memory exhaustion
- Implement size limits in your application

## Vulnerability Disclosure History

We will list all disclosed vulnerabilities here once we have our first release.

Currently: No vulnerabilities have been reported.

## Questions?

If you have questions about security that are not vulnerabilities, please:
- Open a GitHub Discussion
- Tag your question with the "security" label

---

Thank you for helping keep Templify and its users safe!
