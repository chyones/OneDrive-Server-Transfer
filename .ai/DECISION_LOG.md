# Active Decision Log

Only approved active decisions are kept in this working file. Superseded and rejected decisions remain available in Git history and merged pull requests; agents must not use repository history as active requirements.

Use UTC dates. Add a new entry only for a material owner-approved decision that is not already fixed by `IMPLEMENTATION_CONTRACT.md`.

## Platform and product

- **D-001:** C# .NET 10 LTS WPF application for Windows Server 2019.
- **D-002:** Destination is local fixed or directly attached storage only.
- **D-003:** Microsoft 365 access is read-only delegated interactive MSAL; no app-only or write permissions.
- **D-004:** One employee business OneDrive root per run.
- **D-005:** Maximum three simultaneous downloads; not configurable.
- **D-006:** Start with `StateSchemaVersion = 1` and `PathMappingVersion = 1`.
- **D-007:** Documentation Ready, Source Implementation Complete, and Production Ready require different evidence.
- **D-008:** Repository root is the project root.
- **D-009:** Commit small redacted evidence summaries; keep raw output local or in CI artifacts.
- **D-010:** Calculate streaming local SHA-256 and keep it separate from Microsoft source hashes.
- **D-011:** Revalidate destination containment during create, open, replace, and rename.
- **D-012:** Production requires restricted NTFS access and BitLocker, approved equivalent, or approved exception.
- **D-014:** Temporary OneDrive administrative access is managed outside the application and removed when no longer required.

## State, source, and workflow

- **D-016:** SQLite at `_TransferReport/TransferState.db` is the operational state store.
- **D-017:** Use Graph drive delta for initial inventory and up to three reconciliation passes.
- **D-018:** Bind destination to tenant ID, employee object ID, and source drive ID.
- **D-020:** Validate tenant and optional authorized-operator object-ID allowlist.
- **D-021:** Graph package items, including OneNote, are `Unsupported` and cause `Incomplete` unless a more severe state applies.
- **D-022:** Require known remaining bytes plus a fixed 5 GiB free-space reserve.
- **D-023:** Preserve supported source timestamps; timestamp-only failures are warnings.
- **D-025:** Use deterministic `PathMappingVersion = 1` rules.
- **D-026:** Validate SQLite integrity, back up before migration, migrate transactionally, and never silently reset corrupt state.
- **D-027:** Accept employee UPN or OneDrive root URL and require `Scan` before `Start Copy`.
- **D-028:** Employee credentials and employee impersonation are prohibited.
- **D-029:** Run states are `InProgress`, `Completed`, `CompletedWithWarnings`, `Incomplete`, `Failed`, `Cancelled`, and `Interrupted`.
- **D-030:** Residual path collisions expand deterministic suffixes from 10 to 20 characters and then full SHA-256.
- **D-031:** Implement `docs/REPORT_SCHEMA.md`; SQLite is operational authority and reports are audit output.
- **D-032:** Durable employee identity uses tenant, employee object, and drive IDs; operator identity is audit data, not permanent binding.

## Microsoft platform controls

- **D-033:** Prefer WAM with MSAL system-browser fallback; prohibit embedded-browser exception unless separately approved, ROPC, device code, client credentials, certificates, managed identity, and employee authentication.
- **D-034:** Use Graph `v1.0` only and the approved endpoint-permission matrix.
- **D-035:** Treat next and delta links as opaque; handle supported `410 Gone` with fresh enumeration and reconciliation.
- **D-036:** Exactly one layer owns automatic retry per request category; respect `Retry-After` and record protected correlation IDs.
- **D-037:** Temporary download URLs use a separate unauthenticated client, are never logged or persisted, and resume only after valid `206` and `Content-Range`.
- **D-038:** Prefer `quickXorHash` when available, ignore Graph `sha256Hash`, and keep Microsoft source hashes separate from local SHA-256.
- **D-039:** Self-contained releases must be rebuilt for runtime patches and record exact platform versions, source commit, and artifact hash.
