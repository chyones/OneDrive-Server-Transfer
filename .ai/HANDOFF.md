# AI Handoff

## Current position

The workflow-alignment documentation changes are implemented on the current branch. Application implementation has not started.

The branch changes the source input to employee UPN or OneDrive root URL, requires a mandatory scan before copy, prohibits employee passwords and employee impersonation, adds a report schema, introduces `Incomplete` for missing content, defines durable employee identity, and closes the stale `.ai/START_HERE.md` evidence reference.

## Current completion label

`Not Complete` on this branch until the documentation change is reviewed, merged, and replacement M0 evidence is committed.

## Current phase

`M0 — Contract simplification and pre-implementation hardening`

Status: `IN_PROGRESS`

Previous validated main baseline:

```text
e9434ff54c373e1d0129ba2583027897f6f3ff25
```

Previous evidence:

```text
artifacts/evidence/M00_preimplementation-hardening_20260719T113850Z.json
```

The previous evidence remains historical proof for the previous main baseline. It does not validate the workflow-alignment changes on this branch.

## Approved product boundary on this branch

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
- `docs/REPORT_SCHEMA.md`; and
- isolated `_TransferReport/Runs/<RunId>` reports.

## Next exact action

1. Review the documentation branch for contradictions and stale controls.
2. Confirm no source, test, WPF, authentication, Graph, transfer, or production code was added.
3. Merge the documentation pull request only after review.
4. Create replacement M0 evidence tied to the exact merged workflow-alignment commit.
5. Update `.ai/PHASE_STATUS.md`, `.ai/PROJECT_MEMORY.md`, `.ai/HANDOFF.md`, README, and environment readiness to the new validated baseline.
6. Begin `M1 — Solution and CI foundation` only after those steps complete.

## M1 prohibitions

- Do not start M1 while this branch remains unreviewed or replacement M0 evidence is absent.
- Do not implement M2 authentication or later source, scan, or copy behavior during M1.
- Do not create fake successful services or placeholder production behavior.
- Do not add an employee-password path or employee impersonation.
- Do not revive the superseded custom disk index, JSONL engine, or five-million-item benchmark.
- Do not add dashboards, scheduling, batch employee processing, service mode, remote destinations, central reporting, or email notifications.
- Do not weaken any binding later-phase security, integrity, state, path, report, or evidence rule.

## Known missing real-world inputs

See `docs/ENVIRONMENT_AND_INPUTS.md`.

Do not assume Tenant ID, Client ID, authorized transfer account, employee identity, administrator access, production destination, Windows build, WPF startup, Microsoft sign-in, source resolution, dry run, OneDrive copy, resume, publish, or production validation has succeeded.
