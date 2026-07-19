# Security and Integrity Requirements

This document operationalizes `IMPLEMENTATION_CONTRACT.md` without expanding the user workflow.

## Microsoft 365 access

- Use interactive delegated MSAL authentication for the authorized IT operator.
- Use a single-tenant public-client application with no client secret.
- Request read scopes only.
- Validate that the signed-in account belongs to the configured tenant.
- Enforce the configured allowlist of authorized transfer-account Entra object IDs when present.
- Do not authorize by display name or mutable email address alone.
- Never upload, modify, move, rename, or delete OneDrive content.
- Grant and remove Site Collection Administrator access outside the application.
- Use a dedicated transfer administrator account where practical.

## Employee credential prohibition

- Never request, collect, store, log, transmit, or process an employee password.
- Never authenticate as the employee.
- Never present an employee-password field or configuration value.
- Accept only employee UPN or OneDrive root URL as source-identification input.
- Record the signed-in IT operator as the actor for the archive operation.
- Add automated checks that fail when employee-password models, controls, configuration keys, or logging paths are introduced.

## Token and temporary URL protection

- Protect application-owned persistent token-cache data with Windows DPAPI.
- Restrict token-cache files to the executing Windows account and authorized administrators.
- Never log or persist access tokens, refresh tokens, cookies, authorization headers, or temporary download URLs.
- Use a separate unauthenticated HTTP path or client for temporary download URLs so Graph credentials cannot be attached.
- Clearing the application token cache must not be described as clearing every browser or Windows Microsoft session.

## Source resolution and mandatory scan

- Accept one employee UPN or one employee OneDrive for Business root URL.
- Resolve the final source to Tenant ID, employee Entra object ID, and source Drive ID.
- Treat normalized employee UPN as a display and audit value, not the sole durable identity.
- Reject unknown or external users, unprovisioned OneDrive, consumer OneDrive, files, subfolders, shared links, SharePoint libraries, and Teams libraries.
- Require a successful `Scan` before enabling `Start Copy`.
- Changing source or destination input invalidates the previous scan.
- The scan must not download employee file content.
- The scan must validate source, path mapping, destination binding, lock availability, write access, and storage capacity.
- The scan result must not claim that an archive was created.

## Source-item classification

- Copy only Graph items with supported file or folder semantics.
- Classify OneNote notebooks and other package-facet items as `Unsupported` in version 1.
- Never silently skip a package item or claim that it was copied.
- Include every unsupported item in the per-run reports and final `Incomplete` result.
- Do not traverse external shortcut content belonging to another drive.

## Destination source binding

Every destination must be bound to one source using:

- Tenant ID;
- source Drive ID; and
- employee Entra object ID.

Record the authenticated IT operator identity for audit. Do not bind the archive permanently to one operator. Another authorized operator may resume only when tenant, employee, source drive, destination, SQLite state, and current authorization all match.

Reject a destination when its stored source identity differs from the current source. Do not adopt a non-empty destination without valid application state.

## Local data integrity

- Calculate and persist local SHA-256 for every completed file.
- Revalidate local SHA-256 before trusting recovered `Completed` state.
- Store supported Microsoft source-hash information separately from local SHA-256.
- Never describe size and metadata validation as source cryptographic verification.
- Keep all hash operations streaming and bounded in memory.
- Preserve source creation and modification timestamps where Windows supports the values.
- Record timestamp failures and return `CompletedWithWarnings` only when every supported item was copied or validly skipped.
- Return `Incomplete` when supported items fail, unsupported items exist, or the source cannot be stabilized.

## Destination containment and path mapping

- Accept local fixed or directly attached storage only.
- Reject UNC, mapped, NAS, SMB, and remote destinations.
- Reject Windows system directories and application installation directories.
- Canonicalize the selected destination.
- Implement exactly the binding `PathMappingVersion = 1` rules.
- Expand the deterministic source-item hash suffix from 10 to 20 characters and then to the full hash if a collision remains.
- Persist and reuse every source-to-local mapping.
- Revalidate containment during create, open, replace, and rename operations.
- Prevent path traversal and unsafe symbolic-link, junction, mount-point, and reparse-point redirection.
- Do not follow or overwrite untrusted hard-linked destination files.
- Fail safely when path identity changes during an operation.

## Storage capacity and failure safety

- Determine destination-volume free space during scan and before scheduling downloads.
- Require known remaining bytes plus the fixed 5 GiB safety reserve.
- Recheck before each file when the total is incomplete or changes.
- On disk-full or reserve failure, stop new scheduling, preserve verified files and valid partial files, persist accurate state, and never return `Completed` or `CompletedWithWarnings`.

## Local state protection

Store transfer state in:

```text
SelectedDestination\_TransferReport\TransferState.db
```

- Use SQLite transactions and durable incremental commits.
- Keep SQLite as the operational source of truth for scan, resume, recovery, and source binding.
- Do not use CSV or JSON reports as the operational state database.
- Do not store tokens, passwords, cookies, temporary URLs, or file contents in the state database.
- Run SQLite integrity validation before resuming an existing destination.
- Back up the protected state database before supported schema migrations.
- Run migrations transactionally.
- Reject unsupported future schema versions.
- Keep recovery idempotent.
- On corruption or failed migration, preserve diagnostic copies and fail without silently resetting state or adopting existing content.
- Restrict access to `_TransferReport` and `OneDriveData` through NTFS permissions.

## Source changes

- Preserve item identity through Microsoft Graph Drive Item ID.
- Handle source rename and move transactionally without unexplained duplicate output.
- Never delete a local archived file solely because the source item was deleted.
- Record source deletion and retained-local-content conditions in state and reports.
- Use at most three bounded reconciliation passes.
- Return `Incomplete` when the source does not stabilize.

## Reports and run isolation

- Store each copy run under `_TransferReport\Runs\<RunId>`.
- Never overwrite or append to another run's report files.
- Implement `docs/REPORT_SCHEMA.md`.
- Report the dry-run summary, unsupported items, failed items, timestamp warnings, storage failures, reconciliation warnings, and exact terminal run state.
- Keep normal UI errors simple and reference-coded.
- Keep technical details in protected logs.
- Redact account, tenant, and path details where full values are unnecessary.
- Prevent CSV formula injection for untrusted values.
- Do not commit production logs, reports, state databases, or employee data.
- Never place passwords, tokens, cookies, authorization headers, temporary download URLs, or raw Graph responses in reports.

## Storage and endpoint protection

Production validation must record that:

- the application runs under an authorized Windows account;
- employee archive data is not broadly readable through inherited ACLs;
- application and token-cache files are not readable by unrelated normal users;
- the archive volume uses BitLocker, an approved organizational equivalent, or an approved exception; and
- antivirus, EDR, firewall, and application-control protections were not broadly disabled.

## Release controls

Before an approved internal release:

- use deterministic dependency restore;
- run automated tests and Windows Release build;
- run dependency-vulnerability and secret scans;
- generate an SBOM where supported;
- tie the published output to an exact source commit;
- apply Authenticode signing when an approved organizational certificate is available; and
- document the approved limitation when signing is unavailable.
