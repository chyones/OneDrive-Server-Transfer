# Acceptance Matrix

This matrix identifies mandatory evidence. The implementation contract remains authoritative.

| Area | Contract focus | Required evidence | Completion level |
|---|---|---|---|
| Solution structure | Sections 3, 4, 13, 15, 16 | Solution inventory, restore result | Source |
| Authentication | Sections 7, 19 | Unit tests; real Windows interactive sign-in | Production |
| OneDrive root validation | Section 8 | URL/drive tests; real tenant validation | Source + Production |
| Local destination | Section 9 | Local-path, mapped-drive, reparse-point tests | Source |
| Destination locking | Section 9 | Cross-process/session Windows test | Production |
| Enumeration | Sections 10, 26 | Bounded page pipeline tests and benchmark | Source + Production |
| Path mapping | Section 11 | `PathMappingVersion = 1` tests | Source |
| Manifest | Section 11 | Crash, recovery, version, segmentation tests | Source |
| Transfer/resume | Section 10 | Range, restart, partial, throttle tests | Source |
| Download integrity | Section 10 | Hash, mismatch, metadata-change tests | Source |
| Reports | Section 12 | Segmentation, index, UTF-8, formula-protection tests | Source |
| Reconciliation | Section 10 | Three-pass and source-change tests | Source + Production |
| Cancellation | Section 10 | Incomplete enumeration and discovered-item tests | Source |
| UI | Section 6 | View-model tests; WPF startup and review | Production |
| Large scale | Section 26 | 5,000,000-item production-pipeline benchmark | Production |
| Publish | Sections 16, 24 | Self-contained `win-x64` output | Production |
| Windows Server execution | Section 24 | Application executes on Windows Server 2019 | Production |

## Completion rules

### Documentation Ready

- Contract and control documents exist.
- No application implementation is claimed.

### Source Implementation Complete

- Complete source exists.
- Supported restore, static checks, unit tests, and synthetic validation were executed.
- Windows-only checks may remain explicitly unexecuted.
- Must not be presented as production ready.

### Production Ready

Requires all production-acceptance evidence, including:

- Windows Release build
- Windows automated tests
- WPF startup
- Microsoft interactive sign-in
- Real employee OneDrive root validation
- Complete backup transfer
- Interruption and resume
- Final reconciliation
- Destination locking across processes or sessions
- Self-contained `win-x64` publish
- Execution on Windows Server 2019
- Large-scale benchmark acceptance

## Evidence rules

Every evidence item must state:

- Date and time in UTC
- Application or commit version
- Environment
- Command or action executed
- Result
- Output path
- Limitations

Never infer success from an unexecuted step.
