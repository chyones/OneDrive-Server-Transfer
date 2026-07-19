# Implementation Plan

This plan implements the simple workflow defined in `IMPLEMENTATION_CONTRACT.md`. It does not expand product scope.

## M0 — Contract simplification and correction

**Status:** `DOCUMENTATION_COMPLETE`

Evidence:

```text
artifacts/evidence/M00_contract-correction_20260719T110925Z.json
```

Completed outcomes:

- `IMPLEMENTATION_CONTRACT.md` is the single binding contract;
- all control documents match the real IT workflow;
- local SQLite state is approved;
- Graph delta is required for initial inventory and reconciliation;
- destination source binding prevents employee-data mixing;
- mandatory Windows CI, access-removal verification, and production storage protection are binding; and
- obsolete five-million-item custom-index requirements are removed as first-release blockers.

## M1 — Solution and CI foundation

**Status:** `NOT_STARTED`

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
- no placeholder production services exist; and
- committed M1 evidence exists.

## M2 — Microsoft authentication

Goals:

- implement MSAL interactive sign-in;
- support MFA and Conditional Access;
- implement silent token acquisition and renewal;
- protect persistent cache with Windows DPAPI;
- implement remember-sign-in and sign-out semantics; and
- ensure no client secret, write permission, token logging, or false session-clearing claim exists.

Exit criteria:

- authentication is isolated from UI code;
- cache and redaction tests pass;
- Windows CI passes; and
- committed M2 evidence exists.

## M3 — Employee OneDrive validation

Goals:

- validate HTTPS and configured tenant host;
- resolve employee personal-site root and default Graph drive;
- require `driveType = business` and actual drive root;
- reject files, subfolders, consumer OneDrive, shared sources, SharePoint, Teams, and external tenants; and
- show the resolved employee confirmation without exposing Graph IDs.

Exit criteria:

- source-validation matrix passes;
- real-tenant behavior remains unclaimed until executed; and
- committed M3 evidence exists.

## M4 — Local destination and source binding

Goals:

- accept only local fixed or directly attached storage;
- reject UNC, mapped, NAS, SMB, and remote destinations;
- create `OneDriveData` and `_TransferReport`;
- bind the destination to tenant, employee, and drive;
- reject foreign or unsafe non-empty destinations;
- implement cross-process and cross-session locking;
- implement deterministic path mapping; and
- validate containment, reparse points, hard links, NTFS permissions, and disk headroom.

Exit criteria:

- destination, binding, locking, and path-safety tests pass;
- no operation can escape the selected root; and
- committed M4 evidence exists.

## M5 — Copy, resume, verification, and local state

Goals:

- implement Graph delta initial inventory and checkpoint recovery;
- implement bounded queues and streaming downloads;
- enforce fixed concurrency of three;
- implement `.partial` files and safe HTTP Range resume;
- isolate temporary download hosts from Graph credentials;
- implement retries and throttling;
- verify source metadata and supported source hashes;
- calculate local SHA-256;
- persist transactional SQLite state; and
- support idempotent restart and rerun.

Exit criteria:

- inventory, resume, retry, integrity, SQLite, and recovery tests pass;
- unrelated local files cannot be overwritten; and
- committed M5 evidence exists.

## M6 — UI, errors, and reports

Goals:

- complete the single-window UI;
- add progress, cancel, bounded activity, and final summary;
- map failures to simple reference-coded errors;
- create summary, complete, failed, and log reports; and
- protect CSV output from encoding and formula-injection problems.

Exit criteria:

- UI remains responsive;
- no technical secrets or internals appear in normal errors;
- reports and cancellation behavior pass tests; and
- committed M6 evidence exists.

## M7 — Windows and real-tenant acceptance

Goals:

- run Windows Server 2019 Release build and tests;
- start the WPF application;
- complete Microsoft interactive sign-in;
- validate a real test-employee OneDrive;
- perform complete copy, interruption, resume, reconciliation, and destination-lock tests;
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