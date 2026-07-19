# OneDrive Server Transfer — Binding Implementation Contract

## 1. Product purpose

Build a simple internal Windows desktop application that allows an authorized IT administrator to copy the active contents of one employee's Microsoft 365 OneDrive to local storage attached to the same Windows Server on which the application is running.

The product exists to reduce the operation to a small number of clear steps. It is not a synchronization platform, document-management system, backup portal, or Microsoft 365 administration suite.

The application is read-only against Microsoft 365. It must never upload, edit, rename, move, or delete source OneDrive content and must never grant or remove Microsoft 365 permissions.

## 2. Exact user workflow

The application uses one window. The IT administrator performs only these actions:

1. Open the application.
2. Sign in through the official Microsoft sign-in flow.
3. Paste the employee's OneDrive for Business root URL.
4. Select a local destination folder on the same Windows Server.
5. Press `Copy Data`.
6. Monitor progress.
7. Review the final result and report.

Before copying, show a short confirmation containing the resolved employee identity, validation result, and destination. Do not expose Graph IDs or other implementation details.

## 3. Platform and fixed technology

Use:

- C#
- .NET 10 LTS
- WPF
- MVVM
- Microsoft Graph v1.0
- Microsoft.Identity.Client (MSAL)
- Dependency injection
- Structured local logging
- Automated tests

Target Windows Server 2019 x64 with Desktop Experience. Publish self-contained for `win-x64`; the production server must not require a separate .NET installation.

The repository root is the project root. Create:

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

Use a single-tenant public-client Entra application registration. Do not create or use a client secret.

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

The IT administrator grants the designated transfer account access to the employee's OneDrive outside the application, including Site Collection Administrator access when operationally required. The application only validates existing access.

Persistent application-owned token-cache data must be protected with Windows DPAPI for the current Windows user. `Remember sign-in` controls only the application's persistent token cache. The UI must not claim that clearing the application cache also clears every Microsoft browser or Windows session.

## 5. Accepted source and backup scope

Accept only the root URL of one employee's OneDrive for Business in the configured tenant.

Validate before copying:

- URL is present and uses HTTPS.
- Host matches the configured tenant OneDrive host.
- URL resolves to an employee personal site.
- Default drive resolves through Microsoft Graph v1.0.
- Resolved drive is `driveType = business`.
- Resolved item is the drive root.
- Signed-in administrator can enumerate and download the drive.

Reject:

- single-file URLs
- subfolder URLs
- shared-file or shared-folder URLs
- consumer OneDrive
- SharePoint, Teams, project-site, or communication-site libraries
- a source outside the configured tenant

Copy the active files and folders physically belonging to the validated drive, including nested folders, empty folders, Arabic names, Unicode names, and large files.

Do not include:

- Recycle Bin content
- deleted-item history
- previous versions
- sharing permissions or links
- comments or activity history
- retention, compliance, or audit records
- external `remoteItem` or shortcut content belonging to another drive

## 6. Destination rules

The administrator may select any local folder on storage attached to the same Windows Server.

Allow local fixed or directly attached storage only. Reject:

- UNC paths
- mapped network drives
- network shares
- NAS or SMB destinations
- remote-server destinations
- paths redirected outside the selected destination through symbolic links, junctions, mount points, or other unsafe reparse points

Create:

```text
SelectedDestination\
├── OneDriveData\
└── _TransferReport\
```

Store employee content only in `OneDriveData`. Store transfer state, reports, and logs only in `_TransferReport`.

Bind a destination to the source using at least Tenant ID, source Drive ID, and protected employee identity. A destination already bound to another employee, tenant, or drive must be rejected. A non-empty destination without valid application state must not be adopted silently.

Acquire an exclusive operating-system-backed lock for the canonical destination so two processes or Windows sessions cannot use the same destination concurrently.

## 7. Initial inventory and source changes

Use the officially supported Microsoft Graph v1.0 drive delta flow for the initial complete drive inventory and for subsequent change reconciliation.

Process `@odata.nextLink` page by page until the initial inventory reaches an `@odata.deltaLink`. Persist the delta checkpoint safely.

Do not materialize the complete drive hierarchy in memory. Use bounded asynchronous queues and backpressure.

After the initial copy, process source changes using the saved delta state. Repeat reconciliation only as needed, with a maximum of three bounded passes. If the source continues changing, complete the safe work and return `CompletedWithWarnings` rather than claiming a stable snapshot.

A source deletion must never automatically delete an already copied local file. Record the deletion in the report.

## 8. File transfer behavior

- Use streaming downloads; never load a complete file into memory.
- Use a fixed maximum of three simultaneous file downloads.
- Keep concurrency internal and absent from the UI and deployment configuration.
- Download first to `filename.ext.partial`.
- Commit the final filename only after successful verification.
- Support HTTP Range resume when the temporary download host returns a valid `206 Partial Content` response.
- If a range request returns `200 OK`, restart safely from byte zero; never append a full response to an existing partial file.
- Obtain a fresh temporary download URL when required. Never persist or log temporary download URLs.
- Never attach Graph bearer tokens, Graph cookies, or Graph-specific authorization headers to temporary download-host requests.
- Respect `Retry-After` for throttling and temporary service errors.
- Retry transient network or local-file errors with bounded backoff, up to five attempts per file.
- Failure of one file must not stop unrelated files when the run can continue safely.
- Cancellation stops new scheduling, cancels supported active requests, preserves completed files, and preserves safe partial files.

## 9. File verification and existing files

For every completed file:

1. Confirm successful HTTP completion.
2. Confirm the written byte count matches the expected source size.
3. Re-read current source metadata and verify source identity and relevant metadata did not change during download.
4. Verify a supported source hash when Microsoft Graph supplies one.
5. Calculate and store a streaming local SHA-256 for future local-integrity verification.

Keep source-hash verification and local SHA-256 as separate fields. Never claim source cryptographic verification when Microsoft Graph did not provide a comparable supported source hash.

A local file may be skipped only when transfer state proves it represents the same source item and its expected metadata and recorded local SHA-256 remain valid.

Never overwrite an unrelated existing file. When a local name conflicts with unrelated content, generate a deterministic safe name and record the adjustment.

## 10. Local state and resume

Use one application-owned local SQLite database:

```text
SelectedDestination\_TransferReport\TransferState.db
```

SQLite is an embedded local file and does not require a database server. It is approved to simplify reliable resume, source binding, lookup, crash recovery, path collision handling, and reporting.

Store at minimum:

- schema version
- run ID
- tenant ID
- source drive ID
- protected employee identity
- source item ID and parent ID
- source path and mapped local path
- ETag or CTag when available
- source size and modified time
- supported source hash information when available
- local SHA-256
- transfer state and attempt count
- delta checkpoint
- timestamps and final result

Use transactions and durable incremental commits. Recovery must be idempotent. Unsupported future schema versions must fail clearly rather than being misread.

Approved item states:

```text
Discovered
Mapped
Downloading
Verified
Completed
Skipped
Failed
Cancelled
```

## 11. Path mapping

Use deterministic Windows-safe path mapping with `PathMappingVersion = 1`.

Handle:

- Unicode normalization
- invalid Windows filename characters
- reserved Windows names
- trailing dots and spaces
- empty sanitized names
- case-insensitive collisions
- file-versus-folder collisions
- long paths
- deterministic collision suffixes

A rerun must use the destination's recorded mapping version. Do not silently reinterpret an existing destination using another version.

## 12. User interface

Create one simple professional WPF window containing only:

### Microsoft account

- `Sign in with Microsoft`
- signed-in account name
- `Remember sign-in`
- `Sign out`

### Transfer

- `Employee OneDrive URL`
- `Destination Folder`
- `Browse`
- `Copy Data`
- `Cancel`

### Progress

- current operation
- current file
- discovered files
- completed files
- skipped files
- failed files
- downloaded size
- overall progress
- progress bar
- bounded recent activity list

While exact totals are unknown, show `Calculating` or `Unknown` and use an indeterminate progress state. Never fabricate a percentage.

Do not add dashboards, multiple pages, advanced settings, user management, scheduling, email notifications, transfer-history portals, themes, file previews, or Microsoft 365 permission management.

## 13. Errors and reports

Every user-facing error must include:

1. short title
2. plain-language explanation
3. direct corrective action
4. stable reference code

Never display tokens, temporary URLs, raw Graph responses, stack traces, raw exception class names, or authorization headers.

Create per run:

```text
TransferSummary.json
TransferReport.csv
FailedFiles.csv
TransferLog.log
```

The state database remains the operational source for resume. CSV files are human-readable reports only.

Reports must use UTF-8, correct CSV escaping, and spreadsheet-formula-injection protection. Segment reports internally only when required by practical file-size limits; segmentation must not complicate the normal user workflow.

## 14. Required security controls

- Microsoft 365 access is read-only.
- No passwords, client secrets, tokens, cookies, or temporary download URLs are committed or logged.
- Real `appsettings.json`, employee content, state databases, logs, and production reports are not committed.
- Validate canonical destination containment during file create, open, replace, and rename operations.
- Prevent path traversal and unsafe reparse-point redirection.
- Do not follow or overwrite untrusted hard-linked destination files.
- Protect token-cache and backup data with appropriate NTFS permissions.
- Warn or fail when the destination exposes employee data broadly.
- Do not disable antivirus, EDR, firewall, or application-control protections.
- Production storage should use BitLocker or an approved organizational equivalent or exception.

## 15. Required automated and Windows tests

Automated tests must cover at minimum:

- URL and tenant validation
- employee OneDrive root resolution
- rejection of files, subfolders, consumer OneDrive, and SharePoint libraries
- delta paging and checkpoint recovery
- external shortcut rejection
- local-path and network-path validation
- destination source binding and exclusive locking
- deterministic path mapping
- streaming transfer and fixed concurrency
- range resume, invalid range, and safe restart
- temporary URL credential isolation
- retries and throttling
- source metadata changes during download
- source-hash and local SHA-256 behavior
- SQLite transaction, crash recovery, and schema-version handling
- existing-file verification and conflict handling
- cancellation and rerun
- CSV escaping and formula protection
- user-facing error redaction

Production acceptance requires actual execution on Windows Server 2019, WPF startup, Microsoft interactive sign-in, real test-employee OneDrive validation, complete copy, interruption and resume, reconciliation, destination locking, and self-contained publish.

## 16. Deliverables

- complete solution and source
- WPF application project
- automated test project
- `appsettings.example.json`
- Windows build and publish scripts
- README and operating instructions
- GitHub Actions checks for restore, Windows Release build, tests, formatting/static analysis, dependency vulnerability review, and secret detection
- committed redacted milestone evidence summaries
- self-contained `win-x64` publish after successful Windows validation

## 17. Completion labels

- `Documentation Ready`: corrected contract and project controls exist; application implementation has not started.
- `Source Implementation Complete`: source, CI, supported tests, and evidence exist; Windows-only and real-tenant checks may remain explicitly unexecuted.
- `Production Ready`: all mandatory Windows Server, real-tenant, transfer, resume, security, and publish tests passed with evidence.
- `Not Complete`: mandatory implementation or validation is missing.

Never infer success from an unexecuted command, a checked box, an ignored local file, or a verbal claim.

## 18. Explicitly out of scope

- batch processing of multiple employees
- employee directory or user-management screens
- scheduled or unattended transfers
- Windows Service mode
- application-only authentication
- synchronization or mirroring
- source deletion or modification
- source permission management
- network, NAS, SMB, UNC, or remote destinations
- web dashboard or central reporting
- email notifications
- previous-version, Recycle Bin, sharing, compliance, or audit backup
- custom five-million-item benchmark as a release blocker
- custom database-engine or JSONL-index implementation

These features require a separately approved future scope.