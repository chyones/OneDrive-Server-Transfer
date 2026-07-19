# AI Handoff

## Current position

The contract and project-control correction is complete. Application implementation has not started.

## Completion label

`Documentation Ready`

## Completed phase

`M0 — Contract simplification and correction`

Status: `DOCUMENTATION_COMPLETE`

Evidence:

```text
artifacts/evidence/M00_contract-correction_20260719T110925Z.json
```

Validated source commit:

```text
9a40d6bc9ddf036b61cca3a2432254ccd33f2051
```

## Approved product boundary

The IT administrator opens one WPF window, signs in with Microsoft, pastes one employee OneDrive for Business root URL, selects a local destination on the same Windows Server, presses `Copy Data`, monitors progress, and reviews the result.

The application remains read-only against Microsoft 365. It uses Graph delta for inventory and reconciliation, local SQLite state for resume and recovery, and a destination bound to one tenant, employee, and drive.

## Next exact action

Begin `M1 — Solution and CI foundation`:

1. Mark M1 `IN_PROGRESS` in `.ai/PHASE_STATUS.md`.
2. Create `./OneDriveServerTransfer.sln` at repository root.
3. Create the WPF application and automated-test projects.
4. Configure .NET 10 Windows targeting, MVVM, dependency injection, structured logging, and configuration.
5. Add the SQLite dependency and schema foundation without fake transfer services.
6. Add deterministic dependency restore.
7. Add enforceable Windows GitHub Actions for restore, Release build, tests, static checks, vulnerability review, and secret detection.
8. Execute all M1 checks and commit a redacted M1 evidence summary before starting M2.

## Known missing real-world inputs

See `docs/ENVIRONMENT_AND_INPUTS.md`.

Do not assume Tenant ID, Client ID, administrator access, production destination, Windows build, WPF startup, Microsoft sign-in, or OneDrive copy has succeeded.