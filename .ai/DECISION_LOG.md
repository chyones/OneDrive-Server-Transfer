# Decision Log

Use UTC dates. Do not delete prior decisions. Superseded decisions remain for traceability.

## Status values

- `PROPOSED`
- `APPROVED`
- `REJECTED`
- `SUPERSEDED`

## Approved decisions

### D-001 — Native Windows desktop application

- Status: `APPROVED`
- Decision: C# .NET 10 LTS WPF application.
- Reason: Native Windows Server operation with a simple professional interface.

### D-002 — Local destination only

- Status: `APPROVED`
- Decision: Write only to local storage attached to the same Windows Server.
- Excluded: UNC, mapped network drives, NAS, SMB, remote-server destinations.

### D-003 — Read-only Microsoft 365 access

- Status: `APPROVED`
- Decision: Delegated interactive MSAL authentication using approved read permissions only.
- Excluded: Client secret and write permissions.

### D-004 — One employee OneDrive root per run

- Status: `APPROVED`
- Decision: Backup the active in-scope content of one employee OneDrive for Business root.
- Excluded: Shared files, subfolders as source, SharePoint libraries, batch processing.

### D-005 — Fixed concurrency

- Status: `APPROVED`
- Decision: Maximum three simultaneous file downloads.
- Configuration: Not user-editable and not deployment-configurable.

### D-006 — Versioned operational formats

- Status: `APPROVED`
- Decision:
  - `ManifestVersion = 1`
  - `PathMappingVersion = 1`

### D-007 — Evidence-based completion

- Status: `APPROVED`
- Decision: Separate Documentation Ready, Source Implementation Complete, and Production Ready.
- Production Ready requires actual Windows and real-tenant validation.

### D-008 — Repository root is project root

- Date: 2026-07-19 UTC
- Status: `APPROVED`
- Context: The base contract required a nested project directory while repository controls and artifacts were already defined at repository root.
- Decision: Create `OneDriveServerTransfer.sln`, `src`, `tests`, `scripts`, `docs`, and `artifacts` directly at repository root. Do not create a nested project container.
- Contract sections affected: Section 4.
- Security impact: Removes ambiguous duplicate governance and artifact locations.
- Compatibility impact: None; source layout only.
- Test impact: All scripts and workflows use repository-root paths.
- Approval: User authorized direct documentation correction before implementation.

### D-009 — Durable evidence summaries

- Date: 2026-07-19 UTC
- Status: `APPROVED`
- Context: Raw generated evidence is ignored by Git and cannot support durable phase claims in a fresh clone.
- Decision: Commit small redacted evidence summaries under `artifacts/evidence`; retain raw generated output under ignored source and publish directories or CI artifacts.
- Contract sections affected: Sections 15, 24, 27.
- Security impact: Requires explicit redaction and prohibits sensitive evidence content.
- Compatibility impact: None.
- Test impact: Every completed milestone requires a committed evidence summary.
- Approval: User authorized direct documentation correction before implementation.

### D-010 — Local SHA-256 for every completed file

- Date: 2026-07-19 UTC
- Status: `APPROVED`
- Context: Size and source metadata cannot detect later local same-size corruption or substitution.
- Decision: Calculate, persist, and revalidate streaming local SHA-256 for every completed file while retaining exact claims about whether a source-provided hash existed.
- Contract sections affected: Sections 10, 11, 12, 14.
- Security impact: Detects local corruption and tampering after download.
- Compatibility impact: Manifest and report schemas must include local SHA-256.
- Test impact: Add corruption, same-size substitution, recovery, and bounded-memory hash tests.
- Approval: User authorized direct documentation correction before implementation.

### D-011 — Continuous destination containment

- Date: 2026-07-19 UTC
- Status: `APPROVED`
- Context: Startup-only reparse-point validation is vulnerable to directory replacement during multi-day transfers.
- Decision: Revalidate containment and reparse-point state during create, open, replace, and rename operations, including adversarial junction-swap tests.
- Contract sections affected: Sections 9, 10, 14.
- Security impact: Prevents writes outside the selected destination through time-of-check/time-of-use redirection.
- Compatibility impact: Windows filesystem implementation must use safe handle and final-path validation techniques.
- Test impact: Add live junction replacement and race-oriented tests.
- Approval: User authorized direct documentation correction before implementation.

### D-012 — Production ACL and storage-protection baseline

- Date: 2026-07-19 UTC
- Status: `APPROVED`
- Decision: Production acceptance requires restricted NTFS access, protected token cache, and BitLocker or an approved documented equivalent or exception.
- Contract sections affected: Sections 9, 20, 24.
- Security impact: Protects employee backup data and authentication material at rest.
- Compatibility impact: Production preparation checklist expanded.
- Test impact: Add ACL validation and production evidence.
- Approval: User authorized direct documentation correction before implementation.

### D-013 — Manifest lookup design gate

- Date: 2026-07-19 UTC
- Status: `APPROVED`
- Context: Segment-order metadata alone is not a sufficient five-million-item operational index.
- Decision: M5 cannot begin until a disk-based index design is documented and approved, covering lookup keys, crash safety, recovery, complexity, and benchmark behavior. The no-database rule remains unchanged.
- Contract sections affected: Sections 11, 14, 26.
- Security impact: Prevents unreliable or unbounded recovery logic.
- Compatibility impact: Manifest architecture must remain versioned and migratable.
- Test impact: Add index recovery, lookup-complexity, collision, and benchmark evidence.
- Approval: User authorized direct documentation correction before implementation.

### D-014 — Microsoft access lifecycle

- Date: 2026-07-19 UTC
- Status: `APPROVED`
- Decision: Use a dedicated transfer account where possible, document delegated-permission threat impact, and require external removal and verification of temporary Site Collection Administrator access after it is no longer required.
- Contract sections affected: Sections 7, 8, 19, 24.
- Security impact: Reduces persistent privileged access.
- Compatibility impact: Operational procedure only; application remains read-only.
- Test impact: Production acceptance must include external access-removal evidence.
- Approval: User authorized direct documentation correction before implementation.

## New decision template

```text
### D-NNN — Title

- Date: YYYY-MM-DD UTC
- Status: PROPOSED
- Context:
- Decision:
- Contract sections affected:
- Security impact:
- Compatibility impact:
- Test impact:
- Approval:
```