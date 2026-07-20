# AI Handoff

## Current position

- Documentation baseline: complete.
- Application source: M1 foundation, M2 authentication, and M3 employee source resolution complete and merged into `main`.
- M3 integration: PR #12 merged; `main` baseline `fa1b81190b481a4dc4bf3f029a407b59da117ff4`; merge CI run 29742411955 succeeded.
- Development state: paused after M3 integration.
- Current phase: `M4 — Destination and source binding`.
- Status: `NOT_STARTED`. M4 requires a new explicit owner instruction before any work begins. No M4 functionality exists.
- M3 evidence: `artifacts/evidence/M03_onedrive-resolution_20260720T110411Z.json` on validated source commit `eba82ff8510bda8316fa8ce4e4cdbdb4c1ca0cb9` (Windows CI run 29737013050, all checks passed, 209/209 tests).

The exact evidence pointer is maintained only in `.ai/PHASE_STATUS.md`.

## M3 outcome (completed)

Implemented on branch `agent/m3-employee-source-resolution`:

- employee UPN and OneDrive-root URL parsing with strict rejection of invalid, shared, consumer, file, subfolder, SharePoint, Teams, and external-tenant inputs;
- approved v1.0 endpoint inventory centralized in `SourceResolution/GraphEndpoints.cs` (GRAPH-SRC-001/002/003 only);
- authenticated Graph GET channel with client-request-id correlation, sanitized logging (no URLs, tokens, or raw responses), and one controlled silent 401 renewal;
- `GraphRetryCoordinator` as the single Graph retry owner (Retry-After, bounded backoff with jitter, three-attempt budget, responsive cancellation);
- tenant-host, personal-site, business-drive, and owner validation producing `ResolvedEmployeeSource` (tenant ID, employee object ID, UPN when available, display name, drive ID/type/owner/webUrl, quota);
- tenant OneDrive host configuration with placeholder-only example.

## M4 task (not started)

Development is paused. Do not begin M4 until the repository owner issues a new explicit instruction for it. Before changing source files, mark M4 `IN_PROGRESS`.

Implement M4 only:

- local fixed or directly attached destinations only;
- create `OneDriveData` and `_TransferReport`;
- bind destination to tenant, employee object, and drive IDs;
- enforce exclusive locking and authorized resume;
- implement `PathMappingVersion = 1`;
- prevent traversal, unsafe reparse redirection, hard-link overwrite, and writes outside root;
- enforce NTFS checks, known remaining bytes, and fixed 5 GiB reserve.

## M4 boundaries

Do not implement Scan, file transfer, resume, reports, or production behavior during M4. Prohibited paths remain: Graph beta, application permissions, write permissions, ROPC, device-code flow, client secrets, certificates, employee-password handling.

## Completion

A phase is complete only when its exit criteria in `docs/IMPLEMENTATION_PLAN.md` pass, Windows CI validates the exact source commit, and committed evidence references that commit.

Real tenant values and acceptance inputs remain external; see `docs/ENVIRONMENT_AND_INPUTS.md`.
