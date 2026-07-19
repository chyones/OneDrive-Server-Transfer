# Security Policy

## Security posture

OneDrive Server Transfer is a read-only internal Microsoft 365 copy utility. It must never request Microsoft 365 write permissions or modify source OneDrive content.

Binding security requirements are defined by:

1. `IMPLEMENTATION_CONTRACT.md`
2. `docs/SECURITY_AND_INTEGRITY_REQUIREMENTS.md`

`IMPLEMENTATION_CONTRACT_AMENDMENTS.md` is superseded.

## Never commit secrets or employee data

Do not commit:

- Microsoft 365 passwords
- access or refresh tokens
- client secrets
- private certificates or private keys
- authorization headers or cookies
- temporary download URLs
- production `appsettings.json`
- employee OneDrive files
- production SQLite state databases
- production logs or reports containing employee information
- unredacted tenant, account, destination, or ACL details

Only placeholder configuration and redacted evidence may be committed.

## Approved Microsoft access model

- interactive delegated MSAL sign-in
- single-tenant public-client registration
- MFA and Conditional Access support
- `User.Read`, `Files.Read.All`, `Sites.Read.All`, `offline_access`, `openid`, and `profile`
- configured-tenant validation
- authorized transfer-account Entra object-ID allowlist when configured
- no display-name or mutable-email-only authorization
- no client secret
- no write permission
- Microsoft Graph v1.0 only

Use a dedicated transfer administrator account where practical. Grant and remove employee OneDrive administrative access outside the application.

## Source-content boundary

- copy supported active file and folder items only
- classify OneNote notebooks and other Graph package items as `Unsupported` in version 1
- report unsupported items and never silently claim they were copied
- reject external shortcut content belonging to another drive
- exclude previous versions, Recycle Bin, sharing, compliance, and audit content

## Local destination security

- accept local attached storage only
- reject UNC, mapped drives, NAS, SMB, and remote destinations
- bind each destination to one tenant, employee, and drive
- reject another source or unsafe non-empty destination
- use deterministic `PathMappingVersion = 1`
- restrict NTFS access to authorized accounts
- require known remaining bytes plus the fixed 5 GiB safety reserve
- fail safely on disk-full without false completion
- use BitLocker or an approved organizational equivalent or exception for production storage
- do not broadly disable antivirus, EDR, firewall, or application-control protections

## Integrity, state, and containment

- use Graph delta inventory and persist checkpoints safely
- use SQLite transactions for application state
- validate SQLite integrity before resume
- create protected backups before schema migrations
- fail without silent reset when state is corrupt or migration fails
- calculate local SHA-256 for completed files
- distinguish local SHA-256 from supported Microsoft source-hash verification
- preserve source timestamps where supported and report failures
- prevent path traversal and unsafe reparse-point redirection
- revalidate destination containment during file operations
- do not follow or overwrite untrusted hard-linked destination files
- never send Graph credentials to temporary download hosts
- isolate reports by `_TransferReport\Runs\<RunId>`

## Reporting a security issue

Use a private repository-owner communication channel or GitHub private security reporting when enabled. Provide only redacted information:

- concise description
- affected component
- reproduction conditions
- potential impact
- redacted evidence

Never include active credentials, employee content, tokens, private keys, production state databases, or temporary download URLs.
