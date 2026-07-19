# Implementation Plan

This plan implements `IMPLEMENTATION_CONTRACT.md` without expanding product scope. Current phase status and evidence are maintained only in `.ai/PHASE_STATUS.md`.

Work on one phase at a time. Mark it `IN_PROGRESS` before source changes and do not begin the next phase until exit criteria and committed evidence are complete.

## M0 — Documentation and controls

**Status:** `DOCUMENTATION_COMPLETE`

Outcome: one binding contract, aligned agent controls, Microsoft platform policies, acceptance criteria, evidence policy, security requirements, report schema, and environment checklist. Historical drafts and evidence are not active instructions.

## M1 — Solution and CI foundation

Goals:

- root `OneDriveServerTransfer.sln`;
- WPF application and automated-test projects;
- .NET 10 Windows targeting;
- MVVM, dependency injection, logging, configuration, and SQLite foundation;
- clear interfaces for authentication, Graph metadata, temporary downloads, retry ownership, hashing, storage, state, and reporting;
- deterministic dependency restore;
- Windows CI for Release build, tests, static analysis, vulnerabilities, secrets, Graph beta, and prohibited authentication paths.

Exit:

- real solution builds and tests on Windows CI;
- no fake production services or later-phase implementation;
- no prohibited credential, permission, or Graph beta path;
- committed M1 evidence.

## M2 — Microsoft authentication

Read: `docs/AUTHENTICATION_AND_TOKEN_POLICY.md` and `docs/MICROSOFT_PLATFORM_BASELINE.md`.

Goals:

- delegated MSAL sign-in for authorized IT operator;
- WAM preferred with system-browser fallback;
- MFA and Conditional Access support;
- tenant and optional operator object-ID allowlist validation;
- silent token renewal and DPAPI-protected application cache;
- truthful sign-out semantics;
- correct consent, `401`, and `403` handling.

Exit: authentication boundaries and tests pass on Windows CI, prohibited flows are absent, exact dependency versions and current Microsoft review are recorded, and M2 evidence is committed.

## M3 — Employee source resolution

Read: `docs/GRAPH_ENDPOINT_PERMISSION_MATRIX.md` and `docs/MICROSOFT_PLATFORM_BASELINE.md`.

Goals:

- accept employee UPN or OneDrive root URL;
- use approved Graph `v1.0` endpoints and delegated scopes only;
- resolve tenant, employee object ID, and business drive ID;
- reject invalid, unprovisioned, consumer, shared, file, subfolder, SharePoint, Teams, and external-tenant sources;
- classify package and unknown content semantics safely;
- generate protected request correlation data.

Exit: source-resolution and rejection matrices pass, endpoint/scope inventory matches the approved matrix, real-tenant behavior remains unclaimed until executed, and M3 evidence is committed.

## M4 — Local destination and source binding

Goals:

- local fixed or directly attached destinations only;
- create `OneDriveData` and `_TransferReport`;
- bind destination to tenant, employee object, and drive IDs;
- enforce exclusive locking and authorized resume;
- implement `PathMappingVersion = 1`;
- prevent traversal, unsafe reparse redirection, hard-link overwrite, and writes outside root;
- enforce NTFS checks, known remaining bytes, and fixed 5 GiB reserve.

Exit: binding, locking, path-safety, capacity, and unrelated-file tests pass, and M4 evidence is committed.

## M5 — Scan, copy, resume, verification, and state

Read:

- `docs/GRAPH_DELTA_AND_RECONCILIATION_POLICY.md`
- `docs/GRAPH_RESILIENCY_POLICY.md`
- `docs/DOWNLOAD_AND_INTEGRITY_POLICY.md`
- `docs/SECURITY_AND_INTEGRITY_REQUIREMENTS.md`

Goals:

- mandatory dry-run delta inventory before copy;
- opaque next/delta links, duplicate handling, checkpoint recovery, and supported `410 Gone` reconciliation;
- scan invalidation when inputs change;
- bounded queues, streaming, fixed concurrency of three, `.partial` files, and cancellation;
- separate unauthenticated temporary-download client;
- validated Range resume and safe restart from zero;
- one retry owner, `Retry-After`, bounded backoff, and accurate attempts;
- supported Microsoft source hashes separate from local SHA-256;
- timestamp preservation;
- transactional SQLite state, integrity checks, migration backup, corruption safety, idempotent recovery, and exact states;
- safe handling of source rename, move, deletion, and continued changes.

Exit: all scan, transfer, resume, delta, retry, credential-isolation, integrity, state, and recovery tests pass; incomplete content cannot be reported complete; M5 evidence is committed.

## M6 — UI, errors, and reports

Read: `docs/REPORT_SCHEMA.md`.

Goals:

- complete one-window UI;
- signed-in operator, source input, destination, `Scan`, `Start Copy`, `Cancel`, and `Open Report`;
- responsive progress and bounded activity;
- clear dry-run results and exact terminal state;
- simple reference-coded user errors and protected technical logs;
- unique `_TransferReport/Runs/<RunId>` reports;
- UTF-8, CSV escaping, formula-injection protection, redaction, and report-schema compliance.

Exit: UI, cancellation, reporting, redaction, stale-scan, and report-isolation tests pass, and M6 evidence is committed.

## M7 — Windows and real-tenant acceptance

Read all Microsoft platform and security documents and complete `docs/ENVIRONMENT_AND_INPUTS.md`.

Execute on the target environment:

- Windows Server build, WPF startup, WAM/browser sign-in, MFA/Conditional Access as applicable;
- deployed permission inventory;
- real UPN and URL resolution;
- complete dry run and copy;
- interruption/resume, reconciliation, delta reset, source changes, locking, disk-space/disk-full, throttling, temporary URL expiry, Range behavior, hashes, timestamps, reports, ACLs, encryption, and access-removal verification;
- self-contained `win-x64` publish using a supported .NET servicing patch.

Exit: every production-acceptance requirement passes with evidence and exact deployed versions are recorded.

## M8 — Internal release

Read: `docs/PATCHING_AND_RELEASE_LIFECYCLE.md`.

Goals:

- final operating instructions and configuration template;
- deliverable tied to exact source commit;
- supported bundled runtime;
- vulnerability and secret scans, SBOM where supported, artifact hashes, signing status, lifecycle status, upgrade, and rollback instructions;
- approved deliverable under `artifacts/win-x64`.

Exit: release package and evidence are complete. Mark `Production Ready` only after both M7 and M8 pass.
