# AI Handoff

## Current position

M0 documentation alignment and Microsoft platform hardening are complete and validated. Application implementation has not started.

The validated documentation baseline accepts employee UPN or OneDrive root URL, requires a mandatory scan before copy, prohibits employee passwords and employee impersonation, defines the report schema, introduces `Incomplete` for missing or unsupported content, defines durable employee identity, and aligns all active startup and control files.

It also defines the current Microsoft implementation baseline: Graph `v1.0` only, delegated interactive MSAL, WAM-preferred sign-in with system-browser fallback, an endpoint-permission matrix, opaque delta links and `410 Gone` recovery, one retry owner, temporary-download credential isolation, supported source hashes separate from local SHA-256, and self-contained runtime servicing.

## Current completion label

`Documentation Ready`

This label applies only to the repository contract and controls. It does not claim source implementation, Windows validation, Microsoft sign-in, OneDrive access, copy execution, publish, or production readiness.

## Completed phase

`M0 — Contract simplification and pre-implementation hardening`

Status: `DOCUMENTATION_COMPLETE`

Validated documentation source commit:

```text
50e25cc9501ef22ad05ebe6abc1e7a96603efce2
```

Committed evidence:

```text
artifacts/evidence/M00_microsoft-platform-baseline_20260719T172157Z.json
```

Previous workflow-alignment evidence remains historical:

```text
artifacts/evidence/M00_workflow-alignment_20260719T124036Z.json
```

The current evidence is documentation-only and records all unexecuted source, Windows, tenant, authentication, Graph, transfer, publish, and production checks.

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
- `Incomplete` when supported content fails, unsupported or unknown content semantics exist, or the source does not stabilize;
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

Microsoft platform rules additionally include:

- Microsoft Graph `v1.0` only;
- delegated-only version 1 authentication;
- WAM-preferred MSAL sign-in and supported system-browser fallback;
- no ROPC, device-code flow, client secrets, certificates, app-only access, write scopes, or Graph beta;
- exact endpoint and permission inventory;
- opaque next links and delta links;
- supported `410 Gone` fresh enumeration and reconciliation;
- exactly one automatic retry owner per HTTP request category;
- protected request correlation IDs;
- a separate unauthenticated client for temporary download hosts;
- no temporary download URL persistence or logging;
- Range resume accepted only with valid `206` and `Content-Range`;
- Microsoft Graph `sha256Hash` ignored;
- supported Microsoft source hashes stored separately from local SHA-256; and
- supported .NET patch and Windows lifecycle evidence for releases.

## Current phase

`M1 — Solution and CI foundation`

Status: `NOT_STARTED`

Start authorization: Granted.

## Next exact action

1. Read `AGENTS.md`, `IMPLEMENTATION_CONTRACT.md`, `.ai/START_HERE.md`, and every required control document.
2. Confirm `.ai/PHASE_STATUS.md` points to the committed current M0 evidence and validated documentation commit above.
3. Change M1 status to `IN_PROGRESS` before creating source files.
4. Implement M1 only:
   - create `OneDriveServerTransfer.sln` at repository root;
   - create the WPF application and automated-test projects;
   - configure .NET 10 Windows targeting;
   - add MVVM, dependency injection, structured logging, and configuration foundations;
   - add SQLite dependency and schema foundation;
   - establish separate interfaces for authentication, Graph metadata requests, temporary-host downloads, retry ownership, hashing, local storage, SQLite, and reports;
   - add deterministic restore; and
   - add mandatory Windows GitHub Actions, including beta-Graph, prohibited-auth-flow, vulnerability, and secret checks.
5. Execute the M1 validation required by the contract and commit M1 evidence tied to the exact validated source commit.

## M1 prohibitions

- Do not implement M2 authentication or later source, scan, or copy behavior during M1.
- Do not create fake successful services or placeholder production behavior.
- Do not add an employee-password path or employee impersonation.
- Do not add Graph beta, application permissions, Microsoft 365 write permissions, ROPC, device-code flow, client secrets, or certificates.
- Do not revive the superseded custom disk index, JSONL engine, or five-million-item benchmark.
- Do not add dashboards, scheduling, batch employee processing, service mode, remote destinations, central reporting, or email notifications.
- Do not weaken any binding later-phase security, integrity, state, path, report, platform, or evidence rule.

## Known missing real-world inputs

See `docs/ENVIRONMENT_AND_INPUTS.md`.

Do not assume Tenant ID, Client ID, authorized transfer account, employee identity, administrator access, production destination, Windows build, WPF startup, WAM or browser sign-in, Microsoft consent, source resolution, delta scan, OneDrive copy, retry, temporary URL, Range, hash, resume, publish, or production validation has succeeded.
