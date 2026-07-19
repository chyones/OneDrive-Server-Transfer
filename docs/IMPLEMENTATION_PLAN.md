# Implementation Plan

This plan implements the simple workflow defined in `IMPLEMENTATION_CONTRACT.md`. It does not expand product scope.

## M0 — Contract simplification and correction

**Status:** `IN_PROGRESS`

Goals:

- make `IMPLEMENTATION_CONTRACT.md` the single binding contract
- align every control document with the actual IT workflow
- remove the custom five-million-item benchmark and custom index engine as release blockers
- approve local SQLite state for resume and recovery
- require Graph delta for initial inventory and reconciliation
- correct invalid M0 evidence and close the unresolved review concern

Exit criteria:

- no binding document contradicts the simple product workflow
- previous invalid M0 evidence is explicitly superseded
- corrected documentation is reviewed and merged
- a valid evidence summary references the exact merged source commit

## M1 — Solution and CI foundation

Goals:

- create `OneDriveServerTransfer.sln` at repository root
- create WPF application and test projects
- configure .NET 10 Windows targeting
- add MVVM, dependency injection, structured logging, and configuration
- add deterministic dependency restore
- add Windows GitHub Actions for restore, Release build, tests, formatting/static analysis, dependency review, and secret detection

Exit criteria:

- solution structure matches the contract
- Windows CI builds and tests real source
- no placeholder production services
- committed M1 evidence exists

## M2 — Microsoft authentication

Goals:

- implement MSAL interactive delegated sign-in
- support MFA and Conditional Access
- implement silent token acquisition and renewal
- implement DPAPI-protected application token cache
- implement `Remember sign-in` and sign-out accurately
- prevent secret and token logging

Exit criteria:

- authentication logic is isolated from WPF views
- cache behavior and redaction tests pass
- no client secret or write permission exists
- real Microsoft sign-in remains unclaimed until executed
- committed M2 evidence exists

## M3 — Employee OneDrive validation

Goals:

- validate HTTPS and configured tenant host
- resolve one employee personal-site root URL
- resolve its default `driveType = business` drive root
- reject files, subfolders, shared folders, consumer OneDrive, and SharePoint or Teams libraries
- validate administrator read access
- show a simple employee and source confirmation

Exit criteria:

- URL and source-type tests pass
- Graph v1.0 only
- no source modification path exists
- committed M3 evidence exists

## M4 — Local destination and source binding

Goals:

- accept only local attached storage
- reject UNC, mapped, NAS, SMB, and remote destinations
- create `OneDriveData` and `_TransferReport`
- bind destination to tenant, employee, and drive
- reject another source or unsafe non-empty destination
- implement cross-process and cross-session destination locking
- implement deterministic `PathMappingVersion = 1`
- validate destination containment and safe file operations
- check write access and disk-space headroom

Exit criteria:

- path, source-binding, lock, and mapping tests pass
- file operations cannot escape the selected destination
- unrelated local files cannot be overwritten
- committed M4 evidence exists

## M5 — Inventory, copy, resume, and verification

Goals:

- implement Graph delta initial inventory page by page
- persist `@odata.deltaLink` safely
- use bounded queues and fixed concurrency of three
- create folders and copy files through streaming
- implement `.partial` handling and Range resume
- isolate temporary download-host requests from Graph credentials
- implement bounded retry and throttling
- implement source metadata revalidation
- verify supported source hashes
- calculate local SHA-256
- implement SQLite transaction state and crash recovery
- perform up to three bounded delta reconciliation passes

Exit criteria:

- paging, checkpoint, copy, resume, retry, hash, SQLite recovery, and reconciliation tests pass
- no complete-drive hierarchy is retained in memory
- rerun skips verified completed files
- committed M5 evidence exists

## M6 — UI, errors, and reports

Goals:

- complete the single-window WPF interface
- implement responsive progress and cancellation
- keep activity history bounded
- map technical failures to simple reference-coded errors
- generate `TransferSummary.json`, `TransferReport.csv`, `FailedFiles.csv`, and `TransferLog.log`
- protect CSV output from formula injection

Exit criteria:

- view-model tests pass
- no technical secrets or Graph internals appear in the UI
- reports are readable and state remains in SQLite
- committed M6 evidence exists

## M7 — Windows and real-tenant acceptance

Goals:

- run Release build and automated tests on compatible Windows
- start the WPF application
- validate real Microsoft sign-in
- validate a real employee test OneDrive root
- run a complete test copy
- test network interruption and resume
- test source change reconciliation
- test destination locking across processes or sessions
- verify local destination permissions and containment
- publish self-contained `win-x64`

Exit criteria:

- every mandatory Windows and real-tenant acceptance item passes
- failures and unexecuted checks are reported explicitly
- committed M7 evidence exists

## M8 — Internal release

Goals:

- prepare the approved self-contained Windows package
- generate SBOM and dependency scan results where tooling supports them
- apply Authenticode signing when an approved certificate is available
- complete installation and operating instructions
- tie the release to an exact source commit

Exit criteria:

- release package runs on Windows Server 2019
- signing status or approved unsigned limitation is documented
- final evidence and handoff are complete
- project may be marked `Production Ready` only when M7 and M8 acceptance evidence passes