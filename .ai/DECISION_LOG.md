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
- Approval: Repository owner explicitly restated the required workflow in the current conversation.

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

### D-019 — Previous M0 evidence is invalid

- Date: 2026-07-19 UTC
- Status: `APPROVED`
- Context: The former M0 evidence recorded a mutable branch but no immutable source commit and was merged with an unresolved review comment.
- Decision: Mark the former evidence `SUPERSEDED`, reset M0 to `IN_PROGRESS`, and create corrected evidence only after the contract correction is reviewed and merged.
- Test impact: Documentation evidence validation must reject summaries without an exact validated commit.