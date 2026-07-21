# AI Handoff

## Current position

- Documentation baseline: complete.
- Application source: M1 foundation, M2 authentication, and M3 employee source resolution complete and merged into `main`.
- M3 integration: PR #12 merged; `main` baseline `fa1b81190b481a4dc4bf3f029a407b59da117ff4`; merge CI run 29742411955 succeeded.
- Development state: M4 source complete on branch `agent/m4-destination-source-binding` (pushed, intentionally not merged); paused awaiting M5 authorization.
- Current phase: `M5 — Scan, copy, resume, verification, and state`.
- Status: `NOT_STARTED`. M5 requires a new explicit owner instruction before any work begins. No M5 functionality exists.
- M4 evidence: `artifacts/evidence/M04_destination-binding_20260721T095012Z.json` on validated source commit `2861f8549e9c48b09a8336b8f48b700005f058b4` (Windows CI run 29818672841, all checks passed, 350/350 tests).

The exact evidence pointer is maintained only in `.ai/PHASE_STATUS.md`.

## M3 outcome (completed)

Implemented on branch `agent/m3-employee-source-resolution`:

- employee UPN and OneDrive-root URL parsing with strict rejection of invalid, shared, consumer, file, subfolder, SharePoint, Teams, and external-tenant inputs;
- approved v1.0 endpoint inventory centralized in `SourceResolution/GraphEndpoints.cs` (GRAPH-SRC-001/002/003 only);
- authenticated Graph GET channel with client-request-id correlation, sanitized logging (no URLs, tokens, or raw responses), and one controlled silent 401 renewal;
- `GraphRetryCoordinator` as the single Graph retry owner (Retry-After, bounded backoff with jitter, three-attempt budget, responsive cancellation);
- tenant-host, personal-site, business-drive, and owner validation producing `ResolvedEmployeeSource` (tenant ID, employee object ID, UPN when available, display name, drive ID/type/owner/webUrl, quota);
- tenant OneDrive host configuration with placeholder-only example.

## M4 outcome (completed)

Implemented on branch `agent/m4-destination-source-binding` (validated commit `2861f8549e9c48b09a8336b8f48b700005f058b4`, Windows CI run 29818672841):

- `Destination/` module with reference-coded user-facing errors (`DST-*`) mirroring the M3 pattern;
- local fixed-drive validation rejecting UNC, network/non-fixed drives, relative paths, Windows system and application-install directories, and reparse points in the path chain;
- `OneDriveData`/`_TransferReport` layout creation with enforced content/state separation and foreign-content detection;
- SQLite destination binding (tenant ID, drive ID, employee object ID) with operator audit only; foreign-source, non-empty-without-state, future-schema-version, and corrupt-database rejection (original bytes preserved, never reset); binding-store connections run with pooling disabled so rejected databases never retain an OS file lock;
- OS-backed exclusive lock file under `_TransferReport` acquired before any state write and released on failure/dispose;
- `PathMapperV1` implementing all ten contract §11 rules with an `IPathCollisionRegistry` seam (in-memory in M4; M5 substitutes SQLite persistence);
- `DestinationPathGuard` canonical containment, traversal, reparse, multi-hard-link, and stable `DST-PATH-006` path-length failures;
- capacity service with the fixed 5 GiB reserve and strictly-greater boundary plus per-file recheck;
- NTFS broad-exposure ACL evaluation behind a testable seam (Windows-gated reader);
- 141 new tests; full suite 350/350 on Windows CI.

## M5 task (not started)

Development is paused. Do not begin M5 until the repository owner issues a new explicit instruction for it. Before changing source files, mark M5 `IN_PROGRESS`. Implement the M5 goals in `docs/IMPLEMENTATION_PLAN.md` only: mandatory dry-run delta inventory, reconciliation, bounded transfer with resume and verification, transactional state, and exact run states.

## M5 boundaries

Do not implement reports, UI wiring, or production behavior during M5. Prohibited paths remain: Graph beta, application permissions, write permissions, ROPC, device-code flow, client secrets, certificates, employee-password handling.

## Completion

A phase is complete only when its exit criteria in `docs/IMPLEMENTATION_PLAN.md` pass, Windows CI validates the exact source commit, and committed evidence references that commit.

Real tenant values and acceptance inputs remain external; see `docs/ENVIRONMENT_AND_INPUTS.md`.
