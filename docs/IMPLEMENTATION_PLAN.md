# Implementation Plan

This plan organizes the binding contract into controlled milestones. It does not replace `IMPLEMENTATION_CONTRACT_AMENDMENTS.md` or `IMPLEMENTATION_CONTRACT.md`.

The repository root is the project root. Every completed implementation milestone requires a committed redacted evidence summary under `artifacts/evidence`.

## M0 — Repository and contract readiness

**Status:** `DOCUMENTATION_COMPLETE`

Deliverables:

- Approved base implementation contract
- Binding contract amendments
- README and agent operating rules
- Security policy and security/integrity requirements
- Environment-input checklist
- Acceptance matrix
- Evidence policy
- Change-control process
- AI memory and phase tracking

Exit evidence:

- Contract and amendments exist and have a defined authority order
- Repository contains no application code falsely presented as complete
- Phase status says implementation has not started
- Repository-root, evidence, security, and completion-state contradictions are corrected

## M1 — Solution foundation and enforceable CI foundation

Goals:

- Create `./OneDriveServerTransfer.sln` at repository root
- Create WPF application and test projects under `src` and `tests`
- Configure .NET 10 Windows targeting
- Establish MVVM, dependency injection, structured logging, and configuration foundations
- Add `appsettings.example.json`
- Enforce `MaximumRetryAttempts` range `1..5`
- Add deterministic dependency restore or lock-file strategy
- Add build and validation scripts
- Add enforceable GitHub Actions checks for restore, Release build, tests, static checks, dependency review, and secret detection
- Add automated policy checks against prohibited APIs, write permissions, and remote destination support when practical

Exit criteria:

- Solution path and project structure match the amended contract
- Solution restores where supported
- Compatible Windows CI performs a real Release build and automated tests
- No placeholder production services
- No checked PR-template item is treated as validation evidence
- Redacted M1 evidence summary committed under `artifacts/evidence`

## M2 — Authentication and configuration

Goals:

- Implement MSAL interactive sign-in
- Support MFA and Conditional Access
- Implement silent token acquisition and renewal
- Implement DPAPI token-cache persistence on Windows
- Implement Remember sign-in and sign-out behavior
- Enforce public-client and no-secret design
- Protect the application-owned token cache from unrelated non-administrative users
- Document the threat impact of delegated permissions and persistent session access

Exit criteria:

- Authentication services are isolated from UI
- Token and secret logging protections are tested
- Token-cache ACL behavior is tested on Windows where required
- No client secret or Microsoft 365 write permission exists
- Non-Windows limitations are explicitly reported
- Windows interactive sign-in remains unvalidated until actually executed
- Redacted M2 evidence summary committed

## M3 — Employee OneDrive root resolution and validation

Goals:

- Validate HTTPS and allowed tenant host
- Resolve employee personal-site root URL
- Resolve default Graph drive root
- Require `driveType = business`
- Reject files, subfolders, shared folders, consumer OneDrive, and SharePoint libraries
- Validate administrator read access
- Record protected audit identities required by the amended contract

Exit criteria:

- URL and drive-type test matrix passes
- User-facing error mappings exist
- No beta or fallback Microsoft APIs are used
- Real tenant behavior remains unvalidated until actually executed
- Redacted M3 evidence summary committed

## M4 — Local destination, locking, containment, ACL, and path mapping

Goals:

- Accept only local attached storage
- Reject UNC, network drives, and unsafe reparse points
- Create `OneDriveData` and `_TransferReport`
- Implement cross-process and cross-session destination lock
- Implement deterministic `PathMappingVersion = 1`
- Revalidate containment before create, open, replace, and rename operations
- Prevent reparse-point time-of-check/time-of-use redirection
- Validate restricted NTFS access-control baseline without broadly weakening ACLs

Exit criteria:

- Destination validation and lock tests pass
- Path-mapping contract tests pass
- Existing unsupported versions are rejected safely
- Adversarial junction-swap tests pass
- No file operation can escape the canonical destination boundary
- ACL validation behavior is tested and documented
- Redacted M4 evidence summary committed

## M5 — Enumeration, disk-based manifest indexing, and reporting

Precondition:

Before implementation, add an approved decision describing the five-million-item on-disk index design, including lookup keys, file format, crash safety, recovery, integrity, expected complexity, and benchmark behavior.

Goals:

- Implement page-by-page bounded enumeration
- Prevent traversal of external `remoteItem` content
- Create segmented versioned manifest
- Implement a genuine bounded-memory disk-based lookup index
- Implement explicit persisted transfer states
- Create segmented complete and failed CSV reports
- Implement formula-injection protection and indexes
- Include all required protected audit fields

Exit criteria:

- No full-drive metadata materialization
- Queue capacity is bounded
- Routine lookups do not require repeated complete scans of all JSONL segments
- Crash-safe manifest and index recovery tests pass
- CSV segmentation and integrity tests pass
- Index lookup and collision behavior passes scale-oriented validation
- Redacted M5 evidence summary committed

## M6 — Transfer, resume, local SHA-256, and source integrity

Goals:

- Implement streaming downloads
- Implement fixed maximum of three simultaneous downloads
- Implement `.partial` handling
- Implement HTTP Range resume
- Isolate temporary download URLs from Graph bearer tokens
- Implement post-download source metadata revalidation
- Verify supported Graph source hashes
- Calculate and store streaming local SHA-256 for every completed file
- Revalidate local SHA-256 before trusting recovered completed state
- Implement bounded retry and throttling behavior

Exit criteria:

- Range and restart scenarios pass
- Hash and fallback verification tests pass
- Same-size local substitution or corruption is detected
- Temporary URLs are never persisted or logged
- Completed state cannot precede final-file commitment
- Retry configuration cannot exceed five attempts
- Redacted M6 evidence summary committed

## M7 — Reconciliation, cancellation, errors, and UI completion

Goals:

- Implement a maximum of three reconciliation passes
- Use supported Graph v1.0 change tracking where available
- Preserve local backups after source deletion
- Implement cancellation scope accurately
- Complete the bounded activity area and final summaries
- Map technical failures to clear reference-coded user errors
- Produce protected run summaries with required audit fields

Exit criteria:

- Reconciliation outcomes pass
- `CompletedWithWarnings` and `CompletedWithErrors` are correct
- Cancellation never claims undiscovered items were evaluated
- UI exposes no internal Graph or diagnostic details
- Audit records are sufficient without exposing internal identifiers in normal UI
- Redacted M7 evidence summary committed

## M8 — Automated tests, adversarial security tests, and production-pipeline benchmark

Goals:

- Complete the binding contract test matrix
- Add adversarial destination-containment and local-integrity tests
- Add the 5,000,000-item synthetic benchmark
- Exercise production enumeration, queues, path mapping, on-disk index, manifest, report, state, and reconciliation components
- Generate machine-readable evidence

Exit criteria:

- Processed item count exactly matches configured item count
- Peak managed heap remains below the contract threshold on compatible Windows
- Queue capacity remains bounded
- No benchmark-only alternate implementation exists
- Manifest-index lookups remain operational at scale
- Junction replacement, local corruption, and unsafe ACL cases are tested
- Redacted M8 evidence summary committed

## M9 — Windows build, SBOM, signing decision, and self-contained publish

Goals:

- Run Release build on compatible Windows
- Execute automated tests on Windows
- Start the WPF application
- Publish self-contained `win-x64`
- Place successful publish under `artifacts/win-x64`
- Generate an SBOM
- Run dependency-vulnerability and secret scans
- Associate output with an exact source commit
- Apply Authenticode signing when an approved organizational certificate is available
- Document any approved unsigned-build limitation

Exit criteria:

- Build, tests, startup, scan, SBOM, and publish evidence exist
- Published application executes on Windows Server 2019
- Signature verification passes when signing is required and available
- Unexecuted checks are not marked passed
- Redacted M9 evidence summary committed

## M10 — Production acceptance

Goals:

- Configure real Entra application values
- Validate Microsoft interactive sign-in
- Validate real employee test OneDrive access
- Run a complete test backup
- Test interruption and resume
- Test reconciliation during source changes
- Test destination locking across processes or sessions
- Validate continuous containment and production NTFS ACL baseline
- Confirm BitLocker or approved equivalent/exception
- Execute the production-pipeline benchmark
- Verify protected audit records
- Remove and verify removal of temporary Site Collection Administrator access after the test

Exit criteria:

- Every binding production-acceptance item passes
- External Site Collection Administrator grant/removal record exists
- Security, supply-chain, Windows, benchmark, and real-tenant evidence exists
- The project may be marked `Production Ready`
- Remaining limitations are genuine and documented
- Redacted M10 evidence summary committed