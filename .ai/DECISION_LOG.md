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
