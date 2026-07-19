# Project Memory

This file contains durable project facts. Do not place transient logs, secrets, or speculative ideas here.

## Identity

- Project: OneDrive Server Transfer
- Repository: `chyones/OneDrive-Server-Transfer`
- Product type: Internal Windows desktop application
- Primary operator: Authorized IT administrator
- Runtime target: Windows Server 2019 x64 with Desktop Experience
- UI language: English
- Current state: `Documentation Ready`; application implementation not started
- Completed phase: `M0 — Contract simplification and pre-implementation hardening`
- M0 evidence: `artifacts/evidence/M00_preimplementation-hardening_20260719T113850Z.json`
- Validated documentation baseline: `e9434ff54c373e1d0129ba2583027897f6f3ff25`
- Next phase: `M1 — Solution and CI foundation`

## Binding authority

- Current explicit repository-owner instructions have highest project authority.
- `IMPLEMENTATION_CONTRACT.md` is the single binding repository contract.
- `IMPLEMENTATION_CONTRACT_AMENDMENTS.md` is superseded and retained only for history.
- Repository root is the project root.
- Required solution path: `./OneDriveServerTransfer.sln`.
- The custom disk-index engine, JSONL state engine, and five-million-item release benchmark are not active first-release requirements.

## Product purpose and workflow

Copy the supported active files and folders from one employee's Microsoft 365 OneDrive for Business root to a local destination selected by the IT administrator on the same Windows Server.

Workflow:

1. Open the application.
2. Sign in with Microsoft.
3. Paste the employee OneDrive root URL.
4. Select the local destination.
5. Confirm the resolved employee, authorized transfer account, and destination.
6. Press `Copy Data`.
7. Monitor progress and review the result.

The application remains read-only against Microsoft 365.

## Fixed technology

- C#
- .NET 10 LTS
- WPF
- MVVM
- Microsoft Graph v1.0
- MSAL
- dependency injection
- structured logging
- automated tests
- embedded local SQLite transfer state

## Authentication rules

- Interactive delegated MSAL only.
- Single configured tenant.
- Public-client application with no client secret.
- Read permissions only.
- Validate the configured tenant after sign-in.
- Enforce the configured authorized transfer-account Entra object-ID allowlist when present.
- Do not authorize by display name or mutable email address alone.

## Source rules

- Accept one employee OneDrive for Business root in the configured tenant.
- Require `driveType = business` and the actual drive root.
- Reject consumer OneDrive, files, subfolders, shared-folder sources, SharePoint libraries, Teams libraries, and external tenants.
- Do not traverse external `remoteItem` or shortcut content belonging to another drive.
- Copy supported active file items, nested folders, and empty folders.
- Classify OneNote notebooks and other Graph package items as `Unsupported`, report them, and never claim they were copied.
- Exclude Recycle Bin, previous versions, sharing metadata, comments, activity, compliance, and audit records.

## Destination rules

- Administrator selects local attached storage on the same Windows Server.
- Reject UNC, mapped drives, NAS, SMB, remote storage, and unsafe redirection.
- Create `OneDriveData` and `_TransferReport`.
- Bind destination to Tenant ID, source Drive ID, and protected employee identity.
- Reject a destination associated with another source.
- Lock destination across processes and Windows sessions.
- Require known remaining bytes plus a fixed 5 GiB free-space reserve.
- Recheck capacity when totals change and before individual files when required.
- Disk-full or reserve failure cannot return `Completed`.

## Inventory, transfer, and recovery

- Use Microsoft Graph drive delta for initial inventory and reconciliation.
- Persist the delta checkpoint.
- Keep pages, queues, hashing, and memory bounded.
- Fixed maximum of three simultaneous downloads.
- Use streaming and `.partial` files.
- Resume only with valid HTTP Range responses.
- Never send Graph credentials to temporary download hosts.
- Retry transient failures up to five attempts per file.
- Use local SQLite state at `_TransferReport/TransferState.db`.
- Validate SQLite integrity before resume.
- Create a protected backup before schema migration and migrate transactionally.
- Never silently reset or adopt content when state is corrupt or migration fails.
- Recovery must be transactional and idempotent.

## Integrity, timestamps, and path safety

- Separate supported Microsoft source-hash verification from local SHA-256.
- Calculate local SHA-256 for every completed file.
- Preserve source creation and modification timestamps where Windows supports the values.
- Timestamp failure produces a warning without invalidating verified bytes.
- Never overwrite unrelated local content.
- Use the exact deterministic `PathMappingVersion = 1` rules in the binding contract.
- Persist mappings and reuse them on resume and rerun.
- Prevent traversal, unsafe reparse-point redirection, and untrusted hard-link overwrite behavior.

## Item, run, and report rules

Approved item states:

```text
Discovered
Mapped
Downloading
Verified
Completed
Skipped
Unsupported
Failed
Cancelled
```

Approved run states:

```text
InProgress
Completed
CompletedWithWarnings
Failed
Cancelled
Interrupted
```

- Clean `Completed` requires no failed or unsupported item, successful required timestamps, and stable reconciliation.
- Store each run under `_TransferReport/Runs/<RunId>`.
- Never overwrite or append to another run's reports.

## Production requirements

- Mandatory Windows CI restore, Release build, and automated tests before `Source Implementation Complete`.
- Restricted NTFS permissions for backup data and token cache.
- BitLocker, approved equivalent, or documented approved exception for production storage.
- Temporary Site Collection Administrator access must be removed, verified, and externally recorded after it is no longer required.
- Production Ready requires real Windows Server, authorized Microsoft sign-in, employee OneDrive copy, unsupported-item reporting when applicable, resume, reconciliation, locking, disk-space, timestamp, report, security, and publish evidence.

## Out of scope

No dashboards, scheduling, batch employee processing, user management, service mode, central reporting, email notifications, remote destinations, OneNote/package export, custom disk-index engine, JSONL state engine, or five-million-item release benchmark.

## Values not yet provided

- tenant name and domain
- Tenant ID
- Entra Client ID
- allowed OneDrive host
- authorized transfer-account Entra object ID or IDs
- dedicated transfer administrator email
- test employee identity and OneDrive root URL
- Windows Server name, build, and execution account
- local production destination
- proxy status
- production NTFS and BitLocker status
- Authenticode certificate availability
