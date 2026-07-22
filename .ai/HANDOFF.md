# AI Handoff

## Current position

- Documentation baseline: complete.
- Application source: M1 foundation, M2 authentication, and M3 employee source resolution complete and merged into `main`.
- M3 integration: PR #12 merged; `main` baseline `fa1b81190b481a4dc4bf3f029a407b59da117ff4`; merge CI run 29742411955 succeeded.
- Development state: M4 merged into `main` (PR #14, merge commit `f3011cd4216c8c1c03f74ce711c71b421ea39782`); M5 source complete on branch `agent/m5-scan-copy-resume` (pushed, intentionally not merged); paused awaiting M6 authorization.
- Current phase: `M6 — UI, errors, and reports`.
- Status: `NOT_STARTED`. M6 requires a new explicit owner instruction before any work begins. No M6 functionality exists.
- M5 evidence: `artifacts/evidence/M05_scan-copy-resume_20260722T125938Z.json` on validated source commit `c20d39bda96b9d7611cc9dd209e0c9bb38731fb4` (Windows CI run 29921734475, all checks passed, 486/486 tests).

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

## M5 outcome (completed)

Implemented on branch `agent/m5-scan-copy-resume` (validated commit `c20d39bda96b9d7611cc9dd209e0c9bb38731fb4`, Windows CI run 29921734475):

- `Inventory/` delta client: GRAPH-SCAN-001 with opaque next/delta link paging streamed page by page to a sink, checkpoint persistence, facet classification (file, folder, package → `Unsupported`, deleted tombstones, external shortcuts, unknown), and 410 reset surface (`DeltaCheckpointResetException` with opaque Location, never logged);
- `Scan/` mandatory dry run: counts, known bytes, unsupported items, path/storage warnings, capacity and NTFS checks, transactional inventory persistence, and `IsScanCurrentAsync` invalidation on source or destination change;
- `State/` transfer store: `transfer_item`, `transfer_run`, `delta_checkpoint`, `scan_state`, `path_mapping` tables (still `StateSchemaVersion = 1`), transactional idempotent accessors, in-flight reset, and run lifecycle; `SqlitePathCollisionRegistry` replaces the M4 in-memory default;
- `Transfer/`: unauthenticated temporary-download client (no Graph credentials, URL never logged/persisted), `DownloadRetryCoordinator` (single download retry owner, 5 attempts), transfer engine (fixed concurrency 3, `.partial` commit, 206 resume / 200-or-invalid-range restart, fresh-URL refetch, per-file verification: size, GRAPH-ITEM-001 stability, quickXorHash/SHA-1 source hash, separate local SHA-256, timestamp preservation), reconciliation applier (rename/move relocation by Drive Item ID, deletions never delete local, ≤3 passes, 410 fresh enumeration), and the run-state orchestrator (scan-currency gate, crash recovery to `Interrupted`, exact terminal-state rules);
- `Verification/`: streaming SHA-256, reference-exact QuickXorHash, SHA-1; Graph `sha256Hash` ignored (D-038 — delta parser corrected to quickXor-first in this milestone);
- 136 new tests; full suite 486/486 on Windows CI. A disk-stop scheduling-loop hang was found by CI and fixed before validation.

## M6 task (not started)

Development is paused. Do not begin M6 until the repository owner issues a new explicit instruction for it. Before changing source files, mark M6 `IN_PROGRESS`. Implement the M6 goals in `docs/IMPLEMENTATION_PLAN.md` only: complete one-window UI wiring (sign-in, source input, destination, `Scan`, `Start Copy`, `Cancel`, `Open Report`), progress and bounded activity, reference-coded user errors, and unique per-run reports per `docs/REPORT_SCHEMA.md`.

## M6 boundaries

Do not implement production-acceptance (M7) or release (M8) behavior during M6. Prohibited paths remain: Graph beta, application permissions, write permissions, ROPC, device-code flow, client secrets, certificates, employee-password handling.

## Completion

A phase is complete only when its exit criteria in `docs/IMPLEMENTATION_PLAN.md` pass, Windows CI validates the exact source commit, and committed evidence references that commit.

Real tenant values and acceptance inputs remain external; see `docs/ENVIRONMENT_AND_INPUTS.md`.
