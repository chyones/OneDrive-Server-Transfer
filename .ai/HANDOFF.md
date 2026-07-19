# AI Handoff

## Current position

The binding contract and all pre-implementation controls are aligned. Application implementation has not started.

## Completion label

`Documentation Ready`

## Completed phase

`M0 — Contract simplification and pre-implementation hardening`

Status: `DOCUMENTATION_COMPLETE`

Evidence:

```text
artifacts/evidence/M00_preimplementation-hardening_20260719T113850Z.json
```

Validated source commit:

```text
e9434ff54c373e1d0129ba2583027897f6f3ff25
```

## Approved product boundary

The IT administrator opens one WPF window, signs in with Microsoft, pastes one employee OneDrive for Business root URL, selects a local destination on the same Windows Server, confirms the resolved employee, authorized transfer account, and destination, presses `Copy Data`, monitors progress, and reviews the result.

The application remains read-only against Microsoft 365. It uses Graph delta for inventory and reconciliation, local SQLite state for resume and recovery, and a destination bound to one tenant, employee, and drive.

Binding later-phase rules include:

- configured-tenant and authorized transfer-account validation;
- unsupported reporting for OneNote and other package items;
- fixed maximum of three downloads;
- fixed 5 GiB destination-space reserve;
- deterministic `PathMappingVersion = 1`;
- streaming, `.partial` files, safe Range resume, source revalidation, and local SHA-256;
- source timestamp preservation;
- SQLite integrity checks, migration backup, and safe corruption failure;
- exact item and run states; and
- isolated `_TransferReport/Runs/<RunId>` reports.

## Next exact action

Begin `M1 — Solution and CI foundation` only:

1. Mark M1 `IN_PROGRESS` in `.ai/PHASE_STATUS.md` and update this handoff with the exact M1 scope.
2. Create `./OneDriveServerTransfer.sln` at repository root.
3. Create the WPF application and automated-test projects.
4. Configure .NET 10 Windows targeting, MVVM, dependency injection, structured logging, and configuration.
5. Add the SQLite dependency and schema foundation without fake authentication, Graph, transfer, or production services.
6. Add deterministic dependency restore.
7. Add enforceable Windows GitHub Actions for restore, Release build, tests, static checks, vulnerability review, and secret detection.
8. Execute all M1 checks and commit a redacted M1 evidence summary before starting M2.

## M1 prohibitions

- Do not implement M2 authentication or later transfer behavior during M1.
- Do not create fake successful services or placeholder production behavior.
- Do not revive the superseded custom disk index, JSONL engine, or five-million-item benchmark.
- Do not add dashboards, scheduling, batch employee processing, service mode, remote destinations, central reporting, or email notifications.
- Do not weaken any binding later-phase security, integrity, state, path, report, or evidence rule.

## Known missing real-world inputs

See `docs/ENVIRONMENT_AND_INPUTS.md`.

Do not assume Tenant ID, Client ID, authorized transfer account, administrator access, production destination, Windows build, WPF startup, Microsoft sign-in, OneDrive copy, resume, publish, or production validation has succeeded.
