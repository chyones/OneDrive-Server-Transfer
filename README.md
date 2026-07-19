# OneDrive Server Transfer

An internal IT-controlled Windows archival tool used to copy one employee's supported Microsoft 365 OneDrive for Business files and folders to approved local storage attached to the same Windows Server.

The tool supports authorized operational backup, employee offboarding, and recovery preparation. It copies data only and never deletes or modifies the Microsoft 365 source.

## Important security notice

- The application must never request, collect, store, log, or process an employee password.
- The operator must authenticate through Microsoft Entra ID using an authorized IT transfer account.
- Microsoft 365 access is read-only.
- The tool must never delete, rename, move, edit, upload, or change permissions on source OneDrive content.
- Employee archive data and reports must be protected by restricted NTFS permissions and approved storage encryption controls.

## Current status

**Documentation Ready — application implementation has not started.**

Completed documentation phase:

```text
M0 — Contract simplification and pre-implementation hardening
Status: DOCUMENTATION_COMPLETE
```

Committed M0 evidence:

```text
artifacts/evidence/M00_workflow-alignment_20260719T124036Z.json
```

Validated documentation source commit:

```text
c93b38b7e41ffbb50c82b4f8389e71ef511ac54d
```

Current implementation phase:

```text
M1 — Solution and CI foundation
Status: NOT_STARTED
Start authorization: Granted
```

M1 may begin now. The implementation agent must mark M1 `IN_PROGRESS` before creating source files. Documentation Ready does not mean the application builds, runs, signs in, accesses OneDrive, copies data, publishes, or is production ready.

## Intended IT workflow

1. Open the application on the Windows Server.
2. Sign in with the approved Microsoft transfer account.
3. Enter the employee UPN or paste the employee OneDrive for Business root URL.
4. Select a local destination folder.
5. Press `Scan` to perform the mandatory dry run.
6. Review the resolved employee, signed-in operator, destination, file count, known total size, unsupported items, path warnings, and storage warnings.
7. Press `Start Copy` after the scan succeeds.
8. Monitor progress and review the final result and reports.

Changing the source or destination invalidates the scan and disables `Start Copy` until another scan succeeds.

## Destination structure

```text
SelectedDestination\
├── OneDriveData\
└── _TransferReport\
    ├── TransferState.db
    └── Runs\
        └── <RunId>\
            ├── TransferSummary.json
            ├── TransferReport.csv
            ├── FailedFiles.csv
            └── TransferLog.log
```

- `OneDriveData` contains copied employee files and folders.
- `_TransferReport` contains SQLite operational state and isolated per-run reports.
- SQLite is the source of truth for scan, resume, and recovery.
- CSV and JSON files are audit reports only.
- The destination is bound to one tenant, employee Entra object ID, and source drive to prevent data mixing.
- Another authorized IT operator may resume only after all source, destination, authorization, and state checks succeed.
- Earlier reports are never overwritten by a later run.

## Supported source input

The source field accepts one of:

- an employee Microsoft Entra UPN, such as `employee@company.com`; or
- the root URL of that employee's OneDrive for Business.

The application resolves the final source to the configured tenant, employee Entra object ID, and default business OneDrive drive. It rejects consumer OneDrive, files, subfolders, shared links, SharePoint libraries, Teams libraries, and external tenants.

## Mandatory dry run

`Scan` does not download employee file content. It must:

- resolve the employee and source drive;
- inventory the complete drive through Microsoft Graph delta paging;
- classify supported and unsupported items;
- calculate file count and known total size;
- apply deterministic Windows-safe path mapping;
- identify collisions, invalid paths, and long-path failures;
- validate the destination, locking, binding, write access, and storage reserve; and
- present an accurate preflight summary.

`Start Copy` remains disabled when the scan fails or becomes stale.

## Supported content

- one employee OneDrive for Business root;
- same configured Microsoft 365 tenant;
- active supported file and folder items;
- nested and empty folders; and
- Arabic, English, Unicode, large, long-name, and long-path files within the documented Windows mapping rules.

## Explicit unsupported content

Microsoft Graph package items, including OneNote notebooks, are not copied in version 1. They are reported as `Unsupported` and make the final result `Incomplete`. They are never silently skipped or represented as copied.

The first release does not include:

- Recycle Bin or previous versions;
- sharing permissions, links, comments, activity, compliance, or audit records;
- SharePoint or Teams libraries;
- consumer OneDrive;
- external shortcuts to another drive;
- OneNote or other package export;
- multiple employees in one run;
- scheduling, dashboards, email notifications, or service mode; or
- network, UNC, NAS, SMB, or remote destinations.

## Technology

- C# and .NET 10 LTS;
- WPF and MVVM;
- Microsoft Graph v1.0;
- MSAL interactive authentication;
- local SQLite transfer state; and
- self-contained `win-x64` publish.

## Core archive behavior

- Microsoft Graph delta inventory and reconciliation;
- mandatory scan before copy;
- bounded memory and fixed maximum of three simultaneous downloads;
- streaming downloads and `.partial` files;
- safe HTTP Range resume;
- `Retry-After` handling and bounded retry;
- local SHA-256 and supported Microsoft source-hash verification kept separate;
- source timestamp preservation with explicit warnings;
- fixed 5 GiB destination-space reserve;
- deterministic `PathMappingVersion = 1` with collision-suffix expansion;
- SQLite integrity validation and safe migration recovery; and
- exact run states: `InProgress`, `Completed`, `CompletedWithWarnings`, `Incomplete`, `Failed`, `Cancelled`, and `Interrupted`.

`Incomplete` means the archive is missing supported content, contains unsupported source items, or could not reach a stable source snapshot. It must never be presented as a successful complete archive.

## Security model

- authorized IT users only;
- no employee-password fields or employee impersonation;
- read-only Microsoft Graph access;
- configured-tenant validation;
- authorized transfer-account Entra object-ID allowlist when configured;
- no Microsoft password storage and no client secret;
- no Microsoft 365 write permissions;
- DPAPI-protected application token cache;
- temporary download URLs are never logged or stored;
- Graph credentials are never sent to temporary download hosts;
- local attached storage only;
- destination containment and source binding;
- restricted NTFS permissions;
- BitLocker, approved equivalent, or approved documented exception for production storage; and
- removal and verification of temporary Site Collection Administrator access after it is no longer required.

See `SECURITY.md`, `docs/SECURITY_AND_INTEGRITY_REQUIREMENTS.md`, and `docs/REPORT_SCHEMA.md`.

## Binding source of truth

`IMPLEMENTATION_CONTRACT.md` is the single binding project contract.

`IMPLEMENTATION_CONTRACT_AMENDMENTS.md` is superseded historical material and does not override the binding contract.

The custom disk-index engine, JSONL state engine, and five-million-item release benchmark are not first-release requirements.

## Implementation phases

- M0 — Contract simplification and pre-implementation hardening: `DOCUMENTATION_COMPLETE`
- M1 — Solution and CI foundation: `NOT_STARTED`, authorized to start
- M2 — Microsoft authentication
- M3 — Employee source resolution and validation
- M4 — Local destination and source binding
- M5 — Scan, copy, resume, verification, and local state
- M6 — UI, errors, and reports
- M7 — Windows and real-tenant acceptance
- M8 — Internal release

Real tenant, Entra application, authorized transfer account, test employee, and Windows Server values are not committed. Complete `docs/ENVIRONMENT_AND_INPUTS.md` before real-tenant validation.

Never commit passwords, tokens, client secrets, employee content, production state databases, temporary download URLs, or unredacted production reports.
