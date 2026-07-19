# Binding Implementation Contract Amendments

## Status and authority

This document contains approved corrections to `IMPLEMENTATION_CONTRACT.md` identified during the pre-implementation security and consistency review.

The binding implementation requirements consist of:

1. `IMPLEMENTATION_CONTRACT_AMENDMENTS.md`
2. `IMPLEMENTATION_CONTRACT.md`

When this document conflicts with `IMPLEMENTATION_CONTRACT.md`, this document prevails. Requirements not changed here remain fully binding.

These amendments do not authorize new product features. They correct repository structure, evidence handling, integrity, security, audit, and implementation-governance defects before application code is created.

---

## A-001 — Repository root is the project root

The repository root is the complete source-project root.

Create the solution directly at:

```text
./OneDriveServerTransfer.sln
```

Required structure:

```text
OneDrive-Server-Transfer/
├── .ai/
├── .github/
├── artifacts/
│   ├── evidence/
│   ├── source/
│   └── win-x64/
├── docs/
├── scripts/
├── src/
│   └── OneDriveServerTransfer.App/
├── tests/
│   └── OneDriveServerTransfer.Tests/
├── AGENTS.md
├── IMPLEMENTATION_CONTRACT.md
├── IMPLEMENTATION_CONTRACT_AMENDMENTS.md
├── OneDriveServerTransfer.sln
├── appsettings.example.json
├── README.md
└── SECURITY.md
```

Do not create a second nested `./OneDriveServerTransfer` project directory.

The prior Section 4 instruction requiring all project files under `./OneDriveServerTransfer` is superseded.

Implementation agents may update repository-level governance files in `.ai`, `docs`, `.github`, `AGENTS.md`, `README.md`, `SECURITY.md`, and the contract files as required by approved change control.

---

## A-002 — Evidence locations and retention

Use these locations:

```text
artifacts/source
```

for generated source-validation output,

```text
artifacts/win-x64
```

for generated Windows publish output, and

```text
artifacts/evidence
```

for small, redacted, durable evidence summaries committed to Git.

Raw build output, test result directories, logs, benchmark working data, and published binaries remain ignored by Git unless explicitly approved.

Every completed milestone must commit a redacted evidence summary under `artifacts/evidence`. A phase-status entry without a committed evidence summary is not valid completion evidence.

Evidence summaries must contain:

- UTC timestamp
- source commit
- environment and operating system
- exact command or action
- result and exit code
- relevant counts or thresholds
- raw artifact location when applicable
- limitations and unexecuted checks
- confirmation that secrets and employee data were excluded

No evidence summary may contain passwords, tokens, cookies, authorization headers, temporary download URLs, employee file contents, or sensitive production paths.

---

## A-003 — Completion-state correction

`SOURCE_COMPLETE` must never be used for repository documentation preparation.

Allowed phase states are:

- `NOT_STARTED`
- `IN_PROGRESS`
- `BLOCKED`
- `DOCUMENTATION_COMPLETE`
- `SOURCE_COMPLETE`
- `WINDOWS_VALIDATED`
- `PRODUCTION_VALIDATED`

M0 uses `DOCUMENTATION_COMPLETE`.

`SOURCE_COMPLETE` requires implemented source code, supported restore/build/static validation, executed automated tests, and committed evidence summaries.

---

## A-004 — Milestone execution and review gates

The instruction to build the complete application in one execution does not permit bypassing milestone gates.

Implementation must proceed milestone by milestone. Before starting the next milestone, the agent must:

1. Complete the current milestone exit criteria.
2. Execute all checks available in the current environment.
3. Commit the required redacted evidence summary.
4. Update phase status, decision log, project memory, and handoff.
5. Review the current diff for scope, security, incomplete work, placeholders, and false claims.

An agent may continue through several milestones in one working session only when each milestone is independently completed and evidenced.

The agent must not create one unreviewable implementation commit covering all source milestones. Changes must be separated into intentional milestone commits or pull requests.

---

## A-005 — Real quality gates

The existing pull-request template is a review checklist, not a quality gate.

M1 must add enforceable GitHub Actions checks appropriate to the implemented solution. At minimum, before source implementation may be marked complete, pull requests must run:

- dependency restore
- Release build on compatible Windows
- automated tests
- formatting or static-analysis checks selected by the implementation
- dependency vulnerability review
- secret detection
- checks preventing Microsoft Graph beta, SharePoint REST, CSOM, write permissions, and prohibited destination support

Required branch-protection settings are an administrative repository action and must be documented separately when the workflows exist. A checked PR-template box is never evidence that a command passed.

---

## A-006 — Local SHA-256 integrity record

For every successfully downloaded file, calculate a local SHA-256 hash after the complete `.partial` content has been written and before or immediately after atomic final-file commitment.

Store the local SHA-256 in the manifest and complete report.

When Microsoft Graph provides a supported source hash:

- verify the corresponding local hash against the source hash
- also retain the local SHA-256 for future local-integrity verification

When Microsoft Graph provides no supported source hash:

- do not claim source cryptographic verification
- record verification as source identity, byte count, post-download metadata revalidation, and locally recorded SHA-256

On rerun or recovery, a file may not remain accepted as `Completed` when its recorded local SHA-256 does not match its current content.

Hash calculation must be streaming and bounded in memory.

---

## A-007 — Reparse-point TOCTOU protection

Destination validation at run start is necessary but not sufficient.

Before creating, opening, replacing, or renaming every transfer file or directory, the application must prevent time-of-check/time-of-use redirection through symbolic links, junctions, mount points, or other reparse points.

The implementation must:

- revalidate relevant path ancestors during operations
- open or inspect paths without blindly following reparse points
- verify the final resolved path remains inside the canonical selected destination and correct `OneDriveData` or `_TransferReport` boundary
- repeat validation before final rename or atomic replacement
- fail safely if a path changes during the operation
- include tests that replace a destination child with a junction during transfer

A one-time startup path check does not satisfy this requirement.

---

## A-008 — NTFS access control and storage protection

Production acceptance requires a documented access-control baseline for the application directory, token-cache location, `OneDriveData`, and `_TransferReport`.

At minimum:

- run under a dedicated Windows account where operationally possible
- restrict destination and report access to that account and authorized local administrators
- detect and warn or fail when inherited ACLs expose employee backup data broadly
- protect the application-owned token cache from other non-administrative users
- record the effective access-control validation result without exposing sensitive ACL details in the normal UI
- require BitLocker or equivalent approved full-volume protection for production backup storage, unless a documented organizational exception is approved

The application must not silently weaken ACLs, grant broad access, or disable security products.

---

## A-009 — Microsoft access lifecycle and least privilege

Use a dedicated administrator account for transfer operations where operationally possible. The account must not be a normal daily-use account.

The security documentation must contain a threat-model decision for the delegated permissions requested by the application, including the impact of `Files.Read.All`, `Sites.Read.All`, and `offline_access`.

Before production acceptance, evaluate whether a narrower supported Microsoft permission model can meet the validated workflow. Do not silently change the approved authentication model; record the result as an approved security decision.

Site Collection Administrator access remains assigned outside the application. After each completed or cancelled production transfer, the operational procedure must require:

1. Remove the temporary Site Collection Administrator access when it is no longer required.
2. Verify removal through Microsoft 365 administration tools.
3. Record the grant time, transfer run ID, removal time, and person performing the removal in an external administrative record.

The application must not automatically modify Microsoft 365 permissions.

---

## A-010 — Manifest lookup architecture must be decided before M5

The manifest design must support efficient bounded-memory lookup, recovery, path collision detection, and reconciliation at five million items.

A small segment list containing only file names and record counts is not a sufficient lookup index.

Before M5 implementation, record an approved design decision describing:

- lookup keys, including source drive ID plus item ID
- path-mapping and collision lookup
- on-disk index format
- crash-safe update behavior
- index recovery and integrity validation
- bounded memory behavior
- expected lookup complexity
- compaction or migration behavior
- five-million-item benchmark method

The current no-database rule remains binding unless separately changed through explicit contract approval. If no database is used, the implementation must define and test a genuine disk-based index rather than repeatedly scanning all JSONL segments for routine lookups.

M5 may not be marked complete without benchmark evidence demonstrating the selected index design.

---

## A-011 — Retry limit is bounded

`MaximumRetryAttempts` may remain in `appsettings.example.json`, but its valid range is `1` through `5` inclusive.

The application must reject configuration values outside this range at startup with a clear operational error.

No configuration source, environment variable, command-line argument, or registry value may raise the limit above five attempts per file.

---

## A-012 — Required audit fields

Every transfer run summary and manifest index must record, in protected operational metadata:

- run identifier
- application version
- source commit or build identifier
- Windows server name
- Windows execution-account identifier
- signed-in Microsoft account identifier
- tenant identifier
- target employee identifier
- canonical OneDrive personal-site URL or a protected/redacted equivalent suitable for audit
- source drive identifier
- canonical destination root
- start and completion UTC timestamps
- enumeration completion state
- final result
- reconciliation outcome
- counts by item status
- `ManifestVersion`
- `PathMappingVersion`
- configuration fingerprint excluding secrets

User-facing summaries must remain simple and must not expose internal identifiers unnecessarily.

---

## A-013 — Code signing and software supply chain

Before Production Ready status:

- publish reproducibly from a documented source commit
- generate an SBOM for the Windows deliverable
- record dependency versions through lock files or an equivalent deterministic restore mechanism
- scan dependencies for known vulnerabilities
- sign release binaries using an approved Authenticode certificate when the organization provides one
- document signature verification and the behavior when no organizational signing certificate is available

Unsigned development builds may be used for source and test validation but must not be misrepresented as approved production releases.

---

## A-014 — Documentation-only preparation boundary

This correction phase changes documentation and repository controls only. It does not implement the application, create placeholder services, generate fake evidence, or claim that any source, Windows, Microsoft 365, benchmark, or production validation has occurred.
