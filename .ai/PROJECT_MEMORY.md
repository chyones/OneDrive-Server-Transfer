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
- Completed phase: `M0 — Contract simplification and correction`
- M0 evidence: `artifacts/evidence/M00_contract-correction_20260719T110925Z.json`
- Next phase: `M1 — Solution and CI foundation`

## Binding authority

- Current explicit repository-owner instructions have highest project authority.
- `IMPLEMENTATION_CONTRACT.md` is the single binding repository contract.
- `IMPLEMENTATION_CONTRACT_AMENDMENTS.md` is superseded and retained only for history.
- Repository root is the project root.
- Required solution path: `./OneDriveServerTransfer.sln`.

## Product purpose and workflow

Copy the active files and folders from one employee's Microsoft 365 OneDrive for Business root to a local destination selected by the IT administrator on the same Windows Server.

Workflow:

1. Open the application.
2. Sign in with Microsoft.
3. Paste the employee OneDrive root URL.
4. Select the local destination.
5. Confirm the resolved employee and destination.
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

## Source rules

- Accept one employee OneDrive for Business root in the configured tenant.
- Require `driveType = business` and the actual drive root.
- Reject consumer OneDrive, files, subfolders, shared-folder sources, SharePoint libraries, Teams libraries, and external tenants.
- Do not traverse external `remoteItem` or shortcut content belonging to another drive.
- Copy active files, nested folders, and empty folders.
- Exclude Recycle Bin, previous versions, sharing metadata, comments, activity, compliance, and audit records.

## Destination rules

- Administrator selects local attached storage on the same Windows Server.
- Reject UNC, mapped drives, NAS, SMB, remote storage, and unsafe redirection.
- Create `OneDriveData` and `_TransferReport`.
- Bind destination to Tenant ID, source Drive ID, and protected employee identity.
- Reject a destination associated with another source.
- Lock destination across processes and Windows sessions.

## Inventory, transfer, and recovery

- Use Microsoft Graph drive delta for initial inventory and reconciliation.
- Persist the delta checkpoint.
- Keep pages, queues, and memory bounded.
- Fixed maximum of three simultaneous downloads.
- Use streaming and `.partial` files.
- Resume only with valid HTTP Range responses.
- Never send Graph credentials to temporary download hosts.
- Retry transient failures up to five attempts per file.
- Use local SQLite state at `_TransferReport/TransferState.db`.
- Recovery must be transactional and idempotent.

## Integrity and path safety

- Separate supported Microsoft source-hash verification from local SHA-256.
- Calculate local SHA-256 for every completed file.
- Never overwrite unrelated local content.
- Use deterministic `PathMappingVersion = 1`.
- Prevent traversal, unsafe reparse-point redirection, and untrusted hard-link overwrite behavior.

## Production requirements

- Mandatory Windows CI restore, Release build, and automated tests before `Source Implementation Complete`.
- Restricted NTFS permissions for backup data and token cache.
- BitLocker, approved equivalent, or documented approved exception for production storage.
- Temporary Site Collection Administrator access must be removed, verified, and externally recorded after it is no longer required.
- Production Ready requires real Windows Server, Microsoft sign-in, employee OneDrive copy, resume, reconciliation, locking, security, and publish evidence.

## Out of scope

No dashboards, scheduling, batch employee processing, user management, service mode, central reporting, email notifications, or remote destinations.

## Values not yet provided

- tenant name and domain
- Tenant ID
- Entra Client ID
- allowed OneDrive host
- dedicated transfer administrator email
- test employee identity and OneDrive root URL
- Windows Server name, build, and execution account
- local production destination
- proxy status
- production NTFS and BitLocker status
- Authenticode certificate availability