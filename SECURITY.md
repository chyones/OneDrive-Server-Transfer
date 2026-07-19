# Security Policy

Binding security behavior is defined in:

1. `IMPLEMENTATION_CONTRACT.md`
2. `docs/SECURITY_AND_INTEGRITY_REQUIREMENTS.md`
3. the Microsoft platform documents referenced by `AGENTS.md`

This file does not duplicate those requirements.

## Non-negotiable posture

- Authorized IT operators only.
- Read-only Microsoft 365 access.
- Never request, process, store, or log an employee password.
- Never authenticate as the employee.
- Never modify or delete source OneDrive content.
- Never commit credentials, tokens, private keys, temporary download URLs, employee content, production databases, or unredacted reports.
- Protect archive data, application state, reports, logs, and token cache with appropriate NTFS permissions and approved storage encryption.
- Do not broadly disable antivirus, EDR, firewall, TLS validation, or application-control protections.

## Reporting a security issue

Use a private repository-owner channel or GitHub private security reporting when enabled. Provide a concise description, affected component, reproduction conditions, potential impact, and redacted evidence only.
