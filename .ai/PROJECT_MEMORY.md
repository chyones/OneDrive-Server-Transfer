# Project Memory

This file contains durable project facts. Do not place transient logs, secrets, or speculative ideas here.

## Identity

- Project: OneDrive Server Transfer
- Repository: `chyones/OneDrive-Server-Transfer`
- Product type: Internal Windows desktop application
- Primary operator: Authorized IT administrator
- Runtime target: Windows Server 2019 x64 with Desktop Experience
- UI language: English
- Current state: M0 contract correction in progress; application implementation not started

## Binding authority

- Current explicit repository-owner instructions have highest project authority.
- `IMPLEMENTATION_CONTRACT.md` is the single binding repository contract.
- `IMPLEMENTATION_CONTRACT_AMENDMENTS.md` is superseded and retained only for history.
- Repository root is the project root.
- Required solution path: `./OneDriveServerTransfer.sln`.

## Product purpose and workflow

Copy the active files and folders from one employee's Microsoft 365 OneDrive for Business root to a local destination selected by the IT administrator on the same Windows Server.

The user workflow is:

1. Open the application.
2. Sign in with Microsoft.
3. Paste the employee OneDrive root URL.
4. Select the local destination.
5. Press `Copy Data`.
6. Monitor progress and review the result.

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

- Administrator selects any local attached destination on the same Windows Server.
- Reject UNC, mapped drives, NAS, SMB, remote storage, and unsafe redirection.
- Create `OneDriveData` and `_TransferReport`.
- Bind destination to Tenant ID, source Drive ID, and protected employee identity.
- Reject a destination associated with another source.
- Lock destination across processes and Windows sessions.

## Inventory, transfer, and recovery

- Use Microsoft Graph drive delta for initial complete inventory and reconciliation.
- Persist the delta checkpoint.
- Process pages and queues with bounded memory.
- Fixed maximum of three simultaneous downloads.
- Use streaming and `.partial` files.
- Resume with valid HTTP Range responses and restart safely when Range is ignored or invalid.
- Never send Graph credentials to temporary download hosts.
- Retry transient failures up to five attempts per file.
- Use up to three bounded reconciliation passes and return warnings when the source does not stabilize.
- Store state in `_TransferReport/TransferState.db` using SQLite transactions.
- Recovery must be idempotent.

## Integrity and path safety

- Separate supported Microsoft source-hash verification from local SHA-256.
- Calculate local SHA-256 for every completed file.
- Never overwrite an unrelated local file.
- Use deterministic `PathMappingVersion = 1`.
- Prevent traversal, unsafe reparse-point redirection, and untrusted hard-link overwrite behavior.
- Validate containment throughout file operations.

## User interface

One WPF window only:

- Microsoft sign-in controls
- employee OneDrive URL
- destination selector
- `Copy Data` and `Cancel`
- simple progress counts and progress bar
- bounded recent activity
- simple reference-coded errors

No dashboards, scheduling, batch employee processing, user management, service mode, central reporting, email notifications, or remote destinations.

## Evidence and completion truth

- Former M0 evidence is superseded because it omitted an immutable validated source commit.
- M0 remains `IN_PROGRESS` until corrected documents are reviewed, merged, and evidenced against the exact merged commit.
- Cross-platform checks are not Windows validation.
- Source implementation is not Production Ready.
- Production Ready requires actual Windows Server, Microsoft sign-in, real OneDrive copy, resume, reconciliation, locking, security, and publish evidence.

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