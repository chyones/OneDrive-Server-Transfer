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
- Decision: Copy the active content of one employee OneDrive for Business root.

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

### D-015 — Simple IT workflow is the product boundary

- Date: 2026-07-19 UTC
- Status: `APPROVED`
- Context: The repository had accumulated controls that exceeded the actual operational need.
- Decision: The administrator signs in, pastes one employee OneDrive root URL, selects a local destination, presses `Copy Data`, monitors progress, and reviews the result in one window.
- Excluded: dashboards, scheduling, batch employee processing, service mode, remote destinations, central reporting, and advanced user-facing controls.
- Approval: Repository owner explicitly restated the required workflow.

### D-016 — Local SQLite transfer state

- Date: 2026-07-19 UTC
- Status: `APPROVED`
- Context: Reliable resume, crash recovery, source binding, and lookup are required, but a custom JSONL database engine is unnecessary.
- Decision: Use one local SQLite file at `_TransferReport/TransferState.db`. SQLite is embedded and requires no database server.
- Security impact: State database must not contain tokens, passwords, temporary URLs, or employee file contents and must be protected by NTFS permissions.
- Compatibility impact: Replaces the custom five-million-item index requirement.
- Test impact: Add transaction, recovery, schema-version, and corruption-handling tests.

### D-017 — Graph delta inventory and reconciliation

- Date: 2026-07-19 UTC
- Status: `APPROVED`
- Context: The application needs a complete page-by-page initial inventory and reliable detection of changes without retaining the full hierarchy in memory.
- Decision: Use Microsoft Graph v1.0 drive delta for initial inventory, persist the delta checkpoint, and use up to three bounded reconciliation passes.
- Security impact: Read-only Graph calls only.
- Test impact: Add paging, checkpoint, restart, deletion, move, rename, and continued-change tests.

### D-018 — Destination source binding

- Date: 2026-07-19 UTC
- Status: `APPROVED`
- Decision: Bind each destination to Tenant ID, source Drive ID, and protected employee identity. Reject another source or an unsafe non-empty destination.
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

### D-021 — Package items are reported as unsupported in version 1

- Date: 2026-07-19 UTC
- Status: `APPROVED`
- Context: Microsoft Graph package items, including OneNote notebooks, are neither ordinary file nor folder items.
- Decision: Classify package items as `Unsupported`, include them in reports, and force `CompletedWithWarnings` unless a more severe terminal state applies. Never silently claim they were copied.
- Scope impact: Exporting or reconstructing package content remains out of scope.
- Test impact: Add package classification, reporting, and run-state tests.

### D-022 — Fixed destination-space reserve

- Date: 2026-07-19 UTC
- Status: `APPROVED`
- Decision: Require known remaining bytes plus a fixed 5 GiB free-space reserve and recheck before each file when totals are incomplete or change.
- Integrity impact: Disk-full or reserve failure stops new scheduling, preserves safe state, and cannot return `Completed`.
- Test impact: Add preflight, changing-total, mid-run disk-full, partial preservation, and terminal-state tests.

### D-023 — Preserve source timestamps

- Date: 2026-07-19 UTC
- Status: `APPROVED`
- Decision: Preserve source creation and modification timestamps on local files when Windows supports the values and apply directory timestamps after child processing.
- Result impact: Timestamp failure is reported and produces `CompletedWithWarnings` without invalidating verified bytes.
- Test impact: Add file, directory, unsupported-value, and warning tests.

### D-024 — Exact run states and isolated reports

- Date: 2026-07-19 UTC
- Status: `APPROVED`
- Decision: Use `InProgress`, `Completed`, `CompletedWithWarnings`, `Failed`, `Cancelled`, and `Interrupted` run states. Store each run's reports under `_TransferReport/Runs/<RunId>` and never overwrite another run.
- Audit impact: Preserves historical evidence and prevents ambiguous final outcomes.
- Test impact: Add terminal-state truth-table and report-isolation tests.

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
