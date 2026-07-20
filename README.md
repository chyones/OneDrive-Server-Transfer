# OneDrive Server Transfer

Internal Windows application for copying the supported active contents of one employee's Microsoft 365 OneDrive for Business to approved local storage on the same Windows Server.

The application copies only. It never modifies or deletes Microsoft 365 source content and never requests an employee password.

## Status

- Completion label: `Documentation Ready` (Source Implementation Complete requires M1–M6)
- Application implementation: M1 solution/CI foundation, M2 authentication, and M3 employee source resolution complete and merged (PR #12, `main` baseline `fa1b811`)
- Development state: paused after M3 integration
- Current phase: `M4 — Destination and source binding` (not started; requires a new explicit owner instruction)

The exact status and evidence pointer are maintained only in `.ai/PHASE_STATUS.md`.

## Operator workflow

1. Open the WPF application.
2. Sign in with the authorized IT transfer account.
3. Enter employee UPN or OneDrive root URL.
4. Select a local destination.
5. Run mandatory `Scan`.
6. Review employee, operator, counts, known size, unsupported items, path warnings, and storage warnings.
7. Run `Start Copy` after a successful current scan.
8. Monitor progress and review the result and reports.

Changing source or destination invalidates the scan.

## Output

```text
SelectedDestination\
├── OneDriveData\
└── _TransferReport\
    ├── TransferState.db
    └── Runs\<RunId>\
        ├── TransferSummary.json
        ├── TransferReport.csv
        ├── FailedFiles.csv
        └── TransferLog.log
```

SQLite is the operational source for scan, resume, recovery, binding, and mappings. CSV and JSON are audit outputs only.

## Version 1 boundary

Supported:

- one employee business OneDrive root;
- active supported files, nested folders, and empty folders;
- local fixed or directly attached storage;
- Unicode, Arabic, large files, long names, and deterministic Windows-safe path mapping;
- interruption, validated resume, reconciliation, integrity checks, timestamps, and isolated reports.

Not included:

- Recycle Bin or previous versions;
- sharing, comments, activity, compliance, or audit data;
- SharePoint or Teams libraries;
- consumer OneDrive or external shortcuts;
- OneNote/package export;
- multiple employees, scheduling, service mode, dashboards, notifications, or remote destinations.

## Fixed technology

- C# and .NET 10 LTS
- WPF and MVVM
- Microsoft Graph `v1.0`
- delegated interactive MSAL, WAM preferred with system-browser fallback
- embedded SQLite
- self-contained `win-x64` publish

Graph beta, application permissions, Microsoft 365 write permissions, ROPC, device-code flow, client secrets, certificates, and employee authentication are prohibited.

## Development

Prerequisites: the .NET SDK pinned in `global.json` (10.0.3xx band). Building and testing the Windows-targeted solution requires Windows; the mandatory gate is the Windows CI workflow.

```text
dotnet restore OneDriveServerTransfer.sln --locked-mode
dotnet build OneDriveServerTransfer.sln --configuration Release --no-restore
dotnet test OneDriveServerTransfer.sln --configuration Release --no-build
```

Dependency versions are pinned centrally in `Directory.Packages.props` and locked per project in `packages.lock.json`; restore runs in locked mode. Static analysis runs through .NET analyzers with warnings as errors in CI. The Windows CI workflow (`.github/workflows/windows-ci.yml`) additionally executes a dependency vulnerability review, the prohibited authentication/API check (`scripts/Test-ProhibitedContent.ps1`), and secret detection.


## Source of truth

- Binding requirements: `IMPLEMENTATION_CONTRACT.md`
- Agent process: `AGENTS.md`
- Current status: `.ai/PHASE_STATUS.md`
- Current task: `.ai/HANDOFF.md`
- Implementation phases: `docs/IMPLEMENTATION_PLAN.md`
- Acceptance: `docs/ACCEPTANCE_MATRIX.md`
- Environment inputs: `docs/ENVIRONMENT_AND_INPUTS.md`

Implementation agents should begin with `.ai/START_HERE.md` and must not use deleted files, old pull requests, or Git history as active instructions.

Never commit passwords, tokens, secrets, employee content, temporary download URLs, production databases, or unredacted production reports.
