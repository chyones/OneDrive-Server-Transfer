# Security Policy

## Security posture

OneDrive Server Transfer is an internal IT-controlled, read-only Microsoft 365 archival-copy utility. It must never request Microsoft 365 write permissions, authenticate as an employee, or modify source OneDrive content.

Binding security requirements are defined by:

1. `IMPLEMENTATION_CONTRACT.md`
2. `docs/SECURITY_AND_INTEGRITY_REQUIREMENTS.md`

`IMPLEMENTATION_CONTRACT_AMENDMENTS.md` is superseded.

## Access model

- The application is for authorized IT users only.
- The operator authenticates through Microsoft Entra ID using interactive delegated MSAL.
- The configured tenant must be validated after sign-in.
- The configured authorized transfer-account Entra object-ID allowlist must be enforced when present.
- Authorization must not rely on display name or mutable email address alone.
- A dedicated transfer administrator account should be used where practical.
- Temporary employee OneDrive administrative access is granted and removed outside the application.

## Employee credentials are prohibited

The application must never:

- request an employee password;
- present an employee-password field;
- collect, store, log, transmit, or process an employee password;
- authenticate as the employee; or
- represent an IT archive action as employee activity.

The employee source is identified by UPN or OneDrive root URL. The authorized IT operator remains the authenticated actor.

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
- no application-only authentication
- no write permission
- Microsoft Graph v1.0 only

Grant and remove employee OneDrive administrative access outside the application. The application only validates existing read access.

## Source identification and dry run

- Accept one employee UPN or one employee OneDrive for Business root URL.
- Resolve the final source to Tenant ID, employee Entra object ID, and source Drive ID.
- Reject consumer OneDrive, shared links, files, subfolders, SharePoint libraries, Teams libraries, and external tenants.
- Require a successful `Scan` before enabling `Start Copy`.
- The scan must not download employee file content.
- Changing the source or destination invalidates the previous scan.
- The scan result is preflight information and must never be described as a completed archive.

## Source-content boundary

- copy supported active file and folder items only
- classify OneNote notebooks and other Graph package items as `Unsupported` in version 1
- report unsupported items and mark the archive `Incomplete`
- never silently claim unsupported or failed content was copied
- reject external shortcut content belonging to another drive
- exclude previous versions, Recycle Bin, sharing, compliance, and audit content
- never upload, edit, rename, move, delete, or change source permissions

## Local destination security

- accept local fixed or directly attached storage only
- reject UNC, mapped drives, NAS, SMB, and remote destinations
- reject Windows system directories and application installation directories
- bind each destination to one tenant, employee Entra object ID, and source drive
- record the operator identity for audit without binding the archive permanently to one operator
- reject another source or unsafe non-empty destination
- use deterministic `PathMappingVersion = 1`
- expand deterministic collision suffixes when the initial suffix still collides
- restrict NTFS access to authorized accounts
- require known remaining bytes plus the fixed 5 GiB safety reserve
- fail safely on disk-full without false completion
- use BitLocker or an approved organizational equivalent or exception for production storage
- do not broadly disable antivirus, EDR, firewall, or application-control protections

## Integrity, state, and containment

- use Graph delta inventory and persist checkpoints safely
- use SQLite transactions for application state
- keep SQLite as the operational source for scan, resume, and recovery
- treat CSV and JSON files as audit reports only
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

## Logging and reports

Every copy run must generate:

- protected technical log
- CSV transfer manifest
- failed-item report
- JSON transfer summary
- exact terminal state

Reports must follow `docs/REPORT_SCHEMA.md`, use UTC timestamps, apply CSV formula-injection protection, and sanitize error messages. Reports must not contain passwords, tokens, cookies, authorization headers, temporary download URLs, or raw Graph responses.

`Incomplete` must be used when supported content failed, unsupported content exists, or the source did not stabilize. `CompletedWithWarnings` is reserved for non-content warnings after every supported item was copied or validly skipped.

## Storage and endpoint protection

Production validation must record that:

- the application runs under an authorized Windows account;
- employee archive data is not broadly readable through inherited ACLs;
- application and token-cache files are not readable by unrelated normal users;
- the archive volume uses BitLocker, an approved organizational equivalent, or an approved exception; and
- antivirus, EDR, firewall, and application-control protections were not broadly disabled.

## Release controls

Before an approved internal release:

- use deterministic dependency restore;
- run automated tests and Windows Release build;
- run dependency-vulnerability and secret scans;
- generate an SBOM where supported;
- tie the published output to an exact source commit;
- apply Authenticode signing when an approved organizational certificate is available; and
- document the approved limitation when signing is unavailable.

## Reporting a security issue

Use a private repository-owner communication channel or GitHub private security reporting when enabled. Provide only redacted information:

- concise description
- affected component
- reproduction conditions
- potential impact
- redacted evidence

Never include active credentials, employee content, tokens, private keys, production state databases, or temporary download URLs.
