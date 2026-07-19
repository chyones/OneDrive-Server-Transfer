# Project Memory

Store durable project facts here only. Current phase and evidence belong only in `.ai/PHASE_STATUS.md`; the active task belongs only in `.ai/HANDOFF.md`.

## Identity

- Repository: `chyones/OneDrive-Server-Transfer`
- Product: internal Windows desktop archival-copy application
- Operator: authorized IT administrator
- UI language: English
- Primary target: Windows Server 2019 x64 with Desktop Experience
- Solution location: `./OneDriveServerTransfer.sln`

## Product boundary

The application copies supported active files and folders from one employee's Microsoft 365 OneDrive for Business to local fixed or directly attached storage on the same Windows Server.

It never deletes, edits, renames, moves, uploads, or changes permissions on Microsoft 365 source content.

The one-window workflow is:

1. sign in as the authorized IT transfer operator;
2. enter employee UPN or OneDrive root URL;
3. select local destination;
4. run mandatory `Scan`;
5. review preflight results;
6. run `Start Copy`;
7. monitor progress and review reports.

## Fixed platform

- C# and .NET 10 LTS
- WPF and MVVM
- Microsoft Graph `v1.0`
- delegated interactive MSAL
- WAM preferred with system-browser fallback
- dependency injection and structured logging
- embedded SQLite operational state
- self-contained `win-x64` publish

## Fixed controls

- No employee-password collection or employee authentication.
- No Graph beta, application permissions, Microsoft 365 write permissions, ROPC, device-code flow, client secrets, or certificates.
- Only approved endpoints and scopes from `docs/GRAPH_ENDPOINT_PERMISSION_MATRIX.md`.
- One employee and one business OneDrive root per run.
- Mandatory dry run before copy; source or destination changes invalidate it.
- Destination bound to tenant ID, employee object ID, and drive ID.
- Local destinations only; no UNC, NAS, SMB, mapped, or remote storage.
- SQLite is the operational source for scan, resume, recovery, binding, and mappings.
- CSV and JSON are audit outputs only.
- Maximum three concurrent downloads.
- Streaming, `.partial` files, validated Range resume, bounded retry, and `Retry-After` handling.
- Temporary download URLs are never logged or persisted and never receive Graph credentials.
- Delta links are opaque; supported `410 Gone` requires fresh enumeration and reconciliation.
- Local SHA-256 remains separate from supported Microsoft source hashes; Graph `sha256Hash` is ignored.
- `PathMappingVersion = 1` and `StateSchemaVersion = 1` start compatibility versioning.
- Fixed 5 GiB destination-space reserve.
- Unsupported package content or missing supported content produces `Incomplete`.
- Each run has an isolated `_TransferReport/Runs/<RunId>` directory.
- Production storage requires restricted NTFS access and BitLocker, approved equivalent, or approved exception.

## Out of scope for version 1

Dashboards, scheduling, batch employees, service mode, remote administration, remote destinations, central reporting, email notifications, source modification, previous versions, Recycle Bin, permissions, comments, compliance data, OneNote/package export, SharePoint libraries, and Teams libraries.

## External values not stored in Git

Tenant values, real Client ID, account object IDs, employee identities, production paths, credentials, tokens, employee content, production databases, and unredacted reports remain outside the repository. Their readiness is tracked in `docs/ENVIRONMENT_AND_INPUTS.md`.
