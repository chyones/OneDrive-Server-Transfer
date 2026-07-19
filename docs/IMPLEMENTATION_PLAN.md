# Implementation Plan

This plan implements the simple workflow defined in `IMPLEMENTATION_CONTRACT.md`. It does not expand product scope.

## M0 — Contract simplification and pre-implementation hardening

**Status:** `IN_PROGRESS`

M0 remains in progress until this workflow-alignment change is reviewed and merged and replacement evidence is committed against the exact merged documentation commit. Until then, M1 remains blocked.

Evidence is maintained under:

```text
artifacts/evidence/
```

Workflow-alignment outcomes prepared for M0 completion:

- `IMPLEMENTATION_CONTRACT.md` is the single binding contract;
- the product is defined as an internal read-only archival-copy tool;
- employee passwords and employee impersonation are prohibited;
- one source input accepts employee UPN or OneDrive root URL;
- a mandatory scan/dry run is required before copy;
- all control documents match the real IT workflow;
- local SQLite state is approved as the operational source of truth;
- CSV and JSON are audit reports only;
- Graph delta is required for initial inventory and reconciliation;
- destination source binding prevents employee-data mixing;
- authorized transfer-account validation is defined;
- OneNote and other package items have an explicit unsupported-and-incomplete policy;
- disk headroom, disk-full behavior, timestamp preservation, exact run states, per-run reports, path mapping, collision fallback, and SQLite corruption recovery are defined;
- mandatory Windows CI, access-removal verification, and production storage protection are binding; and
- obsolete five-million-item custom-index requirements are removed as first-release blockers.

## M1 — Solution and CI foundation

**Status:** `BLOCKED`

M1 may start only after M0 is marked `DOCUMENTATION_COMPLETE` in `.ai/PHASE_STATUS.md` with committed replacement evidence tied to the exact reviewed documentation commit.

Goals:

- create `OneDriveServerTransfer.sln` at repository root;
- create WPF application and automated-test projects;
- configure .NET 10 Windows targeting;
- add MVVM, dependency injection, structured logging, and configuration;
- add SQLite dependency and schema foundation;
- add deterministic dependency restore; and
- add Windows GitHub Actions for restore, Release build, tests, static analysis, vulnerability review, and secret detection.

Exit criteria:

- solution structure matches the contract;
- Windows CI builds and tests the real source;
- no placeholder production services exist;
- no employee-password UI, model, configuration, or service exists; and
- committed M1 evidence exists.

## M2 — Microsoft authentication

Goals:

- implement MSAL interactive sign-in for the authorized IT operator;
- support MFA and Conditional Access;
- validate the configured tenant;
- enforce the deployment allowlist of authorized transfer-account Entra object IDs when configured;
- implement silent token acquisition and renewal;
- protect persistent cache with Windows DPAPI;
- implement remember-sign-in and sign-out semantics; and
- ensure no client secret, application-only authentication, write permission, token logging, employee-password processing, mutable-email-only authorization, or false session-clearing claim exists.

Exit criteria:

- authentication is isolated from UI code;
- tenant, allowlist, cache, employee-password prohibition, and redaction tests pass;
- Windows CI passes; and
- committed M2 evidence exists.

## M3 — Employee source resolution and validation

Goals:

- accept one employee UPN or one employee OneDrive for Business root URL;
- resolve UPN input to the tenant user and default Microsoft Graph drive;
- validate HTTPS and configured tenant host for URL input;
- resolve employee personal-site root and default Graph drive;
- require `driveType = business` and actual drive root;
- persist the durable identity as Tenant ID, employee Entra object ID, and source Drive ID;
- reject unknown users, unprovisioned OneDrive, files, subfolders, consumer OneDrive, shared sources, SharePoint, Teams, and external tenants;
- classify Microsoft Graph package items, including OneNote notebooks, as `Unsupported` rather than file or folder content; and
- show the resolved employee, source mode, and authorized operator confirmation without exposing protected IDs in the normal UI.

Exit criteria:

- UPN and URL resolution matrices pass;
- source-validation and package-classification matrices pass;
- no employee credential is requested or processed;
- real-tenant behavior remains unclaimed until executed; and
- committed M3 evidence exists.

## M4 — Local destination and source binding

Goals:

- accept only local fixed or directly attached storage;
- reject UNC, mapped, NAS, SMB, remote, Windows system, and application installation destinations;
- create `OneDriveData` and `_TransferReport`;
- bind the destination to tenant, employee Entra object ID, and source drive;
- record operator identity for audit without permanently binding the archive to one operator;
- permit another authorized operator to resume only after all source, destination, state, and authorization checks succeed;
- reject foreign or unsafe non-empty destinations;
- implement cross-process and cross-session locking;
- implement the exact deterministic `PathMappingVersion = 1` rules, including suffix expansion on residual collision;
- validate containment, reparse points, hard links, and NTFS permissions; and
- enforce known-byte headroom, the fixed 5 GiB reserve, and safe disk-full behavior.

Exit criteria:

- destination, binding, authorized-resume, locking, path-safety, and storage-capacity tests pass;
- no operation can escape the selected root or overwrite unrelated content; and
- committed M4 evidence exists.

## M5 — Scan, copy, resume, verification, and local state

Goals:

- implement the mandatory scan/dry run before copy;
- implement Graph delta initial inventory and checkpoint recovery;
- calculate file and folder counts and known source bytes;
- classify unsupported content and preflight path warnings;
- validate destination capacity and lock availability during scan;
- invalidate the scan when source or destination input changes;
- keep `Start Copy` disabled until a current scan succeeds;
- implement bounded queues and streaming downloads;
- enforce fixed concurrency of three;
- implement `.partial` files and safe HTTP Range resume;
- isolate temporary download hosts from Graph credentials;
- implement `Retry-After`, retries, and throttling;
- verify source metadata and supported source hashes;
- calculate local SHA-256;
- preserve source creation and modification timestamps with warning behavior;
- persist transactional SQLite state;
- keep SQLite as the operational scan, resume, and recovery source;
- implement integrity checks, protected pre-migration backup, transactional migrations, and safe corruption failure;
- implement exact item and run states, including `Incomplete`;
- handle source rename, move, deletion, and continued changes without hidden duplicates or source-side deletion; and
- support idempotent restart and rerun.

Exit criteria:

- scan, inventory, resume, retry, integrity, timestamp, run-state, SQLite, migration, source-change, and recovery tests pass;
- unsupported or failed content cannot be reported as a complete archive;
- unrelated local files cannot be overwritten;
- corrupt or unsupported state cannot be silently reset; and
- committed M5 evidence exists.

## M6 — UI, errors, and reports

Goals:

- complete the single-window UI;
- add signed-in operator, employee UPN or OneDrive URL, destination, `Scan`, `Start Copy`, `Cancel`, and `Open Report`;
- display dry-run counts, known size, unsupported items, path warnings, and storage warnings;
- add progress, cancel, bounded activity, unsupported count, failed count, and final summary;
- map failures to simple reference-coded errors;
- create one unique `Runs/<RunId>` report directory per copy run;
- implement `docs/REPORT_SCHEMA.md` exactly;
- report failed and unsupported items, timestamp warnings, storage failures, reconciliation warnings, and terminal run state; and
- protect CSV output from encoding and formula-injection problems.

Exit criteria:

- UI remains responsive;
- `Start Copy` cannot run without a successful current scan;
- no employee-password control exists;
- no technical secrets or protected identifiers appear in normal errors;
- earlier run reports cannot be overwritten;
- SQLite remains the operational source and reports remain audit output only;
- reports and cancellation behavior pass tests; and
- committed M6 evidence exists.

## M7 — Windows and real-tenant acceptance

Goals:

- run Windows Server 2019 Release build and tests;
- start the WPF application;
- complete Microsoft interactive sign-in with an authorized transfer account;
- validate a real test employee by UPN and OneDrive root URL;
- perform the mandatory dry run and verify the displayed preflight summary;
- perform complete copy, interruption, resume, reconciliation, source-change, destination-lock, disk-space, disk-full, timestamp, and report-isolation tests;
- verify `Incomplete` when supported content fails, unsupported package content exists, or the source does not stabilize;
- validate production NTFS and BitLocker/equivalent or approved exception;
- remove and verify temporary Site Collection Administrator access; and
- publish self-contained `win-x64`.

Exit criteria:

- every binding production-acceptance requirement passes with evidence; and
- committed M7 evidence exists.

## M8 — Internal release

Goals:

- finalize operating instructions and configuration template;
- tie release output to an exact source commit;
- generate required supply-chain evidence;
- sign when an approved certificate is available or document the approved limitation; and
- place the approved self-contained deliverable under `artifacts/win-x64`.

Exit criteria:

- internal release package and evidence are complete; and
- the project may be marked `Production Ready` only when M7 and M8 requirements are satisfied.
