# Decision Log

Use UTC dates. Do not delete historical decisions. Mark changed decisions as `SUPERSEDED` and record the replacement.

## Status values

- `PROPOSED`
- `APPROVED`
- `REJECTED`
- `SUPERSEDED`

## Historical decisions retained

### D-001 — Native Windows desktop application

- Status: `APPROVED`
- Decision: C# .NET 10 LTS WPF application running on Windows Server 2019.

### D-002 — Local destination only

- Status: `APPROVED`
- Decision: Write only to local storage attached to the same Windows Server.
- Excluded: UNC, mapped drives, NAS, SMB, and remote destinations.

### D-003 — Read-only Microsoft 365 access

- Status: `APPROVED`
- Decision: Interactive delegated MSAL authentication with read permissions only.
- Excluded: client secret, application-only authentication, and write permissions.

### D-004 — One employee OneDrive root per run

- Status: `APPROVED`
- Decision: Copy the supported active content of one employee OneDrive for Business root.

### D-005 — Fixed concurrency

- Status: `APPROVED`
- Decision: Maximum three simultaneous file downloads; not user-editable.

### D-006 — Versioned operational formats

- Status: `APPROVED`
- Decision: Start with `StateSchemaVersion = 1` and `PathMappingVersion = 1`.

### D-007 — Evidence-based completion

- Status: `APPROVED`
- Decision: Separate Documentation Ready, Source Implementation Complete, and Production Ready. Production Ready requires actual Windows and real-tenant evidence.

### D-008 — Repository root is project root

- Status: `APPROVED`
- Decision: Create the solution, source, tests, scripts, docs, and artifacts directly at repository root.

### D-009 — Durable evidence summaries

- Status: `APPROVED`
- Decision: Commit small redacted milestone summaries under `artifacts/evidence`; raw generated output remains local or in CI artifacts.

### D-010 — Local SHA-256

- Status: `APPROVED`
- Decision: Calculate and persist streaming local SHA-256 for every completed file and keep it separate from Microsoft source-hash verification.

### D-011 — Continuous destination containment

- Status: `APPROVED`
- Decision: Revalidate safe containment during file create, open, replace, and rename operations.

### D-012 — NTFS and storage protection

- Status: `APPROVED`
- Decision: Production use requires restricted local access and BitLocker or an approved organizational equivalent or exception.

### D-013 — Custom five-million-item disk index

- Status: `SUPERSEDED`
- Former decision: No database and a custom disk-based index designed for five million items.
- Replaced by: D-016.
- Reason: It added major implementation risk and complexity unrelated to the required simple internal workflow.

### D-014 — Microsoft access lifecycle

- Status: `APPROVED`
- Decision: Grant Site Collection Administrator access outside the application, use a dedicated transfer account where practical, and remove access when no longer required.

## Current approved decisions

### D-015 — Original simple URL-only workflow

- Date: 2026-07-19 UTC
- Status: `SUPERSEDED`
- Former decision: The administrator signs in, pastes one employee OneDrive root URL, selects a local destination, presses `Copy Data`, monitors progress, and reviews the result in one window.
- Replaced by: D-027.
- Reason: The revised workflow keeps the same one-window simplicity while accepting employee UPN or root URL and requiring a safety scan before copy.

### D-016 — Local SQLite transfer state

- Date: 2026-07-19 UTC
- Status: `APPROVED`
- Context: Reliable scan, resume, crash recovery, source binding, and lookup are required, but a custom JSONL database engine is unnecessary.
- Decision: Use one local SQLite file at `_TransferReport/TransferState.db`. SQLite is embedded and requires no database server.
- Security impact: State database must not contain tokens, passwords, temporary URLs, or employee file contents and must be protected by NTFS permissions.
- Compatibility impact: Replaces the custom five-million-item index requirement.
- Test impact: Add transaction, scan-state, recovery, schema-version, and corruption-handling tests.

### D-017 — Graph delta inventory and reconciliation

- Date: 2026-07-19 UTC
- Status: `APPROVED`
- Context: The application needs a complete page-by-page initial inventory and reliable detection of changes without retaining the full hierarchy in memory.
- Decision: Use Microsoft Graph v1.0 drive delta for dry-run inventory, persist the delta checkpoint, and use up to three bounded reconciliation passes.
- Security impact: Read-only Graph calls only.
- Test impact: Add paging, checkpoint, restart, deletion, move, rename, and continued-change tests.

### D-018 — Destination source binding

- Date: 2026-07-19 UTC
- Status: `APPROVED`
- Decision: Bind each destination to Tenant ID, source Drive ID, and employee Entra object ID. Reject another source or an unsafe non-empty destination.
- Security impact: Prevents mixing data from different employees.
- Test impact: Add mismatch and recovery tests.

### D-019 — Former M0 evidence invalidation

- Date: 2026-07-19 UTC
- Status: `APPROVED`
- Context: The former M0 evidence recorded a mutable branch but no immutable source commit and was merged with an unresolved review comment.
- Decision: Preserve the former evidence as historical only and require corrected evidence tied to an exact reviewed commit.
- Test impact: Documentation evidence validation must reject summaries without an exact validated commit.

### D-020 — Authorized transfer-account validation

- Date: 2026-07-19 UTC
- Status: `APPROVED`
- Decision: Verify the configured tenant after sign-in and support a deployment allowlist of authorized transfer-account Entra object IDs. Do not authorize by display name or mutable email address alone.
- Security impact: Prevents operation under an unintended Microsoft account.
- Test impact: Add tenant, object-ID allowlist, guest, and wrong-account rejection tests.

### D-021 — Package items are unsupported in version 1

- Date: 2026-07-19 UTC
- Status: `APPROVED`
- Context: Microsoft Graph package items, including OneNote notebooks, are neither ordinary file nor folder items.
- Decision: Classify package items as `Unsupported`, include them in reports, and never silently claim they were copied.
- Result impact: Superseded by D-029; package items now require final run state `Incomplete` unless a more severe state applies.
- Scope impact: Exporting or reconstructing package content remains out of scope.
- Test impact: Add package classification, reporting, and run-state tests.

### D-022 — Fixed destination-space reserve

- Date: 2026-07-19 UTC
- Status: `APPROVED`
- Decision: Require known remaining bytes plus a fixed 5 GiB free-space reserve and recheck before each file when totals are incomplete or change.
- Integrity impact: Disk-full or reserve failure stops new scheduling, preserves safe state, and cannot return `Completed` or `CompletedWithWarnings`.
- Test impact: Add preflight, changing-total, mid-run disk-full, partial preservation, and terminal-state tests.

### D-023 — Preserve source timestamps

- Date: 2026-07-19 UTC
- Status: `APPROVED`
- Decision: Preserve source creation and modification timestamps on local files when Windows supports the values and apply directory timestamps after child processing.
- Result impact: Timestamp failure is reported and produces `CompletedWithWarnings` only when every supported item was copied or validly skipped.
- Test impact: Add file, directory, unsupported-value, and warning tests.

### D-024 — Original run-state set

- Date: 2026-07-19 UTC
- Status: `SUPERSEDED`
- Former decision: Use `InProgress`, `Completed`, `CompletedWithWarnings`, `Failed`, `Cancelled`, and `Interrupted` run states.
- Replaced by: D-029.
- Reason: Missing or unsupported content must not be represented as a successful completion with a warning.

### D-025 — Deterministic `PathMappingVersion = 1`

- Date: 2026-07-19 UTC
- Status: `APPROVED`
- Decision: Use Unicode Form C, `_xHHHH_` encoding for unsafe characters and trailing dots or spaces, reserved-name prefixing, ordinal case-insensitive collision checks, a source-item-ID SHA-256 suffix, a 200 UTF-16-unit component limit, persistent mapping, and explicit failure when the final path remains unsupported.
- Compatibility impact: Any mapping-rule change requires a new path-mapping version and migration decision.
- Test impact: Add golden compatibility vectors and collision, length, Unicode, reserved-name, and resume tests.

### D-026 — SQLite integrity and migration failure safety

- Date: 2026-07-19 UTC
- Status: `APPROVED`
- Decision: Validate SQLite integrity before resume, create a protected backup before schema migration, migrate transactionally, reject future schemas, and fail without silent reset or adoption when corruption or migration failure occurs.
- Integrity impact: Existing employee content cannot be overwritten based on missing or untrusted state.
- Test impact: Add integrity-check, backup, rollback, corruption, future-schema, and no-silent-reset tests.

### D-027 — UPN-or-URL source input with mandatory dry run

- Date: 2026-07-19 UTC
- Status: `APPROVED`
- Context: IT needs a simpler and more reliable source identifier than a URL-only workflow, while copy must not begin before scope and storage are understood.
- Decision: Use one source field that accepts employee UPN or OneDrive for Business root URL. Require `Scan` before `Start Copy`. The scan resolves the employee and drive, completes delta inventory, calculates counts and known bytes, applies path mapping, reports unsupported items and warnings, and validates destination and storage without downloading employee file content.
- UI impact: One window remains; changing source or destination invalidates the scan.
- Test impact: Add UPN, URL, scan, stale-scan, and preflight-summary tests.

### D-028 — Employee credentials are prohibited

- Date: 2026-07-19 UTC
- Status: `APPROVED`
- Decision: Never request, collect, store, log, transmit, or process an employee password and never authenticate as the employee. The authorized IT operator remains the authenticated actor.
- Security impact: Preserves accountable IT activity and avoids employee impersonation.
- Test impact: Add UI, configuration, model, logging, and static checks for prohibited employee-password paths.

### D-029 — Exact run states include `Incomplete`

- Date: 2026-07-19 UTC
- Status: `APPROVED`
- Decision: Use `InProgress`, `Completed`, `CompletedWithWarnings`, `Incomplete`, `Failed`, `Cancelled`, and `Interrupted`.
- `Completed`: all supported content copied or validly skipped, no warning, stable reconciliation.
- `CompletedWithWarnings`: all supported content copied or validly skipped and only non-content warnings remain.
- `Incomplete`: supported content failed, unsupported content exists, or the source did not stabilize.
- Audit impact: Prevents an incomplete archive from appearing successful.
- Test impact: Add terminal-state truth-table and archive-completeness tests.

### D-030 — Deterministic residual-collision fallback

- Date: 2026-07-19 UTC
- Status: `APPROVED`
- Decision: Start the path collision suffix with the first 10 SHA-256 hexadecimal characters of the source Drive Item ID. If a collision remains, expand to 20 characters and then the complete hash. Never use random or order-dependent suffixes.
- Compatibility impact: This behavior is part of `PathMappingVersion = 1` before implementation begins.
- Test impact: Add forced residual-collision vectors.

### D-031 — Report schema and operational authority

- Date: 2026-07-19 UTC
- Status: `APPROVED`
- Decision: Implement `docs/REPORT_SCHEMA.md`. SQLite remains the operational source for scan, resume, recovery, and source binding. CSV and JSON are protected human-readable audit reports only.
- Security impact: Reports require UTF-8, CSV formula-injection protection, sanitized errors, and secret redaction.
- Test impact: Add schema, encoding, escaping, redaction, and authority tests.

### D-032 — Durable employee identity and operator audit

- Date: 2026-07-19 UTC
- Status: `APPROVED`
- Decision: Bind the destination to Tenant ID, employee Entra object ID, and source Drive ID. Record normalized employee UPN and signed-in operator UPN for display and audit. Do not bind the archive permanently to one operator.
- Recovery impact: Another authorized operator may resume only after tenant, employee, drive, destination, SQLite state, and authorization checks match.
- Test impact: Add UPN-change, operator-change, allowed-resume, and rejected-resume tests.

### D-033 — WAM-preferred interactive authentication

- Date: 2026-07-19 UTC
- Status: `APPROVED`
- Context: Current MSAL.NET guidance supports WAM on Windows Server 2019 and later with browser fallback.
- Decision: Prefer WAM for version 1 interactive delegated sign-in and support the MSAL system-browser fallback path.
- Excluded: embedded browser without approved exception, ROPC, device-code flow, client credentials, certificates, managed identity, and employee authentication.
- Test impact: Add WAM configuration, fallback boundary, MFA, Conditional Access, consent, sign-out, and prohibited-flow tests.

### D-034 — Exact Graph endpoint and delegated-permission control

- Date: 2026-07-19 UTC
- Status: `APPROVED`
- Decision: Implement Microsoft Graph `v1.0` endpoints only and maintain `docs/GRAPH_ENDPOINT_PERMISSION_MATRIX.md` as the approved endpoint-to-permission inventory.
- Security impact: Version 1 prohibits Graph beta, application permissions, Microsoft 365 write permissions, and unapproved directory permissions.
- Test impact: Add endpoint inventory, scope inventory, beta detection, and write-permission absence checks.

### D-035 — Opaque delta links and reset recovery

- Date: 2026-07-19 UTC
- Status: `APPROVED`
- Decision: Preserve returned next links and delta links as opaque values, process duplicate item occurrences by Drive Item ID, and handle supported `410 Gone` through fresh enumeration and reconciliation.
- Integrity impact: Delta reset must not silently reset SQLite or delete retained local archive content.
- Test impact: Add opaque-link, duplicate-item, 410 reset, fresh-enumeration, and no-local-delete tests.

### D-036 — Single retry owner and request correlation

- Date: 2026-07-19 UTC
- Status: `APPROVED`
- Decision: Exactly one layer owns automatic retry for each HTTP request category. Respect `Retry-After`, use bounded backoff with jitter when required, and generate protected request correlation IDs.
- Reliability impact: Prevents multiplicative SDK-plus-application retries and supports Microsoft troubleshooting.
- Test impact: Add retry-owner, actual-attempt-count, cancellation-delay, 401, 403, 429, 503, correlation, and redaction tests.

### D-037 — Temporary download URL and Range isolation

- Date: 2026-07-19 UTC
- Status: `APPROVED`
- Decision: Treat temporary download URLs as short-lived preauthenticated secrets, use a separate unauthenticated HTTP client, never persist or log them, and never send Graph credentials to their hosts.
- Resume impact: Apply Range to the actual temporary URL, accept resume only with valid `206` and `Content-Range`, and restart from zero when Range is ignored with `200`.
- Test impact: Add credential-isolation, URL-expiration, 206, 200-restart, invalid-range, and no-persistence tests.

### D-038 — Supported Microsoft source hashes

- Date: 2026-07-19 UTC
- Status: `APPROVED`
- Decision: Prefer `quickXorHash` when available, permit other supported Microsoft source hashes when supplied, ignore Microsoft Graph `sha256Hash`, and keep every source hash separate from local SHA-256.
- Integrity impact: Missing source hash cannot be misrepresented as source cryptographic verification.
- Test impact: Add quickXorHash, optional hash, missing hash, unsupported sha256Hash, mismatch, and local-SHA-256 tests.

### D-039 — Self-contained servicing and platform lifecycle

- Date: 2026-07-19 UTC
- Status: `APPROVED`
- Decision: Self-contained releases must be rebuilt and republished to receive later .NET runtime patches. Release evidence records exact SDK, bundled runtime, MSAL, Graph SDK, Windows build, source commit, and artifact hash.
- Lifecycle impact: Production Ready is invalid when the deployed Windows target or bundled .NET runtime is out of support.
- Test impact: Add publish-version, artifact-traceability, vulnerability, SBOM, lifecycle, upgrade, and rollback checks.

### D-040 — Microsoft platform M0 evidence supersedes the prior current pointer

- Date: 2026-07-19 UTC
- Status: `APPROVED`
- Context: The documentation baseline was expanded with current Microsoft implementation controls while preserving the approved product scope.
- Decision: Use `artifacts/evidence/M00_microsoft-platform-baseline_20260719T172157Z.json` as the current M0 evidence tied to documentation source commit `50e25cc9501ef22ad05ebe6abc1e7a96603efce2`.
- History impact: Preserve `M00_workflow-alignment_20260719T124036Z.json` as prior valid historical evidence.
- Completion impact: Application implementation remains not started and M1 remains `NOT_STARTED` until explicitly marked `IN_PROGRESS`.
