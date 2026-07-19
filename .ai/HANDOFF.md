# AI Handoff

## Current position

M0 documentation alignment is complete and validated. Application implementation has not started.

The validated documentation baseline accepts employee UPN or OneDrive root URL, requires a mandatory scan before copy, prohibits employee passwords and employee impersonation, defines the report schema, introduces `Incomplete` for missing content, defines durable employee identity, and aligns all active startup and control files.

## Current completion label

`Documentation Ready`

This label applies only to the repository contract and controls. It does not claim source implementation, Windows validation, Microsoft sign-in, OneDrive access, copy execution, publish, or production readiness.

## Completed phase

`M0 — Contract simplification and pre-implementation hardening`

Status: `DOCUMENTATION_COMPLETE`

Validated source commit:

```text
c93b38b7e41ffbb50c82b4f8389e71ef511ac54d
```

Committed evidence:

```text
artifacts/evidence/M00_workflow-alignment_20260719T124036Z.json
```

The evidence is documentation-only and records all unexecuted source, Windows, tenant, transfer, publish, and production checks.

## Approved product boundary

The authorized IT operator opens one WPF window, signs in with Microsoft, enters one employee UPN or OneDrive for Business root URL, selects a local destination on the same Windows Server, runs a mandatory `Scan`, confirms the resolved employee, operator, destination, counts, known size, unsupported items, path warnings, and storage warnings, presses `Start Copy`, monitors progress, and reviews the result and reports.

The application remains read-only against Microsoft 365. It never requests or processes an employee password and never authenticates as the employee.

Binding later-phase rules include:

- configured-tenant and authorized transfer-account validation;
- durable source identity using Tenant ID, employee Entra object ID, and source Drive ID;
- UPN-or-URL source resolution;
- mandatory Graph delta dry-run inventory before copy;
- scan invalidation when source or destination changes;
- unsupported reporting for OneNote and other package items;
- `Incomplete` when supported content fails, unsupported content exists, or the source does not stabilize;
- fixed maximum of three downloads;
- `Retry-After` handling and bounded retry;
- fixed 5 GiB destination-space reserve;
- deterministic `PathMappingVersion = 1` with residual-collision suffix expansion;
- streaming, `.partial` files, safe Range resume, source revalidation, and local SHA-256;
- source timestamp preservation;
- SQLite operational state, integrity checks, migration backup, and safe corruption failure;
- exact item and run states;
- protected Graph identifiers excluded from normal UI and user-facing errors;
- `docs/REPORT_SCHEMA.md`; and
- isolated `_TransferReport/Runs/<RunId>` reports.

## Current phase

`M1 — Solution and CI foundation`

Status: `NOT_STARTED`

Start authorization: Granted.

## Next exact action

1. Read `AGENTS.md`, `IMPLEMENTATION_CONTRACT.md`, `.ai/START_HERE.md`, and every required control document.
2. Confirm `.ai/PHASE_STATUS.md` points to the committed M0 evidence and validated commit above.
3. Change M1 status to `IN_PROGRESS` before creating source files.
4. Implement M1 only:
   - create `OneDriveServerTransfer.sln` at repository root;
   - create the WPF application and automated-test projects;
   - configure .NET 10 Windows targeting;
   - add MVVM, dependency injection, structured logging, and configuration foundations;
   - add SQLite dependency and schema foundation;
   - add deterministic restore; and
   - add mandatory Windows GitHub Actions.
5. Execute the M1 validation required by the contract and commit M1 evidence tied to the exact validated source commit.

## M1 prohibitions

- Do not implement M2 authentication or later source, scan, or copy behavior during M1.
- Do not create fake successful services or placeholder production behavior.
- Do not add an employee-password path or employee impersonation.
- Do not revive the superseded custom disk index, JSONL engine, or five-million-item benchmark.
- Do not add dashboards, scheduling, batch employee processing, service mode, remote destinations, central reporting, or email notifications.
- Do not weaken any binding later-phase security, integrity, state, path, report, or evidence rule.

## Known missing real-world inputs

See `docs/ENVIRONMENT_AND_INPUTS.md`.

Do not assume Tenant ID, Client ID, authorized transfer account, employee identity, administrator access, production destination, Windows build, WPF startup, Microsoft sign-in, source resolution, dry run, OneDrive copy, resume, publish, or production validation has succeeded.
