# Acceptance Matrix

This matrix identifies mandatory evidence. The binding contract and amendments remain authoritative.

| Area | Contract focus | Required evidence | Completion level |
|---|---|---|---|
| Repository layout | Amendment A-001 | Root solution inventory; no nested project container | Source |
| Durable evidence | Amendment A-002 | Committed redacted milestone summaries under `artifacts/evidence` | All phases |
| Solution structure | Sections 3, 13, 15, 16 | Solution inventory, restore result, Windows CI | Source |
| Enforceable quality gates | Amendment A-005 | GitHub Actions checks and actual check results | Source |
| Authentication | Sections 7, 19; A-009 | Unit tests; token-cache protection; real Windows sign-in | Source + Production |
| Permission threat model | Amendment A-009 | Approved delegated-permission assessment | Source + Production |
| OneDrive root validation | Section 8 | URL/drive tests; real tenant validation | Source + Production |
| Local destination | Section 9; A-007 | Local-path, mapped-drive, reparse-point tests | Source |
| Destination containment | Amendment A-007 | Adversarial junction-swap and final-path tests | Source + Windows |
| NTFS and storage protection | Amendment A-008 | ACL validation; volume encryption or approved exception | Production |
| Destination locking | Section 9 | Cross-process/session Windows test | Production |
| Enumeration | Sections 10, 26 | Bounded page pipeline tests and benchmark | Source + Production |
| Path mapping | Section 11 | `PathMappingVersion = 1` tests | Source |
| Manifest index | Amendment A-010 | Approved architecture; lookup/recovery/scale evidence | Source |
| Manifest | Section 11 | Crash, recovery, version, segmentation tests | Source |
| Transfer/resume | Section 10 | Range, restart, partial, throttle tests | Source |
| Download integrity | Section 10; A-006 | Source-hash tests; local SHA-256; corruption detection | Source |
| Reports and audit | Section 12; A-012 | Segmentation, UTF-8, formula protection, protected audit fields | Source |
| Reconciliation | Section 10 | Three-pass and source-change tests | Source + Production |
| Cancellation | Section 10 | Incomplete enumeration and discovered-item tests | Source |
| UI | Section 6 | View-model tests; WPF startup and review | Production |
| Large scale | Section 26 | 5,000,000-item production-pipeline benchmark | Production |
| Supply chain | Amendment A-013 | Lock strategy, vulnerability scan, secret scan, SBOM | Source + Production |
| Code signing | Amendment A-013 | Authenticode signature or approved documented limitation | Production |
| Access removal | Amendment A-009 | External SCA grant/removal and verification record | Production |
| Publish | Sections 16, 24 | Self-contained `win-x64` output tied to source commit | Production |
| Windows Server execution | Section 24 | Application executes on Windows Server 2019 | Production |

## Completion rules

### Documentation Ready

- Contract, amendments, and control documents exist.
- No application implementation is claimed.
- M0 uses `DOCUMENTATION_COMPLETE`.

### Source Implementation Complete

- Complete source exists at repository root structure.
- Supported restore, static checks, CI, unit tests, security tests, and synthetic validation were executed.
- Each completed milestone has a committed redacted evidence summary.
- Windows-only production checks may remain explicitly unexecuted.
- Must not be presented as production ready.

### Production Ready

Requires all production-acceptance evidence, including:

- Windows Release build and automated tests
- WPF startup
- Microsoft interactive sign-in
- Real employee OneDrive root validation
- Complete backup transfer
- Interruption and resume
- Final reconciliation
- Destination locking across processes or sessions
- Continuous reparse-point containment
- Restricted NTFS ACL validation
- Volume encryption or approved exception
- Protected audit records
- External Site Collection Administrator access removal and verification
- Dependency scan, secret scan, SBOM, and source-commit traceability
- Authenticode signing or approved documented limitation
- Self-contained `win-x64` publish
- Execution on Windows Server 2019
- Large-scale benchmark acceptance

## Evidence rules

Every evidence summary must state:

- schema version and milestone
- date and time in UTC
- source commit and application version where available
- environment and operating system
- command or action executed
- result and exit code
- relevant counts or thresholds
- raw output path or CI artifact
- limitations and unexecuted checks
- redaction confirmation

Never infer success from an unexecuted step, a checked PR-template box, an ignored local file, or a verbal claim.