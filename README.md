# OneDrive Server Transfer

A simple internal Windows desktop application for copying one employee's Microsoft 365 OneDrive to local storage attached to the same Windows Server on which the application runs.

## Current status

**Documentation Ready — application implementation has not started.**

Completed documentation phase:

```text
M0 — Contract simplification and correction
```

Evidence:

```text
artifacts/evidence/M00_contract-correction_20260719T110925Z.json
```

Next phase:

```text
M1 — Solution and CI foundation
```

## Intended IT workflow

1. Open the application on the Windows Server.
2. Sign in with the approved Microsoft administrator account.
3. Paste the employee's OneDrive for Business root URL.
4. Select the local destination folder.
5. Confirm the resolved employee and destination.
6. Press `Copy Data`.
7. Monitor progress and review the final report.

The application copies active files and folders to the selected local destination. It does not modify Microsoft 365 data.

## Destination structure

```text
SelectedDestination\
├── OneDriveData\
└── _TransferReport\
```

- `OneDriveData` contains copied employee files and folders.
- `_TransferReport` contains SQLite transfer state, reports, and logs.

The destination is bound to one tenant, employee, and OneDrive drive to prevent data mixing.

## Supported source

- one employee OneDrive for Business root;
- same configured Microsoft 365 tenant;
- active files and folders;
- nested and empty folders; and
- Arabic, English, Unicode, large, and long-name files.

## Not included

- Recycle Bin or previous versions;
- sharing permissions, links, comments, activity, compliance, or audit records;
- SharePoint or Teams libraries;
- consumer OneDrive;
- external shortcuts to another drive;
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

## Security model

- read-only Microsoft Graph access;
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

## Implementation phases

- M0 — Contract simplification and correction: complete
- M1 — Solution and CI foundation
- M2 — Microsoft authentication
- M3 — Employee OneDrive validation
- M4 — Local destination and source binding
- M5 — Copy, resume, verification, and local state
- M6 — UI, errors, and reports
- M7 — Windows and real-tenant acceptance
- M8 — Internal release

Real tenant, Entra application, test employee, and Windows Server values are not committed. Complete `docs/ENVIRONMENT_AND_INPUTS.md` before real-tenant validation.

Never commit passwords, tokens, client secrets, employee content, production state databases, temporary download URLs, or unredacted production reports.