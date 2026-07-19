# Project Memory

This file contains durable project facts. Do not place transient logs, secrets, or speculative ideas here.

## Identity

- Project: OneDrive Server Transfer
- Repository: `chyones/OneDrive-Server-Transfer`
- Product type: Internal Windows desktop archival-copy application
- Primary operator: Authorized IT administrator
- Runtime target: Windows Server 2019 x64 with Desktop Experience
- UI language: English
- Application implementation started: No
- Production ready: No
- Documentation status: `Documentation Ready`
- Completed documentation phase: `M0 — Contract simplification and pre-implementation hardening`
- M0 evidence: `artifacts/evidence/M00_workflow-alignment_20260719T124036Z.json`
- Validated documentation source commit: `c93b38b7e41ffbb50c82b4f8389e71ef511ac54d`
- Current implementation phase: `M1 — Solution and CI foundation`
- M1 status: `NOT_STARTED`
- M1 start authorization: Granted; mark M1 `IN_PROGRESS` before creating source files

## Binding authority

- Current explicit repository-owner instructions have highest project authority.
- `IMPLEMENTATION_CONTRACT.md` is the single binding repository contract.
- `IMPLEMENTATION_CONTRACT_AMENDMENTS.md` is superseded and retained only for history.
- Repository root is the project root.
- Required solution path: `./OneDriveServerTransfer.sln`.
- The custom disk-index engine, JSONL state engine, and five-million-item release benchmark are not active first-release requirements.
- Exact current phase status and evidence are recorded in `.ai/PHASE_STATUS.md`.

## Product purpose and terminology

Copy and archive the supported active files and folders from one employee's Microsoft 365 OneDrive for Business to a local destination selected by the authorized IT administrator on the same Windows Server.

The product performs a copy only. It does not delete, rename, move, edit, upload, or change permissions on Microsoft 365 source content.

Use `copy` or `archive` in user-facing and operational language. The repository name may remain OneDrive Server Transfer for continuity, but the application must not imply source deletion.

## User workflow

1. Open the application.
2. Sign in with the authorized IT transfer account.
3. Enter the employee UPN or OneDrive root URL.
4. Select the local destination.
5. Press `Scan` for the mandatory dry run.
6. Review the resolved employee, operator, destination, counts, known size, unsupported items, path warnings, and storage warnings.
7. Press `Start Copy` only after the scan succeeds.
8. Monitor progress and review the final result and reports.

Changing the source or destination invalidates the previous scan and disables `Start Copy`.

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

- Interactive delegated MSAL for the authorized IT operator only.
- Single configured tenant.
- Public-client application with no client secret.
- Read permissions only.
- Validate the configured tenant after sign-in.
- Enforce the configured authorized transfer-account Entra object-ID allowlist when present.
- Do not authorize by display name or mutable email address alone.
- Never request, collect, store, log, transmit, or process an employee password.
- Never authenticate as the employee.

## Source rules

- Accept one employee UPN or one employee OneDrive for Business root URL.
- Resolve the final source to Tenant ID, employee Entra object ID, and source Drive ID.
- Treat normalized employee UPN as a display and audit value, not the sole durable identity.
- Require `driveType = business` and the actual drive root.
- Reject unknown or external users, unprovisioned OneDrive, consumer OneDrive, files, subfolders, shared sources, SharePoint libraries, Teams libraries, and external tenants.
- Do not traverse external `remoteItem` or shortcut content belonging to another drive.
- Copy supported active file items, nested folders, and empty folders.
- Classify OneNote notebooks and other Graph package items as `Unsupported`, report them, and mark the archive `Incomplete`.
- Exclude Recycle Bin, previous versions, sharing metadata, comments, activity, compliance, and audit records.

## Mandatory dry run

- A successful `Scan` is required before `Start Copy` is enabled.
- Scan uses Microsoft Graph delta page by page.
- Scan does not download employee file content.
- Scan resolves source identity, inventories items, applies path mapping, calculates counts and known bytes, identifies unsupported items and warnings, validates destination binding and lock availability, and checks storage reserve.
- Scan output is preflight information and is not evidence that content was copied.

## Destination rules

- Administrator selects local fixed or directly attached storage on the same Windows Server.
- Reject UNC, mapped drives, NAS, SMB, remote storage, Windows system directories, application installation directories, and unsafe redirection.
- Create `OneDriveData` and `_TransferReport`.
- Bind destination to Tenant ID, employee Entra object ID, and source Drive ID.
- Record operator identity for audit without permanently binding the archive to one operator.
- Permit another authorized operator to resume only after all source, destination, state, and authorization checks succeed.
- Reject a destination associated with another source.
- Lock destination across processes and Windows sessions.
- Require known remaining bytes plus a fixed 5 GiB free-space reserve.
- Recheck capacity when totals change and before individual files when required.
- Disk-full or reserve failure cannot return `Completed` or `CompletedWithWarnings`.

## Inventory, copy, and recovery

- Use Microsoft Graph drive delta for dry-run inventory and reconciliation.
- Persist the delta checkpoint.
- Keep pages, queues, hashing, and memory bounded.
- Fixed maximum of three simultaneous downloads.
- Use streaming and `.partial` files.
- Resume only with valid HTTP Range responses.
- Respect `Retry-After` and retry transient failures up to five attempts per file.
- Never send Graph credentials to temporary download hosts.
- Use local SQLite state at `_TransferReport/TransferState.db`.
- Keep SQLite as the operational source for scan, resume, recovery, and source binding.
- Keep CSV and JSON as audit reports only.
- Validate SQLite integrity before resume.
- Create a protected backup before schema migration and migrate transactionally.
- Never silently reset or adopt content when state is corrupt or migration fails.
- Recovery must be transactional and idempotent.
- Preserve Drive Item identity through source rename and move.
- Never delete retained local archive content solely because the source item was deleted.

## Integrity, timestamps, and path safety

- Separate supported Microsoft source-hash verification from local SHA-256.
- Calculate local SHA-256 for every completed file.
- Preserve source creation and modification timestamps where Windows supports the values.
- Timestamp failure is a non-content warning without invalidating verified bytes.
- Never overwrite unrelated local content.
- Use the exact deterministic `PathMappingVersion = 1` rules in the binding contract.
- Expand the deterministic suffix from 10 to 20 characters and then the complete SHA-256 value when a residual collision remains.
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
Incomplete
Failed
Cancelled
Interrupted
```

- `Completed` requires a complete stable archive with no warning.
- `CompletedWithWarnings` requires every supported item copied or validly skipped and only non-content warnings.
- `Incomplete` means supported content failed, unsupported content exists, or the source did not stabilize.
- Store each run under `_TransferReport/Runs/<RunId>`.
- Implement `docs/REPORT_SCHEMA.md`.
- Never overwrite or append to another run's reports.

## Production requirements

- Mandatory Windows CI restore, Release build, and automated tests before `Source Implementation Complete`.
- Restricted NTFS permissions for archive data and token cache.
- BitLocker, approved equivalent, or documented approved exception for production storage.
- Temporary Site Collection Administrator access must be removed, verified, and externally recorded after it is no longer required.
- Production Ready requires real Windows Server, authorized Microsoft sign-in, UPN and URL source resolution, mandatory dry run, employee OneDrive copy, correct incomplete reporting, resume, reconciliation, locking, disk-space, timestamp, report, security, and publish evidence.

## Out of scope

No dashboards, scheduling, batch employee processing, user management, service mode, central reporting, email notifications, remote destinations, employee-password collection, employee impersonation, OneNote/package export, custom disk-index engine, JSONL state engine, or five-million-item release benchmark.

## Values not yet provided

- tenant name and domain
- Tenant ID
- Entra Client ID
- allowed OneDrive host
- authorized transfer-account Entra object ID or IDs
- dedicated transfer administrator email
- test employee Entra object ID
- test employee UPN and OneDrive root URL
- Windows Server name, build, and execution account
- local production destination
- proxy status
- production NTFS and BitLocker status
- Authenticode certificate availability
