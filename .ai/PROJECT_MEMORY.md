# Project Memory

This file contains durable project facts for future AI sessions. Do not place transient logs, secrets, or speculative ideas here.

## Identity

- Project: OneDrive Server Transfer
- Repository: `chyones/OneDrive-Server-Transfer`
- Product type: Native Windows desktop application
- Primary operator: IT employee
- Runtime target: Windows Server 2019 x64 with Desktop Experience
- UI language: English
- Current state: Documentation Ready; implementation not started
- M0 state: `DOCUMENTATION_COMPLETE`

## Binding authority

- `IMPLEMENTATION_CONTRACT_AMENDMENTS.md` has highest repository-contract authority.
- `IMPLEMENTATION_CONTRACT.md` remains binding except where explicitly amended.
- The repository root is the project root.
- Required solution path: `./OneDriveServerTransfer.sln`.
- Do not create a nested `./OneDriveServerTransfer` project container.

## Fixed product purpose

Create a complete local file-and-folder backup of the active in-scope content of one employee's Microsoft 365 OneDrive.

The administrator manually receives Site Collection Administrator access outside the application. The application validates the employee OneDrive root and copies data to local storage attached to the same Windows Server.

## Fixed technology

- C#
- .NET 10 LTS
- WPF
- MVVM
- Microsoft Graph v1.0
- MSAL
- Dependency injection
- Structured logging
- Automated tests

## Fixed security decisions

- Read-only against Microsoft 365
- Delegated interactive sign-in
- No client secret
- No password storage
- No Graph beta
- No SharePoint REST or CSOM fallback
- Temporary download URLs receive no Graph bearer token
- Secrets and employee content never enter GitHub
- Local SHA-256 is stored for every completed file
- Destination containment is revalidated during file operations
- Production destination and reports require a restricted NTFS ACL baseline
- Production storage requires BitLocker or an approved documented equivalent or exception
- Site Collection Administrator access is removed and externally recorded after it is no longer required

## Fixed source rules

- Accept employee personal OneDrive site root URL
- Resolve default Graph drive root
- Require OneDrive for Business `driveType = business`
- Reject consumer `personal`
- Reject SharePoint/Teams `documentLibrary`
- Reject file, subfolder, shared-folder, and external-tenant sources
- Do not traverse external `remoteItem` content

## Fixed destination rules

- Local attached storage only
- Reject UNC, mapped drives, NAS, SMB, remote storage, and unsafe reparse points
- Destination structure:
  - `OneDriveData`
  - `_TransferReport`
- Lock destination across processes and Windows sessions
- Prevent reparse-point time-of-check/time-of-use redirection throughout transfer operations

## Fixed transfer and integrity rules

- Bounded page-by-page enumeration
- Fixed maximum of three simultaneous file downloads
- Streaming downloads
- `.partial` files
- Byte-range resume where supported
- Post-download metadata revalidation
- Source hash verification when Graph provides a supported hash
- Streaming local SHA-256 for every completed file
- Three reconciliation passes maximum
- Backup behavior, not synchronization
- Never delete local backup solely because source changed or disappeared
- Retry attempts must remain between 1 and 5 inclusive

## Manifest architecture

- `ManifestVersion = 1`
- `PathMappingVersion = 1`
- A small list of JSONL segments is not sufficient as the five-million-item lookup index.
- Before M5, record an approved disk-based index design covering lookup keys, crash safety, recovery, complexity, and benchmark behavior.
- The no-database rule remains binding unless explicitly amended by the user.

## Evidence rules

- Raw generated source evidence: `artifacts/source`
- Generated Windows publish output: `artifacts/win-x64`
- Small redacted committed evidence summaries: `artifacts/evidence`
- A phase-status claim without its required committed evidence summary is not valid completion evidence.

## Scale target

- At least 5,000,000 files and folders
- Several terabytes
- Multi-day runs
- Peak managed heap below 1 GiB under the default compatible-Windows benchmark configuration
- Production components must be used by the synthetic benchmark

## Completion truth

- macOS can support source preparation only.
- Production Ready requires actual Windows, security, supply-chain, and real-tenant validation.
- Current label: `Documentation Ready`.

## Values not yet provided

- Tenant name
- Tenant domain
- Tenant ID
- Client ID
- Allowed OneDrive host
- Administrator email
- Test employee email and OneDrive root URL
- Windows Server name/build/account
- Local destination root
- Proxy status
- Production ACL baseline confirmation
- Production volume-encryption status
- Authenticode certificate availability