# Project Memory

Store durable project facts here only. Current phase and evidence belong only in `.ai/PHASE_STATUS.md`; the active task belongs only in `.ai/HANDOFF.md`.

## Identity

- Repository: `chyones/OneDrive-Server-Transfer`
- Product: internal Windows desktop archival-copy application
- Operator: authorized IT administrator
- UI language: English
- Primary target: Windows Server 2019 x64 with Desktop Experience
- Solution location: `./OneDriveServerTransfer.sln`

## Product boundary

The application copies supported active files and folders from one employee's Microsoft 365 OneDrive for Business to local fixed or directly attached storage on the same Windows Server.

It never deletes, edits, renames, moves, uploads, or changes permissions on Microsoft 365 source content.

The one-window workflow is:

1. sign in as the authorized IT transfer operator;
2. enter employee UPN or OneDrive root URL;
3. select local destination;
4. run mandatory `Scan`;
5. review preflight results;
6. run `Start Copy`;
7. monitor progress and review reports.

## Fixed platform

- C# and .NET 10 LTS
- WPF and MVVM
- Microsoft Graph `v1.0`
- delegated interactive MSAL
- WAM preferred with system-browser fallback
- dependency injection and structured logging
- embedded SQLite operational state
- self-contained `win-x64` publish

## Source foundation (established in M1)

- `OneDriveServerTransfer.sln`, `src/OneDriveServerTransfer.App` (WPF), and `tests/OneDriveServerTransfer.Tests` (xunit) exist; root namespace `OneDriveServerTransfer`.
- `global.json` pins the .NET SDK 10.0.3xx band; CI installs from it.
- Package versions are pinned centrally in `Directory.Packages.props`; every project commits `packages.lock.json` and restores in locked mode.
- `SQLitePCLRaw.bundle_e_sqlite3` is pinned to 2.1.12 because 2.1.11 carries GHSA-2m69-gcr7-jv3q; NuGet audit NU1901–NU1904 warnings are errors on restore.
- DI composition root is `DependencyInjection/ServiceCollectionExtensions.AddApplicationServices`; later-phase service interfaces (`Abstractions/`) are intentionally unregistered until their milestone implements them.
- Structured logging is Serilog configured from the `Serilog` configuration section; the WPF implicit-using set omits `System.IO`, so IO types need explicit usings.
- SQLite schema foundation is `State/SqliteTransferStateSchemaInitializer` (metadata only); `Microsoft.Data.Sqlite` pools connections, so tests must clear the pool before deleting database files.
- Mandatory Windows CI is `.github/workflows/windows-ci.yml`; the prohibited-content guard is `scripts/Test-ProhibitedContent.ps1`.

## Authentication foundation (established in M2)

- MSAL is `Microsoft.Identity.Client` + `Microsoft.Identity.Client.Broker` 4.86.1; single-tenant public client built lazily in `Authentication/MsalIdentityClient.cs` with `WithBroker(BrokerOptions)`; MSAL itself falls back to the system browser when WAM cannot be used.
- MSAL 4.86 API notes: `PublicClientApplication.IsBrokerAvailable()` (no `IsBrokerAvailableAndInvokable`), `MsalServiceException.SubErrorForLogging` (no `SubError`), `MsalUiRequiredException.Classification` carries `ConsentRequired`, `AccountId` exposes `ObjectId` and `TenantId`, `AuthenticationResult.ClaimsPrincipal` may be absent so ID-token claims are parsed manually.
- The orchestrator is `Authentication/MsalAuthenticationService.cs`; the testable MSAL boundary is `IIdentityClient` (production: `MsalIdentityClient`; test doubles live only in the test assembly).
- The only Graph endpoint called is GRAPH-AUTH-001 `/me?$select=id,userPrincipalName,displayName` via a typed HttpClient (`OperatorProfileProvider`); no Graph SDK is referenced.
- The persistent token cache is DPAPI (CurrentUser, `System.Security.Cryptography.ProtectedData` is inbox in .NET 10 — do not re-add the package) at `%LOCALAPPDATA%/OneDriveServerTransfer/TokenCache/msal-token-cache.bin`, ACL-restricted to the current user and local Administrators; corruption clears the file and requires reauthentication.
- Operator validation (`OperatorValidator`) rejects tenant mismatch, guest/external accounts (home-tenant mismatch or `#EXT#` UPN), non-allowlisted object IDs, and missing `User.Read`; options are read lazily so DI resolution never throws on bad configuration.
- Every user-facing auth error is a `UserFacingAuthException` built by `AuthenticationErrors` with a stable `AuthenticationErrorCodes` reference code; sign-out wording (`AuthenticationErrors.SignOutDescription`) is application-only and must not overclaim.
- Log sanitization lives in `AuthErrorSanitizer`; MSAL callbacks log only sanitized non-PII text; structured auth logs carry only reference code, failure kind, MSAL error code, correlation ID, and HTTP status.
- `appsettings.example.json` authentication values are placeholders (`CONFIGURE_TENANT_ID`, `CONFIGURE_CLIENT_ID`); the approved system-browser redirect URI is `http://localhost`; the broker redirect URI is an external app-registration value.

## Source resolution foundation (established in M3)

- Every Microsoft Graph URL used by the application lives in `SourceResolution/GraphEndpoints.cs` (v1.0 base + GRAPH-AUTH-001 and GRAPH-SRC-001/002/003); a guard test fails if any other file contains a Graph URL.
- `GraphRetryCoordinator` (implements `IRetryCoordinator`) is the single Graph retry owner: 3 attempts, Retry-After honored, bounded exponential backoff with jitter, delay/jitter injectable for deterministic tests.
- `GraphRequestChannel` is the only authenticated Graph GET path: unique client-request-id per request, logs endpoint templates and correlation IDs only (never URLs, tokens, or raw bodies), and performs one controlled silent renewal on 401.
- `isPersonalSite` is a confirmed v1.0 site property (verified 2026-07-20); URL mode requires `isPersonalSite = true`, matching site-collection host, `driveType = business`, and a non-empty drive owner user ID.
- In URL mode the approved endpoints do not expose the employee UPN, so `ResolvedEmployeeSource.UserPrincipalName` is null there by design.
- The tenant OneDrive host is configured in `SourceResolution:TenantOneDriveHost` (placeholder in the example file); URL-mode hosts outside it are rejected as tenant mismatch.
- `InternalsVisibleTo` exposes internal test seams (e.g., the deterministic retry-coordinator constructor) to the test project.

## Destination foundation (established in M4)

- The `Destination/` module owns local-destination behavior with reference-coded `DST-*` user-facing errors (`DestinationErrors`/`DestinationErrorCodes`, M3-style pattern).
- Destinations must be local `DriveType.Fixed` paths; UNC, network, removable, relative, system/application directories, and reparse points in the chain are rejected. `ResolvedDestination` exposes `ContentRootPath` (`OneDriveData`) and `StateRootPath` (`_TransferReport`) and is the only way to reach those roots.
- The SQLite state schema (still `StateSchemaVersion = 1`) now includes `destination_binding` (single row: tenant/drive/employee object IDs + bound-by operator audit) and `destination_operator_audit`. `SqliteDestinationBindingStore` runs with `Pooling = false` so a rejected corrupt database never retains an OS file lock on Windows.
- Exclusive locking is a `FileShare.None` lock file under `_TransferReport`, acquired before any state write (`DestinationSessionService`) and released on failure/dispose.
- `PathMapperV1` (contract §11, all ten rules) is pure and deterministic; collision state lives behind `IPathCollisionRegistry` (in-memory in M4 — M5 substitutes the SQLite-backed registry on the same interface). `MaxCanonicalPathUtf16Units = 32767`; `PathTooLongException` maps to stable `DST-PATH-006`, never to containment.
- `DestinationPathGuard` performs canonical containment plus reparse/hard-link overwrite refusal via the `IFileSystemProbe` seam (`GetFileInformationByHandle` is Windows-gated).
- `DestinationCapacityService` uses a fixed 5 GiB reserve with strictly-greater comparison (boundary-exact fails) for both total and per-file checks.
- NTFS broad-exposure evaluation (`DestinationSecurityEvaluator`) uses `System.IO.FileSystem.AccessControl` 5.0.0 (already pinned; same package as M2), Windows-gated behind a pure evaluation core.
- Windows CI gotcha proven in M4: pooled SQLite connections keep file locks on Windows, and `Path.GetFullPath` can throw `PathTooLongException` for >32767-unit paths on Windows but not macOS.

## Scan and transfer foundation (established in M5)

- `Inventory/`: `IDeltaInventoryClient.EnumerateAsync(driveId, resumeLink, pageSink, ct)` streams delta pages in order; GRAPH-SCAN-001 is the only inventory endpoint; 410 surfaces as `DeltaCheckpointResetException.FreshEnumerationLocation` (opaque, never logged). Facet precedence: deleted → package → remoteItem → file → folder → unknown; package items are `Unsupported` and force `Incomplete`.
- `Scan/`: `IScanService.ScanAsync` (mandatory dry run, transactional inventory persistence) and `IsScanCurrentAsync` (gate consumed by the transfer orchestrator; false on source or destination change).
- State schema (still `StateSchemaVersion = 1`) adds `transfer_item`, `transfer_run`, `delta_checkpoint`, `scan_state`, `path_mapping`. `ITransferStateStore` = `SqliteTransferStateStore` (pooling disabled, transactional, idempotent). `SqlitePathCollisionRegistry` is the DI-registered `IPathCollisionRegistry` (M5 replacement for the M4 in-memory default).
- `Transfer/`: `TemporaryDownloadClient` uses the separate unauthenticated `"download"` HttpClient (cookies off; no Graph credentials ever; URL never logged/persisted). `DownloadRetryCoordinator` owns download retries (5 attempts/file); `GraphRetryCoordinator` owns Graph retries — never two layers on one request. `GraphMetadataClient` implements GRAPH-ITEM-001 and GRAPH-DL-001 (`$select=@microsoft.graph.downloadUrl`).
- `TransferOrchestrator` is the run-state machine: scan-currency gate, fixed semaphore of 3, per-file capacity recheck (stop scheduling, never `Completed`), ≤3 reconciliation passes, crash recovery (stale runs → `Interrupted`, in-flight reset), exact terminal-state rules. `ReconciliationApplier` handles tombstones (never delete local), rename/move relocation by Drive Item ID, and content-change recopy.
- `Verification/`: reference-exact `QuickXorHash` (preferred per D-038; sha1Hash accepted; Graph `sha256Hash` ignored); local streaming SHA-256 stored separately; no source-verification claim without a comparable hash. `TimestampPreservation` classifies pre-1601 values as `UnsupportedValue` (warning → `CompletedWithWarnings`), deterministically, not via OS rejection.
- Windows CI lesson: the disk-reserve stop path must exit the scheduling loop (a re-fetch spin hung CI for 30 minutes); timing-based concurrency tests must use bounded gates, not delays.

## Fixed controls

- No employee-password collection or employee authentication.
- No Graph beta, application permissions, Microsoft 365 write permissions, ROPC, device-code flow, client secrets, or certificates.
- Only approved endpoints and scopes from `docs/GRAPH_ENDPOINT_PERMISSION_MATRIX.md`.
- One employee and one business OneDrive root per run.
- Mandatory dry run before copy; source or destination changes invalidate it.
- Destination bound to tenant ID, employee object ID, and drive ID.
- Local destinations only; no UNC, NAS, SMB, mapped, or remote storage.
- SQLite is the operational source for scan, resume, recovery, binding, and mappings.
- CSV and JSON are audit outputs only.
- Maximum three concurrent downloads.
- Streaming, `.partial` files, validated Range resume, bounded retry, and `Retry-After` handling.
- Temporary download URLs are never logged or persisted and never receive Graph credentials.
- Delta links are opaque; supported `410 Gone` requires fresh enumeration and reconciliation.
- Local SHA-256 remains separate from supported Microsoft source hashes; Graph `sha256Hash` is ignored.
- `PathMappingVersion = 1` and `StateSchemaVersion = 1` start compatibility versioning.
- Fixed 5 GiB destination-space reserve.
- Unsupported package content or missing supported content produces `Incomplete`.
- Each run has an isolated `_TransferReport/Runs/<RunId>` directory.
- Production storage requires restricted NTFS access and BitLocker, approved equivalent, or approved exception.

## Out of scope for version 1

Dashboards, scheduling, batch employees, service mode, remote administration, remote destinations, central reporting, email notifications, source modification, previous versions, Recycle Bin, permissions, comments, compliance data, OneNote/package export, SharePoint libraries, and Teams libraries.

## External values not stored in Git

Tenant values, real Client ID, account object IDs, employee identities, production paths, credentials, tokens, employee content, production databases, and unredacted reports remain outside the repository. Their readiness is tracked in `docs/ENVIRONMENT_AND_INPUTS.md`.
