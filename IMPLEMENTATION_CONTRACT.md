# OneDrive Server Transfer

## Project Status

This document is the fixed implementation contract.

The implementation agent must build the complete application in one execution.

Do not ask the user to choose technologies, architecture, libraries, UI style, authentication flow, retry behavior, or project structure.

Do not add unrequested features.

Resolve only operational or technical issues required to make the defined application work.

---

## 1. Project Purpose

Build a native Windows desktop application used by the IT department to create a complete local backup of one employee's Microsoft 365 OneDrive.

In this contract, `complete local backup` means the complete in-scope active file-and-folder content defined in Section 8. It does not include excluded service history, previous versions, sharing metadata, compliance data, or Recycle Bin content.

The application is installed and executed directly on the target Windows Server 2019 machine.

The transfer flow is:

```text
Employee personal Microsoft 365 OneDrive
        ↓ HTTPS through Microsoft Graph
OneDrive Server Transfer application
        ↓
Local storage attached to the same Windows Server
```

Before starting the transfer, the IT administrator manually grants the designated administrator account Site Collection Administrator access to the employee's personal OneDrive.

The application then signs in using that administrator account, validates access to the employee's OneDrive root, and downloads the complete accessible OneDrive folder structure and files to local server storage.

The application must not grant, change, or remove Microsoft 365 permissions.

The application must never delete, move, rename, upload, or modify source OneDrive data.

---

## 2. Target Environment and Deployment Model

### Development environment

- The source project may be created from macOS.
- WPF cannot be executed or visually validated on macOS.
- The implementation agent must still generate the complete Windows solution.
- Perform every build, restore, static validation, and unit test supported by the current environment.
- Do not claim that the WPF interface was executed on macOS.

### Runtime environment

- Windows Server 2019 x64.
- Windows Server must include Desktop Experience.
- Application type: native Windows desktop application.
- The application is not a website.
- The application does not require IIS, a domain, SSL, or web hosting.

### Deployment and transfer model

The application must be installed and executed directly on the target Windows Server 2019 machine.

The transfer flow is:

```text
Employee Microsoft 365 OneDrive
        ↓ HTTPS through Microsoft Graph
Windows Server running this application
        ↓
Local storage attached to the same Windows Server
```

The application downloads files directly from Microsoft OneDrive to a local destination on the Windows Server where the application is running.

The application is not a remote transfer controller and must not transfer files between separate internal servers.

Supported destination examples:

```text
D:\OneDriveArchive\EmployeeName
```

```text
E:\EmployeeArchives\EmployeeName
```

The current version must not support:

- UNC paths.
- Network shares.
- NAS destinations.
- Remote server destinations.
- SMB destination recovery.
- Multi-server transfer.
- Remote transfer agents.

### Deployment

- Publish as self-contained `win-x64`.
- The server must not require a separate .NET installation.
- Reliability is more important than publishing as one single executable.

---

## 3. Fixed Technology

Use only:

- C#
- .NET 10 LTS
- WPF
- MVVM
- Microsoft Graph v1.0
- Microsoft.Identity.Client MSAL
- Dependency injection
- Structured file logging
- Automated unit tests

Do not replace WPF with:

- WinForms
- MAUI
- Electron
- Web technologies
- PowerShell
- Python
- Java
- Another desktop framework

PowerShell may only be used for build, publish, packaging, or launch helper scripts.

### Tooling compatibility decision

Keep the target framework as `.NET 10 LTS`.

Do not downgrade the project to `.NET 8`.

The project must be buildable through the .NET 10 SDK and `dotnet` command-line tools on a compatible Windows build environment.

Do not make Visual Studio 2022 a project requirement.

A compatible current Visual Studio release may be used when an IDE is required, but the documented build and publish process must work through the .NET 10 SDK command line.

The published application remains self-contained, so the production Windows Server does not need Visual Studio and does not need a separately installed .NET runtime.

---

## 4. Fixed Project Location

Create the complete project only inside:

```text
./OneDriveServerTransfer
```

The solution file must be:

```text
./OneDriveServerTransfer/OneDriveServerTransfer.sln
```

Expected structure:

```text
OneDriveServerTransfer/
├── OneDriveServerTransfer.sln
├── src/
│   └── OneDriveServerTransfer.App/
├── tests/
│   └── OneDriveServerTransfer.Tests/
├── scripts/
├── docs/
├── artifacts/
│   ├── source/
│   └── win-x64/
├── appsettings.example.json
├── README.md
└── .gitignore
```

Do not create or modify files outside `./OneDriveServerTransfer`.

The complete source deliverable is the `./OneDriveServerTransfer` project directory itself.

The `./OneDriveServerTransfer/artifacts/source` directory must contain only generated source-validation artifacts, such as:

- Restore result.
- Test result.
- Static-validation result.
- Source inventory.
- Validation summary.
- Synthetic benchmark reports produced before Windows production validation.

Do not copy or recursively duplicate the complete project into `artifacts/source`.

A successfully published Windows application must be placed inside:

```text
./OneDriveServerTransfer/artifacts/win-x64
```

Creating a ZIP archive is optional and must not be required for source implementation completion or production acceptance.

If an optional ZIP archive is created, it must remain inside `./OneDriveServerTransfer/artifacts`, contain the complete intended deliverable, and be verified after creation.

Recommended installation location on Windows Server 2019:

```text
C:\OneDriveServerTransfer
```

The employee-data destination is not fixed. The administrator selects it inside the application.

Examples:

```text
D:\OneDriveArchive\EmployeeName
```

```text
E:\EmployeeArchives\EmployeeName
```

---

## 5. Exact User Workflow

The application must use one window.

The administrator performs only these actions:

1. Opens the application.
2. Signs in through the official Microsoft sign-in window.
3. Optionally keeps the Microsoft session saved.
4. Pastes the employee's OneDrive URL.
5. Selects a destination folder on Windows Server.
6. Presses `Run`.
7. Watches progress until completion.
8. Reviews the final summary and generated reports.

Do not create username or password fields inside the application.

Never store the Microsoft administrator password.

Saving the sign-in means storing only an encrypted MSAL token cache for the current Windows user.

---

## 6. User Interface and User-Facing Errors

### User interface principle

The application must provide one simple, professional, and operationally clear window intended for an IT employee.

The interface must avoid unnecessary technical information, configuration options, or implementation details.

The IT employee must only perform these actions:

1. Sign in with Microsoft.
2. Paste the employee OneDrive URL.
3. Select a local destination folder on the server.
4. Press `Run`.
5. Monitor the transfer.
6. Review the final result.

The interface must not expose:

- Microsoft Graph terminology.
- Tenant-resolution details.
- Drive IDs.
- Item IDs.
- ETags or CTags.
- HTTP status codes as the primary error message.
- Retry counters.
- Concurrency controls.
- Manifest controls.
- Authentication token information.
- Technical stack traces.
- Internal service names.
- Advanced settings.

Technical information may be written to the protected application log but must not clutter the interface.

### Exact simple interface

Create one clean and professional WPF window containing only the following elements.

#### Microsoft account

- `Sign in with Microsoft`.
- Signed-in account name and email.
- `Remember sign-in`.
- `Sign out`.

#### Transfer

- `Employee OneDrive URL`.
- `Destination Folder`.
- `Browse`.
- `Run`.
- `Cancel`.

#### Progress

Show only:

- Current operation.
- Current file.
- Discovered files.
- Completed files.
- Skipped files.
- Failed files.
- Downloaded size.
- Overall progress.
- Progress bar.
- A compact activity area showing only recent important events.

The activity area must use a bounded number of visible entries and must not retain the complete transfer log in memory.

When the exact total item count or total size is not yet known:

- The progress bar must use an indeterminate `Discovery` state.
- The overall progress value must display `Calculating` or `Unknown`.
- The application must never display a fabricated percentage.

Do not add:

- Multiple pages.
- Side menus.
- Dashboards.
- Advanced settings.
- Technical configuration panels.
- Retry configuration.
- Concurrency configuration.
- Transfer history.
- Detailed Graph information.
- Unnecessary statistics.
- Decorative controls that do not help the IT employee complete the transfer.
- User management.
- Databases.
- Scheduling.
- Email notifications.
- Theme selection.
- Cloud upload.
- File preview.
- Permission management.

### User-facing error requirements

Every error displayed in the interface must be clear and understandable to an IT employee without requiring knowledge of Microsoft Graph, HTTP, MSAL, or application internals.

Each displayed error must contain:

1. A short title describing the problem.
2. A clear explanation of what happened.
3. A direct instruction describing what the employee should check or do.
4. A short reference code that can be used to find the technical details in the application log.

Example:

```text
Cannot Access Employee OneDrive

The signed-in Microsoft account does not have permission to open this employee's OneDrive.

Confirm that access was granted to the administrator, then try again.

Reference: OD-ACCESS-001
```

Example:

```text
Destination Folder Is Not Writable

The application cannot create files in the selected folder.

Select another local folder or grant the Windows account write permission.

Reference: DST-WRITE-001
```

Example:

```text
Internet Connection Interrupted

The transfer was temporarily interrupted because Microsoft OneDrive could not be reached.

The application will retry automatically. Completed files will not be downloaded again.

Reference: NET-TEMP-001
```

Example:

```text
Not Enough Disk Space

The selected server disk does not have enough available space to continue the transfer.

Free additional disk space or select another local disk.

Reference: DST-SPACE-001
```

The interface must never display:

- Stack traces.
- Raw exception messages.
- Raw Microsoft Graph responses.
- Raw HTTP response bodies.
- Access tokens.
- Authentication headers.
- Temporary download URLs.
- Internal file paths unrelated to the selected destination.
- Technical exception class names.

Detailed diagnostics must be written only to the structured log.

### Error categories

Use consistent user-facing error categories:

- `Authentication Required`.
- `Microsoft Sign-In Failed`.
- `OneDrive Access Denied`.
- `Invalid OneDrive URL`.
- `OneDrive Could Not Be Found`.
- `Internet Connection Interrupted`.
- `Microsoft Service Temporarily Unavailable`.
- `Destination Folder Is Invalid`.
- `Destination Folder Is Not Writable`.
- `Not Enough Disk Space`.
- `File Could Not Be Downloaded`.
- `File Could Not Be Saved`.
- `Transfer Cancelled`.
- `Transfer Completed With Warnings`.
- `Transfer Completed With Errors`.
- `Transfer Could Not Continue`.

Transient problems must be retried automatically without requiring the employee to make technical decisions.

Permanent problems must stop only the affected file when safe. A permanent run-level problem must stop the transfer cleanly and preserve resumable state.

### Final summary

At completion, show a simple summary containing:

- Transfer result.
- Completed files.
- Skipped files.
- Failed files.
- Downloaded size.
- Destination folder.
- Report folder.
- A clear indication of whether the transfer completed successfully, completed with warnings, completed with errors, or was cancelled.

Use the run-level result `CompletedWithWarnings` when the backup completed but the source changed during transfer, external shortcuts were skipped, or a stable source moment could not be reached within the bounded reconciliation policy.

Display:

```text
Transfer Completed With Warnings

The accessible OneDrive files were backed up, but some source items changed or could not be included during final verification.

Review the generated report for details.

Reference: TRN-WARN-001
```

Do not display internal implementation metrics in the final user interface.

Benchmark results, queue depth, memory measurements, manifest segments, HTTP retries, and diagnostic information belong in technical reports and logs, not in the normal employee interface.

---

## 7. Authentication

Use Microsoft Identity Platform through MSAL.

Use interactive delegated authentication supporting:

- MFA.
- Conditional Access.
- Silent token acquisition.
- Token renewal during long operations.
- Session restoration.
- Sign out.

Use a public-client Entra application registration.

Read configuration from `appsettings.json`:

- `TenantId`
- `ClientId`
- `RedirectUri`
- `AllowedOneDriveHost`

Use:

```text
RedirectUri = http://localhost
```

Required delegated Microsoft Graph permissions:

- `User.Read`
- `Files.Read.All`
- `Sites.Read.All`
- `offline_access`
- `openid`
- `profile`

### Microsoft Graph API stability

Only Microsoft Graph `v1.0` and officially documented Microsoft authentication APIs are permitted for Microsoft 365 access.

Do not use:

- Microsoft Graph `beta` endpoints.
- Beta-only Graph SDK models.
- Preview APIs.
- SharePoint REST API fallbacks.
- CSOM fallbacks.
- Undocumented endpoints.
- Reverse-engineered SharePoint or OneDrive APIs.

If a required behavior is unavailable through Microsoft Graph `v1.0` and officially documented authentication APIs, record it as a genuine limitation. Do not silently switch to another Microsoft 365 API surface.

Do not place any of the following in source code or configuration:

- Client secret.
- Certificate private key.
- Administrator password.
- Access token.
- Refresh token.

Protect the persistent MSAL token cache with Windows DPAPI for the current Windows user.

When `Remember sign-in` is disabled:

- Do not preserve the authentication session after the application closes.
- Clear any application-owned persistent token-cache data.

When signing out:

- Remove the selected account from the MSAL cache.
- Remove the application-owned persistent token-cache data.

---

## 8. Employee OneDrive Access and URL Support

The application is used to create a complete local backup of one employee's Microsoft 365 OneDrive.

Before using the application, the IT administrator must manually grant the designated administrator account Site Collection Administrator access to the employee's personal OneDrive site through Microsoft 365 administration tools.

Permission assignment is performed outside the application.

The application must never:

- Grant OneDrive permissions.
- Change OneDrive permissions.
- Remove OneDrive permissions.
- Assign Site Collection Administrator access.
- Request Microsoft 365 write permissions.
- Modify the employee's OneDrive content.

### Required source

The supplied URL must resolve to the root of the employee's individual Microsoft 365 OneDrive for Business drive.

The application must transfer the complete in-scope active content physically belonging to that validated employee OneDrive drive, including:

- All files.
- All folders.
- All nested folders.
- Empty folders.
- Files with Arabic, English, Unicode, and special-character names.

### Accepted URL

Accept only a normal employee OneDrive root URL that resolves to the root of an employee OneDrive for Business drive within the configured Microsoft 365 tenant.

Example:

```text
https://YOURTENANT-my.sharepoint.com/personal/employee_domain_com/
```

The IT administrator may paste the employee's personal OneDrive site root URL.

The application must resolve that personal site to its default OneDrive for Business drive and then transfer from the Microsoft Graph drive root.

The pasted URL is not required to contain `/Documents`.

The application may normalize or resolve variations of the same employee OneDrive root URL when they still identify the same personal OneDrive site and default drive.

### Microsoft Graph drive-type validation

The phrase `employee personal OneDrive` describes the employee's individual Microsoft 365 OneDrive site and must not be interpreted as requiring Microsoft Graph `driveType = personal`.

For Microsoft 365 OneDrive for Business, the resolved Microsoft Graph drive is expected to use:

```text
driveType = business
```

The application must reject:

```text
driveType = personal
```

because it represents a consumer OneDrive account outside the organizational Microsoft 365 workflow.

The application must also reject:

```text
driveType = documentLibrary
```

when it represents a SharePoint, Teams, project-site, or other non-employee document library.

Validation must combine the drive type with the resolved tenant, canonical site URL, personal-site path, drive owner, and drive root.

The application must not identify an employee OneDrive from the `driveType` value alone.

### Rejected URLs

Reject URLs that resolve only to:

- A single file.
- A subfolder.
- A shared file.
- A shared folder.
- A SharePoint team-site document library.
- A SharePoint communication-site document library.
- A Microsoft Teams document library.
- A SharePoint project site.
- A consumer OneDrive using `driveType = personal`.
- Any drive that is not the employee's Microsoft 365 OneDrive for Business drive.
- Any OneDrive outside the configured Microsoft 365 tenant.

### Validation before transfer

Before starting, the application must confirm that:

- The URL is present.
- The URL uses HTTPS.
- The URL belongs to the configured Microsoft 365 tenant.
- The URL resolves to an employee personal-site path in that tenant.
- The site resolves to its default Microsoft 365 OneDrive for Business drive.
- The resolved drive uses `driveType = business`.
- The resolved drive owner matches the employee represented by the personal-site URL where the required owner information is available.
- The resolved item is the Microsoft Graph root of that drive.
- The signed-in administrator can read the drive.
- The administrator has sufficient access to enumerate and download its complete in-scope active content.

If the administrator does not have access, do not start the transfer.

Display:

```text
Cannot Access Employee OneDrive

The signed-in administrator does not have permission to access this employee's OneDrive.

Grant the administrator Site Collection Administrator access to the employee's OneDrive, confirm that the OneDrive opens successfully in a browser, then try again.

Reference: OD-ACCESS-001
```

If the supplied URL represents only a file or subfolder, display:

```text
Employee OneDrive Root Required

The entered URL does not represent the complete employee OneDrive.

Open the employee's main OneDrive page and copy the root OneDrive URL, then try again.

Reference: OD-ROOT-001
```

If the URL resolves to a SharePoint library rather than an employee OneDrive for Business drive, display:

```text
Personal OneDrive Required

The entered URL belongs to a SharePoint document library and not to an employee's personal OneDrive.

Enter the employee's personal OneDrive root URL.

Reference: OD-TYPE-001
```

Do not display:

- Drive IDs.
- Site IDs.
- Item IDs.
- Raw Microsoft Graph responses.
- HTTP status codes as the primary message.
- Stack traces.
- Authentication tokens.
- Temporary download URLs.
- Internal exception details.

### Backup scope and non-synchronization boundaries

The application creates a file-and-folder backup of the active content currently accessible from the employee's Microsoft 365 OneDrive root.

The backup includes:

- Active files.
- Active folders.
- Nested folders.
- Empty folders.
- File names and folder names.
- File content.
- Source last-modified timestamps where supported.
- Source identity and transfer metadata required for verification and resume.

The current version does not back up:

- OneDrive Recycle Bin content.
- Deleted-item history.
- Previous file versions.
- Sharing permissions.
- Sharing links.
- Comments.
- Activity history.
- Retention-policy configuration.
- Compliance records.
- Microsoft 365 audit records.

These exclusions must be stated clearly in the README as genuine scope limitations.

### OneDrive shortcuts and external content

The application must not recursively follow OneDrive shortcuts, `remoteItem` references, shared-folder shortcuts, or links that point outside the resolved employee source drive.

An external shortcut must not cause the application to transfer content from:

- Another employee's OneDrive.
- A SharePoint document library.
- A Microsoft Teams library.
- An external tenant.
- Any drive other than the validated employee source drive.

Record such items in the report with:

```text
Status: Skipped
Reason: External shortcut or remote item outside the employee source drive
```

The application must prevent cycles caused by shortcuts or remote references.

---

## 9. Local Destination Support

Allow only local storage paths attached to the same Windows Server where the application is running.

Supported examples:

```text
D:\OneDriveArchive\EmployeeName
```

```text
E:\EmployeeArchives\EmployeeName
```

### Destination directory structure

The destination selected by the IT employee is the transfer container.

The application must create:

```text
SelectedDestination\
├── OneDriveData\
└── _TransferReport\
```

All employee OneDrive folders and files must be recreated only inside:

```text
OneDriveData
```

All application logs, reports, manifests, checkpoints, lock metadata, and operational metadata must be stored only inside:

```text
_TransferReport
```

A source folder named `_TransferReport` must be copied normally under `OneDriveData` and must never conflict with the application's report directory.

Before starting:

- Validate that the destination is a local path.
- Reject UNC paths and any remote or network destination.
- Reject mapped network drives and volumes reported by Windows as network drives.
- Reject paths that resolve through symbolic links, junctions, mount points, or other reparse points to a remote location or outside the selected destination root.
- Canonicalize and validate the selected destination before creating transfer files.
- Create the destination container when possible.
- Create `OneDriveData` and `_TransferReport` when possible.
- Confirm write access to both application-created subdirectories.
- Prevent path traversal.
- Detect invalid or inaccessible paths.
- Check available disk space when total source size is available.

### Exclusive destination lock

Prevent concurrent use of the same destination across the current application instance, separate application processes, and separate Windows sessions on the same server.

The application must acquire an exclusive destination lock before transfer initialization.

The lock must be associated with the canonical selected destination and held for the complete transfer lifecycle.

The lock implementation must work across processes and Windows sessions on the same server.

A stale lock may be recovered only after confirming that no active owning process still holds the operating-system lock.

If the destination is already locked by an active transfer, do not start and display:

```text
Destination Is Already in Use

Another transfer is currently using the selected destination.

Wait for the active transfer to finish or select another local destination.

Reference: DST-LOCK-001
```

The application must not support:

- UNC paths.
- Mapped network drives.
- Network shares.
- NAS destinations.
- SMB destinations.
- Remote server destinations.
- Remote targets reached through symbolic links, junctions, mount points, or reparse points.
- Any destination that is not local storage attached to the current Windows Server.

---

## 10. Transfer Behavior

Implement the following exact behavior.

1. Recursively enumerate the complete source folder structure.

2. Follow Microsoft Graph pagination through `@odata.nextLink`.

3. Recreate the complete employee OneDrive folder structure only inside `SelectedDestination\OneDriveData`.

4. Recreate empty folders.

5. Download files using streaming.

6. Never load complete files into memory.

7. Download each file initially as:

```text
filename.extension.partial
```

8. Rename the partial file to the final filename only after a successful complete download.

9. For large files, support resumable byte-range downloads when the Microsoft download endpoint supports HTTP Range requests.

When a `.partial` file exists:

- Verify the source drive ID, source item ID, ETag or CTag where available, expected size, and last-modified timestamp before appending.
- Determine the existing local `.partial` length.
- Obtain a current Microsoft Graph download URL. Do not persist or reuse expired temporary download URLs.
- Requests sent to temporary Microsoft download URLs must not include the Microsoft Graph `Authorization` header.
- Use a dedicated request or HTTP client that does not automatically attach Graph bearer tokens, cookies, or Graph-specific authentication headers to temporary download URLs.
- Temporary download URLs must never be logged, persisted, placed in reports, placed in manifests, or included in user-facing errors.
- Send the `Range` header to the actual Microsoft download URL, not to the original Graph `/content` request.
- Resume from the next required byte only when the endpoint returns a valid `206 Partial Content` response with a matching `Content-Range`.
- If the endpoint ignores the range request and returns `200 OK`, do not append the full response to the existing partial file. Safely restart that file from byte zero.
- If the source identity or metadata no longer matches, discard only the invalid `.partial` file and restart that file.
- Never append to a `.partial` file unless source identity and relevant metadata match.
- Perform the required download-integrity validation before renaming the `.partial` file.
- Record whether the file was `Resumed`, `Restarted`, or downloaded from the beginning in the CSV report and manifest.

### Download integrity validation

Before committing a downloaded file:

- Confirm that the HTTP download completed successfully.
- Confirm that the number of bytes written matches the expected source size.
- Re-read the current Microsoft Graph source metadata.
- Confirm that the source drive ID, source item ID, and relevant ETag, CTag, size, and last-modified metadata have not changed during the download.
- When Microsoft Graph provides a supported source file hash, calculate the corresponding local hash and verify the downloaded file against that source hash.
- Do not claim cryptographic or hash verification when no supported source hash is available.
- When no supported source hash is available, record that verification used source identity, byte count, and post-download metadata revalidation rather than a cryptographic hash.

If the source changed during download:

- Do not commit the downloaded file as completed.
- Preserve or discard the `.partial` file according to whether its source identity and current metadata still make it safe to resume.
- Obtain current source metadata.
- Retry according to the bounded retry policy.
- Do not promote the state to `DownloadVerified`, `FinalFileCommitted`, or `Completed` until the current source version has been downloaded and verified.

10. Preserve the source last-modified timestamp where supported.

11. Support:

- Arabic filenames.
- Unicode filenames.
- Spaces.
- Special characters.

12. Safely handle:

- Invalid Windows filename characters.
- Reserved Windows filenames.
- Trailing dots.
- Trailing spaces.
- Long paths.
- Duplicate names after sanitization.
- Files with the same visible name.
- Empty folders.

13. Never silently overwrite an unrelated existing local file.

14. Record every local filename or path adjustment in the CSV report.

15. On rerun using the same destination:

- Skip files already completed when source identity, size, and relevant metadata match.
- Retry incomplete `.partial` files.
- Download changed files again.
- Do not download all completed files again.

16. Use asynchronous operations throughout.

17. Keep the WPF interface responsive.

18. Prevent more than one transfer from running simultaneously.

19. Use `CancellationToken`.

20. Cancellation must:

- Stop scheduling new source items.
- Cancel supported active requests.
- Preserve completed files.
- Preserve safe `.partial` files.
- Record `Cancelled` for every discovered item that entered the processing pipeline but did not complete.

Source items that had not yet been discovered when cancellation occurred cannot be recorded individually as cancelled.

The run summary must record:

- That enumeration was incomplete.
- The discovered-item count at cancellation.
- The completed, skipped, failed, and cancelled discovered-item counts.
- That undiscovered source items may remain unprocessed.

The application must never imply that the entire OneDrive was evaluated when cancellation occurred before enumeration completed.

21. Use a fixed maximum of three simultaneous file downloads.

### Bounded download concurrency

Use a fixed maximum of three simultaneous file downloads.

Concurrency must remain internal and must not appear as an editable interface setting.

The application must use bounded metadata and download queues with backpressure.

When Microsoft Graph throttles requests or returns `Retry-After`, the application must pause or reduce active request scheduling as required, but it must never increase concurrency above three simultaneous file downloads.

When stability is uncertain, the application must continue with fewer workers or wait rather than increasing concurrency.

22. Keep the concurrency behavior internal. Do not expose it in the interface.

23. For HTTP 429 and HTTP 503:

- Respect `Retry-After`.
- Pause new Graph requests when throttled.
- Retry safely.

24. For temporary network errors:

- Use bounded exponential backoff.
- Use a maximum of five attempts per file.

25. A failed file must not stop the remaining transfer.

### In-scope items that cannot be downloaded

Any source item that is visible, belongs to the validated employee source drive, is within the active backup scope, and cannot be downloaded through the permitted Microsoft Graph `v1.0` APIs must not be silently ignored.

Record the item as:

```text
Status: Failed
Reason: Source item is not downloadable through the supported Microsoft Graph v1.0 interface
```

Do not create a placeholder file that could be mistaken for the original source content.

The final run result must be `CompletedWithErrors` when one or more required in-scope source items could not be downloaded.

26. Refresh authentication tokens during long-running operations when required.

27. Dispose all streams, HTTP responses, file handles, and cancellation resources correctly.

### Local file-lock and antivirus behavior

The transfer engine must tolerate temporary local file-system interference caused by antivirus scanning, indexing, backup software, or another process briefly opening a destination file.

For transient local errors such as sharing violations, temporary access denial, or rename failure:

- Retry the specific local operation with bounded backoff.
- Revalidate that the path still refers to the expected transfer item before retrying.
- Never disable antivirus or security controls automatically.
- Never add broad antivirus exclusions.
- Do not treat a permanent permission failure as transient indefinitely.
- Preserve the `.partial` file when safe.
- Record the final local error clearly when retries are exhausted.

### Final reconciliation pass

After the initial transfer, the application must perform a final reconciliation pass against the source drive.

The reconciliation pass must detect files added, modified, moved, renamed, or deleted during the transfer.

Detected new or changed files must be processed before the run is marked completed.

Moved, renamed, and deleted source items must be recorded in the manifest and final report so the result does not falsely claim an unchanged source.

A source deletion must never cause deletion of an already completed local backup file.

A moved or renamed source item must be processed at its current source path without silently deleting a verified local copy stored at its previous backup path.

The reconciliation implementation must remain bounded in memory and use Microsoft Graph `v1.0` only.

The reconciliation policy must use a maximum of three reconciliation passes.

When an incremental Microsoft Graph `v1.0` change-tracking mechanism is officially documented and available for the validated drive, use it instead of repeatedly re-enumerating the complete drive.

If incremental reconciliation cannot be continued safely, perform one bounded final re-enumeration of the complete source drive.

Do not mark the run as `Completed` while unresolved changes remain.

After three reconciliation passes, unresolved source changes must produce `CompletedWithWarnings`.

Record all unresolved additions, modifications, moves, renames, deletions, and inaccessible items in the manifest and final reports.

The run-level result must be `CompletedWithWarnings` when the backup completed but could not represent one stable source moment. It must not be reported as a fully successful consistent backup.

---

## 11. Resume and Existing-File Rules

A local file may be skipped only when there is sufficient evidence that it matches the source.

Use available source metadata such as:

- Microsoft Graph drive ID.
- Microsoft Graph item ID.
- Parent item ID.
- ETag or CTag when appropriate.
- File size.
- Last-modified timestamp.
- Stored transfer-manifest information.

Do not use filename alone to determine that a transfer is complete.

When a local file exists but cannot be verified as the same source file:

- Do not silently overwrite it.
- Generate a deterministic safe local name.
- Record the adjustment and reason in the report.

### Stable path-mapping contract

The deterministic Windows path-mapping algorithm must define a required integer `PathMappingVersion`.

Start the current path-mapping format at:

```text
PathMappingVersion = 1
```

Store `PathMappingVersion` in:

- The manifest index.
- Every manifest segment header.
- Every transfer run summary.
- The machine-readable benchmark report.

The path-mapping algorithm must document and consistently apply:

- Unicode normalization.
- Invalid-character replacement.
- Reserved Windows-name handling.
- Trailing-dot and trailing-space handling.
- Empty sanitized-name handling.
- Case-insensitive collision handling.
- File-versus-folder collision handling.
- Long-path shortening.
- Deterministic collision suffix generation.

A rerun using an existing destination must use the `PathMappingVersion` recorded by that destination manifest.

Do not silently reinterpret an existing destination using a different path-mapping version.

Reject unsupported future path-mapping versions with a clear operational error, or perform an explicitly documented, atomic, recoverable migration.

Path mapping must remain deterministic for the same source identity, source path, destination root, and `PathMappingVersion`.

### Backup versus synchronization

The application is a backup-copy tool and not a synchronization or mirroring tool.

The application must never delete a completed local backup file solely because the corresponding source item:

- Was deleted from OneDrive.
- Was moved.
- Was renamed.
- Became temporarily inaccessible.

When reconciliation detects a deleted source item:

- Retain any completed local copy.
- Record that the source item was deleted after discovery.
- Do not delete the local backup copy.

When reconciliation detects a moved or renamed source item:

- Process the current source path according to the normal transfer rules.
- Do not silently delete an existing verified local backup from its previous path.
- Record the old and current source paths in the reconciliation result.

When a source file with the same source item identity has changed:

- Download the current version to a `.partial` file.
- Verify the completed download.
- Atomically replace only the previous local file that is proven by the manifest to represent that same source item.
- Never replace an unrelated local file.

The README must state that rerunning into the same destination updates verified changed items and preserves local copies of source items that were later deleted.

### Persisted transfer states

Every source item must use an explicit persisted state.

Allowed transfer states:

```text
Discovered
DestinationMapped
Downloading
DownloadVerified
FinalFileCommitted
Completed
Skipped
Failed
Cancelled
```

State rules:

- `Completed` must never be written before the final local file has been successfully committed.
- A valid final file with state `FinalFileCommitted` must be verified after restart and promoted to `Completed`.
- A valid `.partial` file with matching source identity and metadata may be resumed.
- An invalid or mismatched `.partial` file must be restarted from byte zero.
- A missing final file must not remain recorded as `Completed`.
- Manifest state transitions must be flushed incrementally.
- Recovery must be idempotent and safe when performed repeatedly.

### Mandatory local transfer manifest

The application must maintain an incremental local transfer manifest inside `SelectedDestination\_TransferReport` for resume, verification, auditing, and crash recovery.

The manifest must:

- Define a required integer `ManifestVersion` in the manifest index and every segment header.
- Start the current format at `ManifestVersion = 1`.
- Define `PathMappingVersion` in the manifest index and every manifest segment header.
- Start the current path-mapping format at `PathMappingVersion = 1`.
- Reject unsupported future manifest versions with a clear error instead of misreading them.
- Preserve backward-reading support for every manifest version previously released by this product, or provide an explicit atomic migration procedure.
- Never rewrite or migrate the only valid manifest copy without first creating a recoverable backup.
- Be written incrementally during enumeration and transfer.
- Be partitioned automatically into ordered manifest segments.
- Use deterministic segment names such as `TransferManifest_000001.jsonl`.
- Rotate to a new segment before the active segment exceeds 1 GiB.
- Never require a single manifest segment larger than 1 GiB.
- Maintain a small local manifest index containing segment order, run identity, record counts, byte ranges, and integrity information.
- Write the manifest index atomically.
- Store source drive ID.
- Store source item ID.
- Store parent item ID.
- Store ETag or CTag where available.
- Store source web URL and source relative path.
- Store the deterministic sanitized local path.
- Store expected source size.
- Store local partial or completed size.
- Store source last-modified timestamp.
- Store transfer state and attempt count.
- Store whether a file was downloaded from the beginning, resumed, restarted, skipped, failed, or cancelled.
- Be safe to read after application crash, forced process termination, network failure, or server restart.
- Be append-safe or update-safe without requiring the complete manifest to be loaded into memory.
- Use atomic replacement, durable append records, checksummed records, or another documented corruption-resistant local-file approach.
- Support bounded-memory lookup and recovery at the defined multi-million-item scale.
- Be flushed regularly rather than only when the application closes.
- Never contain passwords, tokens, authentication headers, cookies, or temporary download URLs.
- Not require or introduce a database.

The final CSV report is a human-readable report. It must not be the only operational source used for resume and recovery.

---

## 12. Reports and Logs

Store all application reports and operational metadata only inside:

```text
SelectedDestination\_TransferReport
```

Store employee OneDrive content only inside:

```text
SelectedDestination\OneDriveData
```

For each run, create inside `_TransferReport`:

```text
TransferLog_yyyyMMdd_HHmmss.log
TransferReport_yyyyMMdd_HHmmss_000001.csv
TransferReportIndex_yyyyMMdd_HHmmss.json
TransferSummary_yyyyMMdd_HHmmss.csv
FailedFiles_yyyyMMdd_HHmmss_000001.csv
FailedFilesIndex_yyyyMMdd_HHmmss.json
FailedFilesSummary_yyyyMMdd_HHmmss.csv
```

### Segmented CSV reports

The complete transfer report must be written incrementally and automatically segmented.

Use deterministic segment names:

```text
TransferReport_yyyyMMdd_HHmmss_000001.csv
TransferReport_yyyyMMdd_HHmmss_000002.csv
```

The report index must contain:

- Run identifier.
- Report schema version.
- Ordered segment names.
- Record count per segment.
- Total record count.
- Segment byte size.
- Creation and completion timestamps.
- Integrity information.

Rotate to a new CSV segment before either:

- The current segment reaches 1,000,000 data records.
- The current segment reaches 1 GiB.

CSV files must:

- Use UTF-8.
- Correctly escape commas, quotation marks, and line breaks.
- Prevent spreadsheet-formula interpretation of untrusted filenames or values beginning with `=`, `+`, `-`, or `@`.
- Be written incrementally without retaining the complete report in memory.

The summary file must remain small and contain only run-level totals and results.

Every transfer run summary must include:

- Run identifier.
- Application version.
- `ManifestVersion`.
- `PathMappingVersion`.
- Run-level result.
- Whether enumeration completed.
- Discovered-item count.
- Completed, skipped, failed, and cancelled discovered-item counts.
- Reconciliation result.
- Start and completion timestamps.

### Segmented failed-item reports

Failed-item details must be written incrementally and automatically segmented.

Use deterministic segment names:

```text
FailedFiles_yyyyMMdd_HHmmss_000001.csv
FailedFiles_yyyyMMdd_HHmmss_000002.csv
```

Create:

```text
FailedFilesIndex_yyyyMMdd_HHmmss.json
FailedFilesSummary_yyyyMMdd_HHmmss.csv
```

Rotate to a new failed-item segment before either:

- The current segment reaches 1,000,000 data records.
- The current segment reaches 1 GiB.

The failed-item index must contain:

- Run identifier.
- Report schema version.
- Ordered segment names.
- Record count per segment.
- Total failed-item record count.
- Segment byte size.
- Creation and completion timestamps.
- Integrity information.

Failed-item CSV segments must follow the same UTF-8, escaping, spreadsheet-formula-injection prevention, incremental-writing, and bounded-memory rules as the complete transfer-report segments.

`FailedFilesSummary_yyyyMMdd_HHmmss.csv` must remain small and contain only run-level failed-item totals and category counts.

Each complete-report record must contain:

- Source item ID.
- Source web URL.
- Source relative path.
- Previous source path where reconciliation detected a move or rename.
- Original filename.
- Final local filename.
- Final local path.
- Source size.
- Local size.
- Source last-modified date.
- Source hash algorithm and source hash value when Microsoft Graph provides a supported hash.
- Verification method: `SourceHashAndMetadata` or `SizeAndMetadata`.
- Post-download source metadata revalidation result.
- Status.
- Reason.
- Persisted transfer state.
- Number of attempts.
- Start time.
- Completion time.
- Error message.
- Path adjustment reason.
- Download mode: `Fresh`, `Resumed`, or `Restarted`.
- Initial partial size.
- Final local size.
- Reconciliation result where applicable.

Allowed item-report statuses:

- `Downloaded`.
- `Skipped`.
- `Failed`.
- `Cancelled`.

Allowed run-level results:

- `Completed`.
- `CompletedWithWarnings`.
- `CompletedWithErrors`.
- `Cancelled`.
- `Failed`.

Use `CompletedWithWarnings` when:

- The source continued changing after the third reconciliation pass.
- External shortcuts were skipped.
- Source items were deleted, moved, or renamed during the transfer.
- The backup completed but could not represent one stable source moment.

Use `CompletedWithErrors` when:

- One or more required in-scope source items failed to download.
- One or more required in-scope source items were visible but not downloadable through the supported Microsoft Graph `v1.0` interface.
- The transfer completed the remaining safe work but contains item-level failures.

Never log:

- Passwords.
- Access tokens.
- Refresh tokens.
- Authentication headers.
- Cookies.
- Temporary Microsoft Graph download URLs.
- Client secrets.
- Complete sensitive Graph responses.

---

## 13. Required Architecture

Use MVVM and dependency injection.

Create clear components equivalent to:

- `AuthenticationService`
- `TokenCacheService`
- `OneDriveUrlResolver`
- `GraphDriveService`
- `TransferService`
- `ReconciliationService`
- `DestinationLockService`
- `PathMappingService`
- `RetryPolicyService`
- `ReportService`
- `MainViewModel`

Do not place authentication, Graph, transfer, reporting, or filesystem logic directly inside WPF code-behind.

Code-behind may contain only minimal view-specific behavior that cannot reasonably be expressed through MVVM.

Do not add a database.

---

## 14. Required Testing

Create a separate automated test project.

Most automated tests must not require access to a real Microsoft tenant.

Mock Microsoft Graph and HTTP communication for unit and synthetic tests.

Test at minimum:

- Employee personal OneDrive root URL resolution.
- Personal-site root URL resolution without requiring `/Documents` in the pasted URL.
- Resolution of the personal site to its default Microsoft Graph drive root.
- Microsoft 365 employee OneDrive validation with `driveType = business`.
- Consumer OneDrive rejection with `driveType = personal`.
- SharePoint and Teams document-library rejection with `driveType = documentLibrary`.
- Validation combining tenant, canonical site URL, personal-site path, drive owner, drive type, and drive root.
- OneDrive subfolder URL rejection.
- Single-file URL rejection.
- Shared-folder URL rejection.
- SharePoint document-library rejection.
- Administrator access validation.
- OneDrive root-item validation.
- Tenant URL rejection.
- Complete root-drive recursive enumeration.
- Active-content backup scope excludes Recycle Bin, previous versions, permissions, links, comments, retention metadata, activity history, and audit records.
- External `remoteItem` and OneDrive shortcut detection.
- External shortcut skipped without traversing another drive.
- Shortcut-cycle prevention.
- Graph pagination.
- Recursive folder enumeration.
- Empty folder handling.
- Destination structure creation with separate `OneDriveData` and `_TransferReport` directories.
- Source `_TransferReport` folder copied under `OneDriveData` without collision.
- UNC path rejection.
- Mapped network-drive rejection.
- Reparse-point rejection when the resolved target is remote or outside the selected destination root.
- Destination canonicalization.
- Exclusive destination locking across application processes.
- Exclusive destination locking across separate Windows sessions on the same server.
- Active destination lock rejection with `DST-LOCK-001`.
- Safe stale-lock recovery only when no active owner holds the operating-system lock.
- Path sanitization.
- `PathMappingVersion = 1` is stored in the manifest index, every manifest segment header, every transfer run summary, and the benchmark report.
- Rerun uses the destination's recorded `PathMappingVersion`.
- Unsupported future `PathMappingVersion` rejection.
- Atomic and recoverable path-mapping migration when migration is implemented.
- Stable Unicode normalization.
- Stable invalid-character replacement.
- Stable reserved-name handling.
- Stable trailing-dot and trailing-space handling.
- Stable empty-name handling.
- Stable case-insensitive collision handling.
- Stable file-versus-folder collision handling.
- Stable long-path shortening.
- Stable deterministic collision suffix generation.
- Arabic filenames.
- Unicode filenames.
- Reserved Windows filenames.
- Trailing dot and space handling.
- Long paths.
- Duplicate sanitized names.
- Existing-file skip decisions.
- Changed-file replacement decisions.
- Unrelated local-file conflict handling.
- Partial-file recovery.
- Valid byte-range resume returning `206 Partial Content`.
- Range request ignored with `200 OK` without corrupt append behavior.
- Invalid or mismatched `Content-Range`.
- Expired temporary download URL refresh.
- Temporary Microsoft download URL requests do not include Graph bearer tokens, cookies, or Graph-specific authentication headers.
- Temporary download URLs are not logged or persisted.
- Source ETag, CTag, size, or modified-date change during resume.
- Successful source-hash verification when a supported Microsoft Graph file hash is available.
- Hash mismatch rejection.
- Source metadata changing after the download started.
- Correct fallback verification when no supported source hash is available.
- No false claim of cryptographic verification when no supported source hash is available.
- Mandatory manifest incremental writes.
- Every allowed persisted transfer state and transition.
- Crash recovery between `DownloadVerified`, `FinalFileCommitted`, and `Completed`.
- Missing final file invalidates a persisted `Completed` state.
- Idempotent repeated recovery.
- Manifest recovery after a truncated or interrupted write.
- Bounded-memory manifest recovery and lookup.
- Supported `ManifestVersion` loading.
- Unsupported future `ManifestVersion` rejection.
- Safe manifest migration or backward-compatibility behavior.
- Retry behavior.
- HTTP 429 handling.
- HTTP 503 handling.
- Fixed concurrency never exceeding three downloads.
- Request scheduling pauses or reduces during throttling.
- Bounded queues remain within their configured capacity.
- Temporary local file sharing violation.
- Antivirus-style delayed file release.
- Transient local access denial.
- Final-file rename temporarily blocked.
- Permanent destination permission denial.
- Initial transfer followed by a final reconciliation pass.
- Detection and processing of files added during transfer.
- Detection and processing of files modified during transfer.
- Detection and reporting of files moved or renamed during transfer.
- Detection and reporting of files deleted during transfer.
- Bounded reconciliation when the source continues changing.
- Warning result when a consistent final state cannot be reached within the reconciliation policy.
- Incremental Microsoft Graph `v1.0` change tracking is used when officially available for the validated drive.
- Reconciliation never exceeds three passes.
- One bounded final re-enumeration is used when incremental reconciliation cannot continue safely.
- Unresolved source changes after the third pass produce `CompletedWithWarnings`.
- Required in-scope source items that are not downloadable produce item status `Failed` and run result `CompletedWithErrors`.
- Deleted source item retains the completed local backup.
- Moved or renamed source item preserves the verified previous local copy and processes the current path.
- Changed source item atomically replaces only the verified local file for the same source identity.
- `CompletedWithWarnings` run-level result and `TRN-WARN-001` user message.
- RDP disconnect without cancellation.
- Sleep or long network pause followed by recovery.
- Clock correction without invalid retry or token calculations.
- Cancellation stops scheduling new source items.
- Only discovered items that entered the processing pipeline can be recorded individually as `Cancelled`.
- Undiscovered items are not falsely recorded as cancelled.
- Cancelled run summary records incomplete enumeration and the discovered-item count.
- Cancelled run summary states that undiscovered source items may remain unprocessed.
- User-facing errors contain a title, plain-language explanation, direct action, and reference code.
- User-facing errors do not expose raw exceptions, HTTP bodies, Graph responses, tokens, or stack traces.
- The activity area remains bounded and does not retain the complete log in memory.
- Final summary contains only the defined operational results.
- CSV report generation.
- CSV report segmentation at record and byte thresholds.
- Failed-item CSV segmentation at record and byte thresholds.
- Atomic complete-report and failed-report index updates and integrity metadata.
- Small failed-item summary generation.
- UTF-8 CSV escaping for commas, quotation marks, and line breaks.
- Spreadsheet-formula injection prevention for untrusted values.
- Small run-level summary generation.
- Token-cache behavior that can be tested without Windows DPAPI.
- Prevention of simultaneous transfer jobs.
- Page-by-page enumeration without accumulating the complete drive hierarchy in memory.
- Bounded-memory behavior with a synthetic multi-page source containing a very large item count.

Do not remove or weaken tests merely to make the suite pass.

---

## 15. Required Deliverables

Create:

1. Complete Visual Studio solution.
2. WPF application project.
3. Automated test project.
4. `appsettings.example.json`.
5. `.gitignore`.
6. `README.md`.
7. Windows build script.
8. Windows publish script.
9. Source-validation reports under `artifacts/source`.
10. Published application directory under `artifacts/win-x64` after successful Windows publish.

Creating a packaging script or ZIP archive is optional and is not a required deliverable.

Do not leave:

- Placeholder methods.
- TODO comments representing unfinished required work.
- Fake implementations.
- Simulated transfers.
- Hardcoded successful responses.
- Unimplemented interfaces.
- Sample-only code.

---

## 16. Project Configuration

Configure Windows targeting:

```xml
<EnableWindowsTargeting>true</EnableWindowsTargeting>
```

The final Windows publish process must use:

- Release configuration.
- Runtime `win-x64`.
- Self-contained deployment.
- One deployment directory.
- No requirement to install .NET manually.

Do not force single-file publishing when it creates WPF or native dependency problems.

---

## 17. Required Configuration Template

### Fixed concurrency configuration

The fixed concurrency limit of three simultaneous file downloads is an application invariant and not a configurable deployment value.

Remove `MaximumConcurrentDownloads` from `appsettings.example.json`.

The implementation must never schedule more than three simultaneous file downloads.

Create `appsettings.example.json`:

```json
{
  "MicrosoftIdentity": {
    "TenantId": "YOUR-TENANT-ID",
    "ClientId": "YOUR-CLIENT-ID",
    "RedirectUri": "http://localhost",
    "AllowedOneDriveHost": "YOURTENANT-my.sharepoint.com"
  },
  "Transfer": {
    "MaximumRetryAttempts": 5
  },
  "Logging": {
    "MinimumLevel": "Information"
  }
}
```

`MaximumRetryAttempts` remains an internal operational value and must not appear as an editable field in the interface.

The fixed three-download concurrency invariant must not be read from configuration, environment variables, command-line arguments, registry values, or user-interface settings.

---

## 18. Information Required Before Production Use

Complete these values:

```text
ProjectPath:
TenantName:
TenantDomain:
TenantId:
ClientId:
OneDriveHost:
AdminEmail:
TestEmployeeEmail:
TestEmployeeOneDriveUrl:
LocalDestinationRoot:
WindowsServerName:
WindowsServerBuild:
WindowsRunAccount:
ProxyPresent: Yes/No
UI Language: English
Application Name: OneDrive Server Transfer
```

Never record the administrator password in this document.

---

## 19. Entra App Registration

Create a Microsoft Entra App Registration with:

```text
Application name: OneDrive Server Transfer
Supported account type: Accounts in this organizational directory only
Application type: Public client / native desktop
Redirect URI: http://localhost
```

Add delegated Microsoft Graph permissions:

```text
User.Read
Files.Read.All
Sites.Read.All
offline_access
openid
profile
```

Grant admin consent where required.

Do not request or add `Files.ReadWrite.All`, `Sites.ReadWrite.All`, or any other write permission.

This application is strictly read-only against Microsoft 365.

`Files.Read.All` and `Sites.Read.All` are requested only because the signed-in administrator must read another employee's personal OneDrive after Site Collection Administrator access has been granted manually.

The source is always the root of an employee personal OneDrive for Business drive. SharePoint team sites, project sites, Teams libraries, shared files, and shared folders are outside the application scope.

`Files.Read` alone is not sufficient because employee OneDrive access may involve resources other than the signed-in administrator's own drive, and the designated administrator must read another employee's OneDrive after access has been granted manually.

These delegated permissions allow the application to read only content that the signed-in administrator is authorized to access. They do not authorize this project to modify source content.

Resume metadata, local path mappings, reports, manifests, and transfer state must be stored locally and do not require write access to OneDrive or SharePoint.

Any future feature that modifies Microsoft 365 data requires a separate security review, separate scope approval, and an explicit specification change.

Do not create a client secret for this desktop application.

Enable the public-client flow required by MSAL desktop authentication.

---

## 20. Windows Server Preparation

Prepare Windows Server 2019 x64 with Desktop Experience.

Confirm:

- RDP access works.
- The Windows account can run the application.
- The destination disk has sufficient free space.
- The Windows account has write permission to the selected local destination.
- The selected destination is a local disk or locally attached storage on the same server.
- Long-path support is enabled when required.
- Outbound HTTPS access is available.
- Firewall, proxy, antivirus, or application-control policies do not block the application.

Required Microsoft endpoints include:

```text
https://login.microsoftonline.com
https://graph.microsoft.com
https://YOURTENANT-my.sharepoint.com
```

Do not disable security controls globally. Add only the smallest required exception when an operational issue is confirmed.

---

## 21. Test OneDrive Preparation

Prepare one employee OneDrive test account containing:

- Nested folders.
- Empty folders.
- Arabic filenames.
- English filenames.
- Unicode filenames.
- Filenames with spaces.
- A relatively large file.
- Duplicate filenames in different folders.
- A file later modified to test resume behavior.

Before testing:

- Grant the designated administrator account Site Collection Administrator access manually.
- Confirm the administrator can open the employee's complete personal OneDrive in a browser.
- Obtain the employee personal OneDrive root URL.
- Prepare separate invalid test URLs for a file, subfolder, shared folder, and SharePoint document library to verify rejection behavior.

---

## 22. README Requirements

The README must document only:

1. Entra App Registration creation.
2. Public-client configuration.
3. Redirect URI.
4. Required delegated Graph permissions.
5. Admin consent.
6. Placement of `TenantId` and `ClientId`.
7. macOS source-preparation limitations.
8. Windows Server 2019 build commands.
9. Windows Server 2019 publish commands.
10. How to copy and run the published application.
11. How to grant the administrator access to an employee OneDrive manually.
12. How to paste the OneDrive root URL, select a local server destination, and run the transfer.
13. The destination structure containing `OneDriveData` and `_TransferReport`.
14. Destination-lock behavior and `DST-LOCK-001`.
15. Log and report locations.
16. Final reconciliation behavior and warning outcomes.
17. The active-content backup scope and exclusions, including Recycle Bin, previous versions, sharing metadata, comments, retention data, and audit records.
18. External shortcut and `remoteItem` behavior.
19. Backup-versus-synchronization behavior, including preservation of local copies after source deletion.
20. Segmented report files and report index.
21. Genuine technical limitations.
22. Three-pass reconciliation policy and `CompletedWithWarnings`.
23. Behavior for required in-scope items that are not downloadable through Microsoft Graph `v1.0`.
24. Segmented failed-item reports, failed-item index, and failed-item summary.
25. Security rule prohibiting Graph authorization headers on temporary Microsoft download URLs.
26. Download-integrity behavior, including supported source hashes and the non-hash fallback.
27. `PathMappingVersion`, deterministic path-mapping behavior, compatibility, and migration rules.
28. Cancellation reporting scope and the limitation that undiscovered items cannot be reported individually.

Do not add general Microsoft 365 tutorials.

---

## 23. Execution Rules for the Implementation Agent

Complete the entire implementation now.

Completing the implementation in one execution must not override correctness.

If a required component cannot be implemented correctly, do not replace it with a fake implementation, placeholder, simplified behavior, simulated transfer, hardcoded success path, misleading test, or unsupported claim of completion.

Do not ask questions.

Do not provide multiple approaches.

Do not stop at:

- Planning.
- Architecture.
- Scaffolding.
- Pseudocode.
- Partial implementation.
- Sample code.

Do not:

- Add optional features.
- Redesign the requirements.
- Expand the scope.
- Stop because the current environment is macOS.
- Replace the requested application with a prototype.
- Create only code snippets.

When an operational problem blocks required behavior:

1. Investigate the problem.
2. Apply the smallest reliable correction.
3. Continue implementation.
4. Document the correction in the final report.

An operational issue does not authorize adding new features.

---

## 24. Final Acceptance Criteria

### Source implementation completion

When no compatible Windows environment is available, the result may be marked `Source Implementation Complete` only.

Source implementation completion requires:

- The full solution structure exists.
- The application source code is complete.
- The project restores successfully where supported.
- Automated and synthetic tests that are executable in the current environment have been executed.
- Discovered source, compilation, static-analysis, and test failures have been corrected where the current environment permits.
- The Windows Server 2019 build and publish commands are documented.
- The README is complete.
- No source operation can delete or modify OneDrive data.
- No Microsoft password is stored.
- No client secret is used.
- No unrequested feature was added.
- The complete source deliverable is the `./OneDriveServerTransfer` project directory itself.
- Source-validation reports are placed under `artifacts/source` without duplicating the project.
- The synthetic large-scale benchmark is included.
- The benchmark exercises the production transfer pipeline rather than a benchmark-only implementation.

When the current environment is not compatible Windows, the result must not be marked:

- `Production Ready`.
- `Fully Validated`.
- `Accepted`.
- Successfully published for Windows.

Cross-platform restore, source generation, static validation, or non-Windows tests must not be reported as successful Windows validation.

### Production acceptance

Production acceptance requires successful validation on a compatible Windows environment, including:

- Release build.
- Automated test execution.
- WPF startup.
- Microsoft interactive sign-in.
- Access validation against a real employee test OneDrive.
- Validation that the employee personal-site URL resolves to a `driveType = business` drive root.
- Complete test transfer from the employee OneDrive root.
- Verification of the `OneDriveData` and `_TransferReport` directory separation.
- Resume after interruption.
- Final reconciliation after changes during a test transfer.
- Exclusive destination locking across two application processes or Windows sessions.
- Self-contained `win-x64` publish.
- The published application is placed under `artifacts/win-x64`.
- Execution of the published application on Windows Server 2019.
- Successful execution of the 5,000,000-item synthetic benchmark within the specified memory and queue bounds.

Only after every required Windows validation step succeeds may the result be marked `Production Ready` or `Fully Validated`.

A failed or unexecuted production acceptance step must be reported explicitly and must not be inferred as successful.

Creating a ZIP archive is optional and is not part of source implementation completion or production acceptance.

---

## 25. Future Version — Explicitly Out of Scope

The following items are not missing requirements and must not be implemented in the current version:

- Multiple OneDrive batch processing.
- CSV import of employee accounts or OneDrive URLs.
- Scheduled transfer jobs.
- Windows Service mode.
- Unattended application authentication.
- Azure Key Vault integration.
- Central reporting.
- Admin dashboard.
- Remote administration.
- Database storage.
- Transfer history portal.
- OneDrive synchronization.
- Uploading or writing files back to Microsoft 365.
- Source file deletion, movement, or modification.
- Email notifications.
- Multi-server coordination.
- UNC destinations.
- Network shares.
- NAS destinations.
- SMB destination recovery.
- Remote transfer agents.

These items may be considered only in a separately approved future version.

Their presence in this section does not authorize architecture, dependencies, permissions, database structures, interface controls, placeholders, or preparatory code for them.

---

## 26. Supported OneDrive Scale

Design the application for large production OneDrive accounts.

The implementation target is:

- At least 5 million files and folders in one source drive.
- Several terabytes of source content.
- Transfers that may remain active for multiple days.
- Deep folder hierarchies.
- Microsoft Graph responses spanning a very large number of pages.

The figure of 5 million items is a required application design target, not a claim that it is the Microsoft service maximum.

### Production-pipeline synthetic validation benchmark

The project must include a repeatable synthetic stress-validation tool or automated benchmark that does not require a real Microsoft tenant.

The benchmark dataset must simulate at minimum:

- 5,000,000 files and folders.
- At least 10,000 Graph-style result pages.
- Deep and wide folder structures.
- Arabic, Unicode, long, duplicated, and sanitized paths.
- Mixed file sizes and metadata.
- Enumeration, manifest writing, bounded queuing, resume lookup, report writing, reconciliation, and mocked transfer completion.

The synthetic benchmark must exercise the same production components used by the application, including:

- The production page-by-page enumeration pipeline.
- The production bounded channels and queue limits.
- `PathMappingService`.
- The production manifest writer.
- The production manifest lookup and recovery implementation.
- The production report writer.
- The production reconciliation state model.
- The production transfer-state transitions.

Mocks may replace only:

- Microsoft Graph network transport.
- Authentication.
- Physical file-content download streams.
- Expensive creation of millions of physical destination files.

The benchmark must not use an independent simplified counter, alternate manifest implementation, alternate path mapper, or benchmark-only processing pipeline.

The benchmark must:

- Complete without out-of-memory failure.
- Avoid creating five million physical files.
- Record elapsed time, processed-item rate, peak managed heap, peak process working set, maximum queue depth, manifest segment count, manifest size, and report segment count.
- Demonstrate that queue depth remains within its configured bound.
- Demonstrate that the complete source hierarchy is never materialized in memory.
- Keep peak managed heap below 1 GiB under the default benchmark configuration on a compatible x64 Windows environment.
- Produce a machine-readable benchmark report.
- Fail with a non-zero exit code when an acceptance threshold is violated.
- Fail when `ProcessedItemCount` does not exactly match `SimulatedItemCount`.

The machine-readable benchmark report must include:

```text
BenchmarkSchemaVersion
ApplicationVersion
PathMappingVersion
SourceCommit
BuildConfiguration
RuntimeVersion
OperatingSystem
Processor
LogicalProcessorCount
InstalledMemory
GarbageCollectorMode
RandomSeed
ConfiguredQueueCapacity
SimulatedItemCount
SimulatedPageCount
ProcessedItemCount
ElapsedTime
ProcessedItemsPerSecond
PeakManagedHeapBytes
PeakWorkingSetBytes
MaximumQueueDepth
ManifestSegmentCount
ManifestTotalBytes
ReportSegmentCount
ThresholdResults
```

A statement that the code “theoretically supports” five million items is not sufficient evidence.

### Bounded-memory enumeration

The application must not assume that all source metadata can be held in memory simultaneously.

Do not build or retain an unbounded collection such as:

```csharp
List<DriveItem> allItems
```

Do not fully materialize the complete OneDrive hierarchy before processing it.

Enumeration must:

- Read Microsoft Graph results page by page.
- Follow each `@odata.nextLink` incrementally.
- Process or persist each page before requesting excessive additional pages.
- Use asynchronous streaming such as `IAsyncEnumerable<T>`, a bounded channel, or an equivalent bounded-memory design.
- Keep the number of queued metadata items bounded.
- Apply backpressure when download workers cannot keep up with enumeration.
- Avoid recursive algorithms whose call-stack depth grows with the folder hierarchy.
- Use an iterative traversal or another stack-safe approach.
- Release page objects and HTTP responses as soon as they are no longer needed.
- Keep normal working-memory usage bounded independently of the total source item count.

The mandatory local transfer manifest defined in Section 11 must be used for resume, verification, ordering, audit, and crash recovery.

At large scale:

- Persist manifest records incrementally inside `_TransferReport`.
- Do not keep the complete manifest in RAM.
- Keep manifest queues and indexes bounded.
- Partition or segment manifest files when required to maintain reliable bounded-memory operation.
- Do not introduce a database.
- The manifest must not contain access tokens, authentication headers, cookies, or temporary download URLs.

### Progress behavior at large scale

The application must not perform a memory-heavy pre-scan solely to calculate progress.

While enumeration is incomplete:

- Show `Current operation` as discovery or transfer activity.
- Show the number of discovered files.
- Show completed, skipped, and failed file counts as they change.
- Show downloaded size.
- Show overall progress as `Calculating` or `Unknown` when exact totals are unavailable.
- Use an indeterminate discovery progress state when an exact percentage is unavailable.

After enumeration completes, the overall progress may become determinate when reliable totals are available.

Do not add extra technical counters to the normal user interface and do not display a fabricated percentage.

### Multi-day operating conditions

The application must remain correct across:

- RDP session disconnection.
- RDP reconnection.
- Temporary network loss.
- Microsoft authentication token expiration.
- Temporary proxy or DNS failure.
- Windows clock correction or daylight-saving changes.
- System sleep and resume when sleep is permitted.
- User logoff, process termination, or server restart followed by manual application restart.

Rules:

- Use UTC and `DateTimeOffset` for persisted timestamps and expiry comparisons.
- Do not calculate token validity or retry deadlines from local wall-clock time alone.
- An RDP disconnect must not be treated as application cancellation.
- After sleep or network restoration, refresh authentication and retry eligible operations safely.
- User logoff, process termination, or server restart may stop the process; the mandatory manifest and checkpoints must allow safe continuation after the application is launched again.
- Do not require the original RDP session to resume the transfer state.
- Do not install or create a Windows Service in the current version.

### Multi-day reliability

For transfers that run for multiple days:

- Persist sufficient local checkpoint and mandatory manifest information incrementally.
- Flush reports, manifest records, and checkpoint data regularly.
- Do not wait until application shutdown to write all state.
- Recover safely after application restart, server restart, network interruption, token renewal, or process termination.
- Reuse verified completed files.
- Retry partial and failed work according to the existing transfer rules.
- Do not require re-enumerated metadata to remain in memory between runs.

---

## 27. Final Implementation Report

At completion, return only a concise report containing:

- Validation status: `Source Implementation Complete`, `Production Ready`, or `Not Complete`.
- Project path.
- Main files created.
- Restore result.
- Build result.
- Test result.
- Windows publish command.
- Expected publish output path.
- Source project directory path.
- Source-validation artifacts directory path.
- Published application directory path, only when Windows publish was actually completed.
- Stress benchmark result and report path.
- Peak managed heap, peak working set, maximum queue depth, manifest segment count, and processed-item rate when the benchmark was executed.
- `ManifestVersion`.
- `PathMappingVersion`.
- Configuration values still requiring the real `TenantId` and `ClientId`.
- Genuine remaining limitations.

If the current environment is not a compatible Windows environment, the final report must explicitly state:

- Windows compilation was not executed.
- WPF execution was not executed.
- Windows publish was not executed.
- Cross-platform restore, source generation, static validation, or non-Windows tests do not constitute a successful Windows build.

Never report an unexecuted Windows validation step as successful.

Do not repeat the full requirements in the final report.
