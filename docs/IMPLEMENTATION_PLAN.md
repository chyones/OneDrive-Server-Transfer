# Implementation Plan

This plan organizes the approved contract into controlled milestones. It does not replace the contract.

The implementation agent may execute all milestones in one working session, but must update `.ai/PHASE_STATUS.md` and evidence after every milestone.

## M0 — Repository and contract readiness

**Status at repository preparation:** complete.

Deliverables:

- Approved implementation contract
- README
- AI execution and memory files
- Security policy
- Environment-input checklist
- Acceptance matrix
- Change-control process

Exit evidence:

- Contract exists and is treated as immutable without approval
- Repository contains no application code falsely presented as complete
- Phase status says implementation has not started

## M1 — Solution foundation

Goals:

- Create the .NET 10 solution
- Create WPF application and test projects
- Configure Windows targeting
- Establish MVVM, dependency injection, logging, and configuration foundations
- Add `appsettings.example.json`
- Add build and validation scripts

Exit criteria:

- Solution restores where supported
- Project structure matches the contract
- No placeholder production services
- Source inventory and restore evidence stored under `artifacts/source`

## M2 — Authentication and configuration

Goals:

- Implement MSAL interactive sign-in
- Support MFA and Conditional Access
- Implement silent token acquisition and renewal
- Implement DPAPI token-cache persistence on Windows
- Implement Remember sign-in and sign-out behavior
- Enforce public-client and no-secret design

Exit criteria:

- Authentication services are isolated from UI
- Token and secret logging protections are tested
- Non-Windows limitations are explicitly reported
- Windows interactive sign-in remains unvalidated until actually executed

## M3 — Employee OneDrive root resolution and validation

Goals:

- Validate HTTPS and allowed tenant host
- Resolve employee personal-site root URL
- Resolve default Graph drive root
- Require `driveType = business`
- Reject files, subfolders, shared folders, consumer OneDrive, and SharePoint libraries
- Validate administrator read access

Exit criteria:

- URL and drive-type test matrix passes
- User-facing error mappings exist
- No beta or fallback Microsoft APIs are used

## M4 — Local destination, locking, and path mapping

Goals:

- Accept only local attached storage
- Reject UNC, network drives, and unsafe reparse points
- Create `OneDriveData` and `_TransferReport`
- Implement cross-process and cross-session destination lock
- Implement deterministic `PathMappingVersion = 1`

Exit criteria:

- Destination validation and lock tests pass
- Path-mapping contract tests pass
- Existing unsupported versions are rejected safely

## M5 — Enumeration, manifest, and reporting

Goals:

- Implement page-by-page bounded enumeration
- Prevent traversal of external `remoteItem` content
- Create segmented versioned manifest
- Implement explicit persisted transfer states
- Create segmented complete and failed CSV reports
- Implement formula-injection protection and indexes

Exit criteria:

- No full-drive metadata materialization
- Queue capacity is bounded
- Crash-safe manifest recovery tests pass
- CSV segmentation and integrity tests pass

## M6 — Transfer, resume, and integrity

Goals:

- Implement streaming downloads
- Implement fixed maximum of three simultaneous downloads
- Implement `.partial` handling
- Implement HTTP Range resume
- Isolate temporary download URLs from Graph bearer tokens
- Implement post-download source metadata revalidation
- Verify supported Graph source hashes
- Implement bounded retry and throttling behavior

Exit criteria:

- Range and restart scenarios pass
- Hash and fallback verification tests pass
- Temporary URLs are never persisted or logged
- Completed state cannot precede final-file commitment

## M7 — Reconciliation, cancellation, errors, and UI completion

Goals:

- Implement a maximum of three reconciliation passes
- Use supported Graph v1.0 change tracking where available
- Preserve local backups after source deletion
- Implement cancellation scope accurately
- Complete the bounded activity area and final summaries
- Map technical failures to clear reference-coded user errors

Exit criteria:

- Reconciliation outcomes pass
- `CompletedWithWarnings` and `CompletedWithErrors` are correct
- Cancellation never claims undiscovered items were evaluated
- UI exposes no internal Graph or diagnostic details

## M8 — Automated tests and production-pipeline benchmark

Goals:

- Complete the contract test matrix
- Add the 5,000,000-item synthetic benchmark
- Exercise production enumeration, queues, path mapping, manifest, report, state, and reconciliation components
- Generate machine-readable evidence

Exit criteria:

- Processed item count exactly matches configured item count
- Peak managed heap remains below the contract threshold on compatible Windows
- Queue capacity remains bounded
- No benchmark-only alternate implementation exists

## M9 — Windows build and self-contained publish

Goals:

- Run Release build on compatible Windows
- Execute automated tests on Windows
- Start the WPF application
- Publish self-contained `win-x64`
- Place successful publish under `artifacts/win-x64`

Exit criteria:

- Build, tests, startup, and publish evidence exist
- Published application executes on Windows Server 2019
- Unexecuted checks are not marked passed

## M10 — Production acceptance

Goals:

- Configure real Entra application values
- Validate Microsoft interactive sign-in
- Validate real employee test OneDrive access
- Run a complete test backup
- Test interruption and resume
- Test reconciliation during source changes
- Test destination locking across processes or sessions
- Execute the production-pipeline benchmark

Exit criteria:

- Every contract production-acceptance item passes
- The project may be marked `Production Ready`
- Remaining limitations are genuine and documented
