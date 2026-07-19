# OneDrive Server Transfer — Binding Implementation Contract

## 1. Product purpose

Build a simple internal Windows desktop application that allows an authorized IT administrator to copy the active files and folders from one employee's Microsoft 365 OneDrive for Business root to local storage attached to the same Windows Server on which the application runs.

The application is a read-only backup-copy utility. It must never upload, edit, rename, move, or delete Microsoft 365 content and must never grant, change, or remove Microsoft 365 permissions.

## 2. Exact user workflow

The application uses one window. The IT administrator performs only these actions:

1. Open the application.
2. Sign in through the official Microsoft sign-in flow.
3. Paste the employee's OneDrive for Business root URL.
4. Select a local destination on the same Windows Server.
5. Review the resolved employee, signed-in transfer account, and destination confirmation.
6. Press `Copy Data`.
7. Monitor progress and review the final result.

Do not add dashboards, advanced settings, batch employee processing, scheduling, service mode, remote administration, or unnecessary technical steps.

## 3. Platform and technology

Use:

- C#
- .NET 10 LTS
- WPF
- MVVM
- Microsoft Graph v1.0
- Microsoft.Identity.Client (MSAL)
- dependency injection
- structured local logging
- automated tests
- embedded local SQLite transfer state

Target Windows Server 2019 x64 with Desktop Experience. Publish self-contained for `win-x64`.

The repository root is the project root:

```text
./OneDriveServerTransfer.sln
./src/OneDriveServerTransfer.App
./tests/OneDriveServerTransfer.Tests
./scripts
./docs
./artifacts
```

## 4. Authentication and Microsoft access

Use interactive delegated authentication through MSAL with MFA and Conditional Access support.

Use a single-tenant public-client Entra application. Do not create or use a client secret.

Approved delegated scopes:

```text
User.Read
Files.Read.All
Sites.Read.All
offline_access
openid
profile
```

Do not request Microsoft 365 write permissions.

After sign-in, verify that the authenticated account belongs to the configured tenant. Support a deployment allowlist of authorized transfer-account Microsoft Entra object IDs. When the allowlist is configured, reject every account not present in it. Do not authorize an account by display name alone, and do not treat a mutable email address as the sole durable identity.

The IT administrator grants the designated transfer account access to the employee's OneDrive outside the application, including temporary Site Collection Administrator access when operationally required. The application only validates existing access.

When temporary Site Collection Administrator access is used, the external operating procedure must require:

1. removal after the completed, failed, cancelled, or abandoned transfer no longer requires it;
2. verification of removal through Microsoft 365 administration tools; and
3. an external record containing the transfer run ID, grant time, removal time, responsible administrator, and verification result.

Production acceptance requires evidence that this removal and verification procedure was completed for the production test.

Protect the application-owned persistent token cache with Windows DPAPI for the current Windows user. `Remember sign-in` controls only the application's persistent cache and must not claim to clear every Microsoft browser or Windows session.

## 5. Accepted source and scope

Accept only the root URL of one employee's OneDrive for Business in the configured tenant.

Validate before copying:

- HTTPS URL;
- configured tenant OneDrive host;
- employee personal-site URL;
- default Microsoft Graph drive root;
- `driveType = business`;
- actual drive root rather than a file or subfolder; and
- signed-in administrator read access.

Reject:

- single-file or subfolder URLs;
- shared-file or shared-folder URLs;
- consumer OneDrive;
- SharePoint, Teams, project-site, or communication-site libraries; and
- sources outside the configured tenant.

Copy active file items, nested folder items, and empty folders, including Arabic, English, Unicode, large, and long-name files.

Microsoft Graph package items, including OneNote notebooks, are not file or folder items and are not copied in version 1. Classify them as `Unsupported`, include them in the reports, and force the run result to `CompletedWithWarnings` unless another condition requires `Failed`, `Cancelled`, or `Interrupted`. Never silently skip a package item or claim that it was copied. Exporting or reconstructing package content requires a separately approved future scope.

Do not include Recycle Bin content, deleted-item history, previous versions, sharing metadata, comments, activity, retention, compliance, audit records, or external shortcut content belonging to another drive.

## 6. Destination rules

The administrator may select any local fixed or directly attached storage on the same Windows Server.

Reject UNC paths, mapped drives, network shares, NAS, SMB, remote-server destinations, and unsafe reparse-point redirection.

Create:

```text
SelectedDestination\
├── OneDriveData\
└── _TransferReport\
```

Store employee content only in `OneDriveData`. Store transfer state, reports, and logs only in `_TransferReport`.

Bind each destination to at least Tenant ID, source Drive ID, and a protected employee identity. Reject a destination bound to another source. Do not silently adopt a non-empty destination without valid application state.

Acquire an operating-system-backed exclusive lock so two processes or Windows sessions cannot use the same destination concurrently.

Before scheduling downloads, determine destination-volume free space and calculate the known remaining source bytes. Require free space greater than the known remaining bytes plus a fixed 5 GiB safety reserve. When the total is incomplete or changes during reconciliation, recheck before each file and require at least that file's expected size plus the reserve. A disk-full or reserve violation must stop new scheduling safely, preserve verified files and valid partial files, persist accurate state, and never return `Completed`.

## 7. Inventory and source changes

Use the officially supported Microsoft Graph v1.0 drive delta flow for the initial complete inventory and subsequent reconciliation.

Process `@odata.nextLink` page by page until `@odata.deltaLink` is reached. Persist the delta checkpoint safely.

Use bounded asynchronous queues and backpressure. Do not materialize the complete drive hierarchy in memory.

After the initial copy, process changes through the saved delta state. Use at most three bounded reconciliation passes. When the source does not stabilize, complete safe work and return `CompletedWithWarnings` rather than claiming a stable snapshot.

A source deletion must never automatically delete an already copied local file.

## 8. File transfer behavior

- Use streaming downloads.
- Use a fixed maximum of three simultaneous file downloads.
- Keep concurrency absent from the UI and deployment configuration.
- Download first to `filename.ext.partial`.
- Commit the final name only after verification.
- Resume only when the temporary host returns valid `206 Partial Content` and matching range metadata.
- Restart from byte zero when a range request returns `200 OK` or invalid range metadata.
- Obtain fresh temporary download URLs when needed.
- Never persist or log temporary download URLs.
- Never send Graph bearer tokens, cookies, or Graph-specific authorization headers to temporary download hosts.
- Respect `Retry-After` and retry transient failures with bounded backoff, up to five attempts per file.
- Do not stop unrelated files because one file failed when continuation is safe.
- On cancellation, stop new scheduling, cancel supported requests, preserve completed files, and preserve safe partial files.
- After content verification, preserve the source `createdDateTime` and `lastModifiedDateTime` on the local file when Windows supports the values. Apply directory timestamps after child processing. Timestamp failures do not invalidate verified bytes, but they must be recorded and force `CompletedWithWarnings`.

## 9. Verification and existing files

For every completed file:

1. confirm successful HTTP completion;
2. confirm written bytes equal the expected source size;
3. re-read source metadata and verify source identity and relevant metadata remained stable;
4. verify a supported Microsoft source hash when available;
5. calculate and store a streaming local SHA-256; and
6. apply and verify source timestamps when supported.

Store source-hash verification and local SHA-256 separately. Never claim source cryptographic verification when Microsoft Graph did not provide a comparable source hash.

Skip an existing file only when transfer state proves it represents the same source item and its metadata and recorded local SHA-256 remain valid.

Never overwrite unrelated local content. Generate a deterministic safe name and report the conflict when required.

## 10. Local state, run states, and recovery

Use one application-owned SQLite database:

```text
SelectedDestination\_TransferReport\TransferState.db
```

SQLite is embedded and requires no database server. Use it for source binding, lookup, resume, crash recovery, path collision handling, reporting state, and the delta checkpoint.

Store at minimum:

- schema version, path-mapping version, and run ID;
- tenant, source drive, authenticated transfer-account identity, and protected employee identity;
- source item and parent IDs;
- source and mapped local paths;
- item facet classification, including unsupported package items;
- ETag or CTag when available;
- source size, created time, and modified time;
- supported source-hash information;
- local SHA-256;
- transfer state and attempt count;
- timestamp-preservation result;
- delta checkpoint; and
- timestamps and final result.

Use transactions and durable incremental commits. Recovery must be idempotent. Reject unsupported future schema versions clearly.

Before opening an existing destination for resume, run SQLite integrity validation. Before every supported schema migration, create a protected backup of the state database and execute the migration transactionally. If integrity validation or migration fails, preserve the original database with a timestamped diagnostic copy, do not silently reset or rebuild state, do not overwrite existing employee content, and require restoration of a known-good state database or selection of a new empty destination.

Approved item states:

```text
Discovered
Mapped
Downloading
Verified
Completed
Skipped
Unsupported
Failed
Cancelled
```

Approved run states:

```text
InProgress
Completed
CompletedWithWarnings
Failed
Cancelled
Interrupted
```

Run-state rules:

- `Completed`: every supported item is `Completed` or validly `Skipped`, no item is `Failed` or `Unsupported`, required timestamps succeeded, and reconciliation reached a stable delta state.
- `CompletedWithWarnings`: safe work finished but at least one item is `Failed` or `Unsupported`, a timestamp could not be preserved, or the source did not stabilize within three reconciliation passes.
- `Failed`: source validation, destination validation, binding, locking, state integrity, storage safety, or another fatal requirement prevented safe continuation.
- `Cancelled`: the administrator requested cancellation and state was persisted safely.
- `Interrupted`: a previous `InProgress` run ended without an orderly terminal transition and remains eligible for validated resume.

## 11. Path safety and `PathMappingVersion = 1`

Use deterministic Windows-safe path mapping with `PathMappingVersion = 1`.

For version 1:

1. Normalize every source path component to Unicode Normalization Form C.
2. Encode Windows-invalid characters, ASCII control characters, trailing dots, and trailing spaces as `_xHHHH_` using the uppercase four-digit UTF-16 code unit value.
3. Prefix Windows reserved device names with `_` after normalization and encoding.
4. When the mapped component would be empty, use `_empty_` followed by the deterministic collision suffix.
5. Detect collisions using ordinal case-insensitive comparison and treat file-versus-folder conflicts as collisions.
6. Derive the collision suffix as `~` followed by the first 10 lowercase hexadecimal characters of SHA-256 over the UTF-8 source Drive Item ID. Insert the suffix before the file extension when practical and append it to folder names.
7. Limit each mapped component to 200 UTF-16 code units. When longer, truncate only the human-readable portion and retain the deterministic suffix and as much of the extension as fits.
8. Use long-path-capable Windows APIs. If the canonical final path still exceeds the supported runtime or filesystem limit, fail that item with a stable path-length error instead of shortening it non-deterministically.
9. Persist every mapping in SQLite and reuse it on resume and rerun.

Validate canonical containment during create, open, replace, and rename operations. Prevent traversal and unsafe symbolic-link, junction, mount-point, or reparse-point redirection. Do not follow or overwrite untrusted hard-linked destination files.

Changing any mapping rule requires a new `PathMappingVersion` and an approved compatibility and migration decision.

## 12. User interface

Create one professional WPF window containing only:

- Microsoft sign-in, signed-in account, remember sign-in, and sign-out;
- employee OneDrive URL;
- destination selector;
- resolved employee, authorized transfer account, and destination confirmation;
- `Copy Data` and `Cancel`;
- current operation and current file;
- discovered, completed, skipped, unsupported, and failed counts;
- downloaded size;
- progress bar; and
- a bounded recent-activity list.

While totals are unknown, show `Calculating` or `Unknown` and use indeterminate progress. Never fabricate a percentage.

Every user-facing error must contain a short title, plain-language explanation, corrective action, and stable reference code. Never display tokens, temporary URLs, raw Graph responses, stack traces, authorization headers, tenant IDs, drive IDs, or protected database values.

## 13. Reports

Create a unique report directory for every run:

```text
SelectedDestination\_TransferReport\Runs\<RunId>\
├── TransferSummary.json
├── TransferReport.csv
├── FailedFiles.csv
└── TransferLog.log
```

Never overwrite or append to another run's report files. SQLite remains the operational source for recovery. CSV and JSON files are human-readable reports only.

Reports must include unsupported package items, timestamp warnings, disk-space failures, source-instability warnings, and the exact terminal run state.

Use UTF-8, correct CSV escaping, and spreadsheet-formula-injection protection. Segment reports internally only when practical file-size limits require it.

## 14. Security and production storage

- Microsoft 365 access remains read-only.
- Do not commit or log passwords, client secrets, tokens, cookies, temporary download URLs, employee content, production state databases, logs, or reports.
- Protect token-cache and backup data with restricted NTFS permissions.
- Warn or fail when destination permissions expose employee data broadly.
- Do not disable antivirus, EDR, firewall, or application-control protections.
- Production backup storage must use BitLocker or an approved organizational equivalent. When encryption is unavailable, Production Ready status requires a documented organizational exception approved before use.

## 15. Tests and validation

Automated tests must cover URL validation, tenant and authorized-account validation, root resolution, rejected source types, package-item classification, delta paging and recovery, external shortcut rejection, destination validation and binding, exclusive locking, disk headroom and disk-full recovery, path-mapping version 1, streaming and fixed concurrency, range resume and safe restart, temporary URL credential isolation, retries and throttling, source changes during download, source-hash and local SHA-256 behavior, timestamp preservation, SQLite transactions, integrity checks, migration backup and recovery, existing-file conflicts, run-state rules, per-run report isolation, cancellation, CSV safety, and error redaction.

Windows CI is mandatory before `Source Implementation Complete`. Required CI evidence includes:

- dependency restore on compatible Windows;
- Release build of the Windows-targeted solution;
- automated tests on Windows;
- formatting or static-analysis checks;
- dependency vulnerability review; and
- secret detection.

A non-Windows development environment may perform additional supported checks, but it cannot replace mandatory Windows CI build and test evidence.

Production acceptance additionally requires actual Windows Server 2019 execution, WPF startup, Microsoft interactive sign-in with an authorized transfer account, real test-employee OneDrive validation, complete copy, unsupported-package reporting when present, interruption and resume, reconciliation, destination locking, disk-space behavior, timestamp preservation, production ACL and encryption validation, access-removal verification, and self-contained publish.

## 16. Deliverables

- complete solution and source;
- WPF application and automated-test projects;
- `appsettings.example.json`;
- Windows build and publish scripts;
- README and operating instructions;
- enforceable GitHub Actions checks;
- committed redacted milestone evidence summaries; and
- self-contained `win-x64` publish after successful Windows validation.

## 17. Completion labels

- `Documentation Ready`: one binding contract and aligned controls exist, no unresolved binding contradiction remains, implementation has not started, and committed documentation evidence is tied to an exact validated commit.
- `Source Implementation Complete`: complete source exists, mandatory Windows CI restore/build/tests and other required source checks passed, and committed evidence exists. Real-tenant and interactive production checks may remain unexecuted.
- `Production Ready`: every mandatory Windows Server, real-tenant, transfer, resume, security, access-removal, encryption, and publish test passed with evidence.
- `Not Complete`: mandatory implementation or validation is missing.

Never infer success from an unexecuted command, checked box, ignored local file, mutable branch name, or verbal claim.

## 18. Explicitly out of scope

- batch processing of multiple employees;
- employee directory or user-management screens;
- scheduled or unattended transfers;
- Windows Service mode;
- application-only authentication;
- synchronization or mirroring;
- source deletion, modification, or permission management;
- network, NAS, SMB, UNC, or remote destinations;
- web dashboard or central reporting;
- email notifications;
- previous-version, Recycle Bin, sharing, compliance, or audit backup;
- exporting or reconstructing OneNote notebooks or other Microsoft Graph package items;
- custom five-million-item benchmark as a release blocker; and
- custom database-engine or JSONL-index implementation.

These features require a separately approved future scope.
