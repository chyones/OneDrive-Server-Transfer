# OneDrive Server Transfer

A simple internal Windows desktop application for copying one employee's supported Microsoft 365 OneDrive for Business files and folders to local storage attached to the same Windows Server on which the application runs.

## Current status

**Documentation Ready — application implementation has not started.**

Completed phase:

```text
M0 — Contract simplification and pre-implementation hardening
```

Evidence:

```text
artifacts/evidence/M00_preimplementation-hardening_20260719T113850Z.json
```

Validated documentation baseline:

```text
e9434ff54c373e1d0129ba2583027897f6f3ff25
```

Next phase:

```text
M1 — Solution and CI foundation
```

## Intended IT workflow

1. Open the application on the Windows Server.
2. Sign in with the approved Microsoft transfer account.
3. Paste the employee's OneDrive for Business root URL.
4. Select the local destination folder.
5. Confirm the resolved employee, authorized transfer account, and destination.
6. Press `Copy Data`.
7. Monitor progress and review the final report.

The application copies supported active files and folders to the selected local destination. It does not modify Microsoft 365 data.

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
- `_TransferReport` contains SQLite transfer state and isolated per-run reports.
- The destination is bound to one tenant, employee, and OneDrive drive to prevent data mixing.
- Earlier reports are never overwritten by a later run.

## Supported source

- one employee OneDrive for Business root;
- same configured Microsoft 365 tenant;
- active supported file and folder items;
- nested and empty folders; and
- Arabic, English, Unicode, large, long-name, and long-path files within the documented Windows mapping rules.

## Explicit unsupported content

Microsoft Graph package items, including OneNote notebooks, are not copied in version 1. They are reported as `Unsupported` and prevent a clean `Completed` result. They are never silently skipped or represented as copied.

## Not included

- Recycle Bin or previous versions;
- sharing permissions, links, comments, activity, compliance, or audit records;
- SharePoint or Teams libraries;
- consumer OneDrive;
- external shortcuts to another drive;
- OneNote or other package export;
- multiple employees in one run;
- scheduling, dashboards, email notifications, or service mode; and
- network, UNC, NAS, SMB, or remote destinations.

## Technology

- C# and .NET 10 LTS;
- WPF and MVVM;
- Microsoft Graph v1.0;
- MSAL interactive authentication;
- local SQLite transfer state; and
- self-contained `win-x64` publish.

## Core transfer behavior

- Microsoft Graph delta inventory and reconciliation;
- bounded memory and fixed maximum of three simultaneous downloads;
- streaming downloads and `.partial` files;
- safe HTTP Range resume;
- retry and throttling handling;
- local SHA-256 and supported Microsoft source-hash verification kept separate;
- source timestamp preservation with explicit warnings;
- fixed 5 GiB destination-space reserve;
- deterministic `PathMappingVersion = 1`;
- SQLite integrity validation and safe migration recovery; and
- exact run states: `InProgress`, `Completed`, `CompletedWithWarnings`, `Failed`, `Cancelled`, and `Interrupted`.

## Security model

- read-only Microsoft Graph access;
- configured-tenant validation;
- authorized transfer-account Entra object-ID allowlist when configured;
- no Microsoft password fields or client secret;
- no Microsoft 365 write permissions;
- DPAPI-protected application token cache;
- temporary download URLs are never logged or stored;
- Graph credentials are never sent to temporary download hosts;
- local attached storage only;
- destination containment and source binding;
- restricted NTFS permissions;
- BitLocker, approved equivalent, or approved documented exception for production storage; and
- removal and verification of temporary Site Collection Administrator access after it is no longer required.

## Binding source of truth

`IMPLEMENTATION_CONTRACT.md` is the single binding project contract.

`IMPLEMENTATION_CONTRACT_AMENDMENTS.md` is superseded historical material and does not override the binding contract.

The custom disk-index engine, JSONL state engine, and five-million-item release benchmark are not first-release requirements.

## Implementation phases

- M0 — Contract simplification and pre-implementation hardening: complete
- M1 — Solution and CI foundation
- M2 — Microsoft authentication
- M3 — Employee OneDrive validation
- M4 — Local destination and source binding
- M5 — Copy, resume, verification, and local state
- M6 — UI, errors, and reports
- M7 — Windows and real-tenant acceptance
- M8 — Internal release

Real tenant, Entra application, authorized transfer account, test employee, and Windows Server values are not committed. Complete `docs/ENVIRONMENT_AND_INPUTS.md` before real-tenant validation.

Never commit passwords, tokens, client secrets, employee content, production state databases, temporary download URLs, or unredacted production reports.
