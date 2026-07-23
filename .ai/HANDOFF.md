# AI Handoff

## Current position

- Documentation baseline: complete.
- Application source: M1 foundation, M2 authentication, and M3 employee source resolution complete and merged into `main`.
- M3 integration: PR #12 merged; `main` baseline `fa1b81190b481a4dc4bf3f029a407b59da117ff4`; merge CI run 29742411955 succeeded.
- Development state: M4 merged into `main` (PR #14) and M5 merged into `main` (PR #15, merge commit `5a986bba4ee6c1b1bfa7c6d3d5431854bd7b0e71`); M6 source complete on branch `agent/m6-ui-errors-reports` (pushed, intentionally not merged); paused awaiting M7 authorization.
- Current phase: `M7 — Windows and real-tenant acceptance`.
- Status: `NOT_STARTED`. M7 requires a new explicit owner instruction before any work begins. No M7 activity has occurred.
- M6 evidence: `artifacts/evidence/M06_ui-errors-reports_20260723T092549Z.json` on validated source commit `c33138b4c1c34cb57603077679d8c42b3ea4c083` (Windows CI run 29995074450, all checks passed, 576/576 tests).

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

## M6 outcome (completed)

Implemented on branch `agent/m6-ui-errors-reports` (validated commit `c33138b4c1c34cb57603077679d8c42b3ea4c083`, Windows CI run 29995074450):

- `Reporting/`: `IReportWriter` implementation producing `_TransferReport\Runs\<RunId>\{TransferSummary.json, TransferReport.csv, FailedFiles.csv, TransferLog.log}` with exact `docs/REPORT_SCHEMA.md` header order, UTF-8 (no BOM), RFC 4180 escaping, leading-apostrophe formula-injection neutralization (D-040), sanitized error text, and `FileMode.CreateNew` per-run isolation; per-run Serilog file sink for the protected technical log; report generation wired into `TransferOrchestrator` finalization without ever masking the run outcome;
- one-window UI: complete `MainViewModel` workflow (sign-in, UPN/URL input, destination selection, mandatory Scan, confirmation-gated Start Copy, Cancel, progress with counts and a 100-entry bounded activity list, exact terminal run states with `Incomplete` stating the archive is not complete) and full `MainWindow.xaml`;
- Start Copy enabled only after a successful current scan plus explicit confirmation; any source or destination change invalidates the scan; scan currency revalidated service-side before scheduling; indeterminate progress while totals are unknown;
- reference-coded error display with UI redaction (protected identifiers, tokens, URLs, stack traces never rendered); `IFolderPickerService` and `IShellService` abstractions for Browse, Open Report, and Open Destination;
- additive `IProgress<TransferProgress>` surface on the transfer orchestrator; no M2–M5 semantics changed;
- 90 new tests; full suite 576/576 on Windows CI.

With M1–M6 complete and evidenced, the completion label is `Source Implementation Complete` (not Production Ready; M7/M8 unexecuted).

## M7 task (not started)

Development is paused. Do not begin M7 until the repository owner issues a new explicit instruction for it. M7 executes on the target environment per `docs/IMPLEMENTATION_PLAN.md`: Windows Server build, WPF startup, real sign-in, real UPN/URL resolution, complete dry run and copy, interruption/resume, reconciliation, locking, disk-space behavior, reports, ACL/encryption validation, access-removal verification, and self-contained publish.

## M6 boundaries

Do not implement production-acceptance (M7) or release (M8) behavior during M6. Prohibited paths remain: Graph beta, application permissions, write permissions, ROPC, device-code flow, client secrets, certificates, employee-password handling.

## Completion

A phase is complete only when its exit criteria in `docs/IMPLEMENTATION_PLAN.md` pass, Windows CI validates the exact source commit, and committed evidence references that commit.

Real tenant values and acceptance inputs remain external; see `docs/ENVIRONMENT_AND_INPUTS.md`.
