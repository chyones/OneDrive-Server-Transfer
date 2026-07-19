# Implementation Plan

This plan implements the simple workflow defined in `IMPLEMENTATION_CONTRACT.md`. It does not expand product scope.

The Microsoft platform documents referenced by `AGENTS.md` are mandatory non-conflicting controls for the relevant milestones. Before completing an affected milestone, revalidate the current official Microsoft documentation listed in `docs/MICROSOFT_PLATFORM_BASELINE.md`.

## M0 — Contract simplification and pre-implementation hardening

**Status:** `DOCUMENTATION_COMPLETE`

Current committed evidence:

```text
artifacts/evidence/M00_microsoft-platform-baseline_20260719T172157Z.json
```

Validated documentation source commit:

```text
50e25cc9501ef22ad05ebe6abc1e7a96603efce2
```

The earlier workflow-alignment evidence remains committed for historical traceability.

Completed outcomes:

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
- protected Graph identifiers are excluded from normal UI and user-facing errors;
- disk headroom, disk-full behavior, timestamp preservation, exact run states, per-run reports, path mapping, collision fallback, and SQLite corruption recovery are defined;
- mandatory Windows CI, access-removal verification, and production storage protection are binding; and
- obsolete five-million-item custom-index requirements are removed as first-release blockers.

Microsoft platform hardening documents additionally define:

- Graph `v1.0` only;
- delegated-only version 1 authentication;
- WAM-preferred MSAL sign-in with system-browser fallback;
- exact endpoint and permission review;
- opaque delta links and supported `410 Gone` recovery;
- one retry owner and throttling policy;
- temporary download-host credential isolation;
- supported Microsoft source hashes and separate local SHA-256; and
- .NET and Windows patch lifecycle.

These additions do not authorize implementation before M1 or change the approved product scope.

## M1 — Solution and CI foundation

**Status:** `NOT_STARTED`

M1 is authorized to start. The implementation agent must mark M1 `IN_PROGRESS` in `.ai/PHASE_STATUS.md` before creating or changing source files.

Goals:

- create `OneDriveServerTransfer.sln` at repository root;
- create WPF application and automated-test projects;
- configure .NET 10 Windows targeting;
- add MVVM, dependency injection, structured logging, and configuration;
- add SQLite dependency and schema foundation;
- establish separate interfaces and dependency-injection boundaries for authentication, Graph metadata requests, temporary-host downloads, retry ownership, local storage, hashing, SQLite, and reporting;
- pin direct dependencies and add deterministic dependency restore;
- expose build version, source commit, SDK, and runtime version boundaries for later release evidence; and
- add Windows GitHub Actions for restore, Release build, tests, static analysis, vulnerability review, secret detection, Graph beta detection, and prohibited-authentication-flow detection.

Exit criteria:

- solution structure matches the contract;
- Windows CI builds and tests the real source;
- no placeholder production services exist;
- no employee-password UI, model, configuration, or service exists;
- no Graph beta dependency, client secret, ROPC, device-code, app-only, or Microsoft 365 write path exists;
- deterministic restore evidence includes exact initial dependency versions; and
- committed M1 evidence exists.

## M2 — Microsoft authentication

Goals:

- implement MSAL interactive sign-in for the authorized IT operator;
- configure WAM as the preferred Windows authentication broker;
- support the MSAL system-browser fallback path;
- support MFA and Conditional Access;
- validate the configured tenant;
- enforce the deployment allowlist of authorized transfer-account Entra object IDs when configured;
- implement silent token acquisition and renewal;
- protect persistent cache with Windows DPAPI;
- implement remember-sign-in and sign-out semantics;
- classify missing consent, revoked consent, `401`, and `403` correctly; and
- ensure no client secret, certificate, application-only authentication, device-code flow, ROPC, write permission, token logging, employee-password processing, mutable-email-only authorization, or false session-clearing claim exists.

Exit criteria:

- authentication is isolated from UI code;
- WAM and browser-fallback boundaries are implemented and tested;
- tenant, allowlist, cache, consent, employee-password prohibition, prohibited-flow, and redaction tests pass;
- exact MSAL version and relevant official-document review are recorded;
- Windows CI passes; and
- committed M2 evidence exists.

## M3 — Employee source resolution and validation

Goals:

- accept one employee UPN or one employee OneDrive for Business root URL;
- implement only endpoints approved in `docs/GRAPH_ENDPOINT_PERMISSION_MATRIX.md`;
- use Microsoft Graph `v1.0` only;
- use `$select` for contract-required fields where supported;
- generate `client-request-id` and record Microsoft request IDs in protected logs;
- resolve UPN input to the tenant user and default Microsoft Graph drive without adding unapproved directory permissions;
- validate HTTPS and configured tenant host for URL input;
- resolve employee personal-site root and default Graph drive;
- require `driveType = business` and actual drive root;
- persist the durable identity as Tenant ID, employee Entra object ID, and source Drive ID;
- reject unknown users, unprovisioned OneDrive, files, subfolders, consumer OneDrive, shared sources, SharePoint, Teams, and external tenants;
- classify Microsoft Graph package items, including OneNote notebooks, as `Unsupported` rather than file or folder content;
- handle unknown JSON properties and enum values safely without guessing content semantics; and
- show the resolved employee, source mode, and authorized operator confirmation without exposing protected IDs in the normal UI.

Exit criteria:

- UPN and URL resolution matrices pass;
- source-validation and package-classification matrices pass;
- the implemented endpoint inventory matches the approved delegated scope set;
- no beta endpoint, application permission, write scope, or unapproved directory permission exists;
- correlation and redaction tests pass;
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
- preserve next links and delta links as opaque values;
- process duplicate delta occurrences by Drive Item ID with the last completed occurrence winning;
- implement supported `410 Gone` fresh enumeration and reconciliation without resetting SQLite or deleting local archive content;
- calculate file and folder counts and known source bytes;
- classify unsupported and unknown content semantics and preflight path warnings;
- validate destination capacity and lock availability during scan;
- invalidate the scan when source or destination input changes;
- keep `Start Copy` disabled until a current scan succeeds;
- implement bounded queues and streaming downloads;
- enforce fixed concurrency of three;
- implement `.partial` files and safe HTTP Range resume;
- apply Range to the actual temporary URL and validate `206` and `Content-Range`;
- restart from zero when a Range request safely returns `200` instead of `206`;
- isolate temporary download hosts from Graph credentials and middleware;
- implement one retry owner, `Retry-After`, bounded exponential backoff with jitter, and accurate attempt budgets;
- classify `401`, `403`, `429`, `410`, and transient `5xx` behavior correctly;
- obtain fresh temporary URLs when expired and never persist or log them;
- verify source metadata and supported Microsoft source hashes;
- ignore Microsoft Graph `sha256Hash`;
- calculate and persist local SHA-256 separately;
- preserve source creation and modification timestamps with warning behavior;
- persist transactional SQLite state;
- keep SQLite as the operational scan, resume, and recovery source;
- implement integrity checks, protected pre-migration backup, transactional migrations, and safe corruption failure;
- implement exact item and run states, including `Incomplete`;
- handle source rename, move, deletion, and continued changes without hidden duplicates or source-side deletion; and
- support idempotent restart and rerun.

Exit criteria:

- scan, inventory, opaque-link, delta-reset, resume, retry, throttling, correlation, temporary-URL isolation, Range, integrity, supported-source-hash, local-SHA-256, timestamp, run-state, SQLite, migration, source-change, and recovery tests pass;
- unsupported, unknown, or failed content cannot be reported as a complete archive;
- unrelated local files cannot be overwritten;
- corrupt or unsupported state cannot be silently reset;
- no Graph credentials are present on temporary-host requests;
- no temporary URL is persisted or logged; and
- committed M5 evidence exists.

## M6 — UI, errors, and reports

Goals:

- complete the single-window UI;
- add signed-in operator, employee UPN or OneDrive URL, destination, `Scan`, `Start Copy`, `Cancel`, and `Open Report`;
- display dry-run counts, known size, unsupported items, unknown-item warnings, path warnings, and storage warnings;
- add progress, cancel, bounded activity, unsupported count, failed count, and final summary;
- map failures to simple reference-coded errors;
- keep request correlation and technical details in protected logs only;
- create one unique `Runs/<RunId>` report directory per copy run;
- implement `docs/REPORT_SCHEMA.md` exactly;
- report failed and unsupported items, timestamp warnings, storage failures, reconciliation warnings, delta-reset events, and terminal run state; and
- protect CSV output from encoding and formula-injection problems.

Exit criteria:

- UI remains responsive;
- `Start Copy` cannot run without a successful current scan;
- no employee-password control exists;
- no technical secrets, temporary URLs, raw responses, or protected identifiers appear in normal errors;
- earlier run reports cannot be overwritten;
- SQLite remains the operational source and reports remain audit output only;
- reports and cancellation behavior pass tests; and
- committed M6 evidence exists.

## M7 — Windows and real-tenant acceptance

Goals:

- revalidate current Microsoft platform guidance and exact deployed dependency versions;
- run Windows Server 2019 Release build and tests;
- start the WPF application;
- complete Microsoft interactive sign-in with an authorized transfer account;
- validate WAM behavior and controlled system-browser fallback where testable;
- validate MFA and Conditional Access behavior when enforced;
- confirm the deployed Entra app has only the approved delegated scopes, no application permission, and no write permission;
- validate a real test employee by UPN and OneDrive root URL;
- perform the mandatory dry run and verify the displayed preflight summary;
- perform complete copy, interruption, resume, reconciliation, delta-reset, source-change, destination-lock, disk-space, disk-full, timestamp, throttling, temporary-URL expiration, Range, source-hash, local-hash, and report-isolation tests;
- verify `Incomplete` when supported content fails, unsupported or unknown content exists, or the source does not stabilize;
- validate protected request correlation and credential isolation;
- validate production NTFS and BitLocker/equivalent or approved exception;
- remove and verify temporary Site Collection Administrator access; and
- publish self-contained `win-x64` using a supported .NET 10 servicing patch.

Exit criteria:

- every binding production-acceptance and Microsoft-platform requirement passes with evidence;
- exact MSAL, Graph SDK, .NET SDK, bundled runtime, Windows build, approved scope set, and endpoint templates are recorded; and
- committed M7 evidence exists.

## M8 — Internal release

Goals:

- finalize operating instructions and configuration template;
- tie release output to an exact source commit;
- use a currently supported .NET 10 servicing patch;
- record the bundled self-contained runtime version;
- generate required supply-chain evidence and SBOM where supported;
- calculate release artifact hashes;
- document current Windows and .NET support status;
- sign when an approved certificate is available or document the approved limitation;
- include upgrade and rollback instructions; and
- place the approved self-contained deliverable under `artifacts/win-x64`.

Exit criteria:

- internal release package and evidence are complete;
- target Windows and bundled .NET runtime remain supported;
- no unresolved relevant critical or high-severity dependency vulnerability exists without an approved exception; and
- the project may be marked `Production Ready` only when M7 and M8 requirements are satisfied.
