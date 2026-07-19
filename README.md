# OneDrive Server Transfer

A simple internal Windows desktop application for copying one employee's Microsoft 365 OneDrive to local storage attached to the same Windows Server on which the application runs.

## Current status

**Documentation correction in progress — application implementation has not started.**

The repository currently contains the binding implementation contract and project-control documents. It does not yet contain the application source.

## Intended IT workflow

1. Open the application on the Windows Server.
2. Sign in with the approved Microsoft administrator account.
3. Paste the employee's OneDrive for Business root URL.
4. Select the local destination folder.
5. Press `Copy Data`.
6. Monitor progress and review the final report.

The application copies the active files and folders from that employee's OneDrive to the selected local destination. It does not modify Microsoft 365 data.

## Destination structure

```text
SelectedDestination\
├── OneDriveData\
└── _TransferReport\
```

- `OneDriveData` contains the copied employee files and folders.
- `_TransferReport` contains the local transfer-state database, reports, and logs.

The destination is bound to one tenant, employee, and OneDrive drive. A destination belonging to another source must be rejected to prevent data mixing.

## Supported source

- one employee OneDrive for Business root
- same configured Microsoft 365 tenant
- active files and folders
- nested and empty folders
- Arabic, English, Unicode, and large files

## Not included

- Recycle Bin
- previous versions
- sharing permissions or links
- comments or activity history
- compliance or audit records
- SharePoint or Teams libraries
- consumer OneDrive
- external shortcuts to another drive
- multiple employees in one run
- scheduling, dashboard, email notifications, or service mode
- network, UNC, NAS, SMB, or remote destinations

## Technology

- C#
- .NET 10 LTS
- WPF and MVVM
- Microsoft Graph v1.0
- MSAL interactive authentication
- local SQLite transfer state
- self-contained `win-x64` publish

## Security model

- read-only Microsoft Graph access
- no Microsoft password fields
- no client secret
- no Microsoft 365 write permissions
- DPAPI-protected application token cache
- temporary download URLs are never logged or stored
- Graph bearer tokens are never sent to temporary download hosts
- only local attached storage is accepted
- destination containment and source binding are validated

## Binding source of truth

`IMPLEMENTATION_CONTRACT.md` is the binding project contract.

`IMPLEMENTATION_CONTRACT_AMENDMENTS.md` is retained only as a superseded historical file; its approved corrections are incorporated into the main contract.

## Implementation phases

- M0 — Contract simplification and correction
- M1 — Solution and CI foundation
- M2 — Microsoft authentication
- M3 — Employee OneDrive validation
- M4 — Local destination and source binding
- M5 — Copy, resume, verification, and local state
- M6 — UI, errors, and reports
- M7 — Windows and real-tenant acceptance
- M8 — Internal release

## Required production inputs

Real tenant, Entra application, test employee, and Windows Server values are not committed. Complete `docs/ENVIRONMENT_AND_INPUTS.md` before real-tenant validation.

Never commit passwords, tokens, client secrets, employee content, production state databases, temporary download URLs, or unredacted production reports.