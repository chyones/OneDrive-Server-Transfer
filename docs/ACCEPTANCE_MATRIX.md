# Acceptance Matrix

The binding contract is `IMPLEMENTATION_CONTRACT.md`.

| Area | Required evidence | Completion level |
|---|---|---|
| Contract consistency | Reviewed documents aligned with the simple IT workflow | Documentation |
| Solution structure | Root solution inventory, restore result, Windows CI | Source |
| Authentication | Unit tests, token-cache protection, real Windows sign-in | Source + Production |
| OneDrive validation | URL and drive tests, real tenant validation | Source + Production |
| Delta inventory | Page processing, checkpoint persistence, recovery tests | Source |
| Local destination | Local-path rejection tests, source binding, write and space checks | Source + Windows |
| Destination locking | Cross-process/session Windows test | Production |
| Path safety | Mapping, containment, reparse-point, traversal, and unrelated-file tests | Source + Windows |
| SQLite state | Transactions, crash recovery, schema version, rerun lookup | Source |
| Transfer and resume | Streaming, Range, restart, partial, retry, throttle tests | Source |
| Credential isolation | No Graph credentials sent to temporary download hosts | Source |
| Integrity | Source-hash tests, local SHA-256, corruption detection | Source |
| Reconciliation | Graph delta changes, maximum three passes, warning outcome | Source + Production |
| Cancellation | Safe stop, retained completed and partial files, accurate summary | Source |
| UI | View-model tests, WPF startup and operational review | Production |
| Reports | UTF-8, escaping, formula protection, summary and failed records | Source |
| Supply chain | Dependency lock strategy, vulnerability scan, secret scan, SBOM where available | Release |
| Publish | Self-contained `win-x64` tied to exact source commit | Production |
| Windows Server | Published application executes on Windows Server 2019 | Production |

## Documentation Ready

Requires:

- one binding contract
- simple workflow clearly defined
- no unresolved binding contradictions
- previous invalid evidence explicitly superseded
- corrected documentation reviewed and merged
- valid documentation evidence tied to an exact commit

## Source Implementation Complete

Requires:

- complete application source
- Windows CI restore, Release build, and automated tests
- implemented Graph delta inventory
- implemented local SQLite state and recovery
- implemented transfer, resume, integrity, destination binding, UI, and reports
- no placeholder or fake production behavior
- committed evidence for each completed source milestone

This status must not be represented as Production Ready.

## Production Ready

Requires actual evidence for:

- Windows Release build and automated tests
- WPF startup
- Microsoft interactive sign-in
- real employee OneDrive validation
- complete copy to local storage
- interruption and resume
- source-change reconciliation
- destination locking across processes or sessions
- destination containment and NTFS access review
- self-contained `win-x64` publish
- execution on Windows Server 2019
- release traceability and documented signing status

## Evidence rules

Every completed milestone evidence summary must contain:

- evidence schema version
- milestone
- exact validated source commit
- UTC execution time
- environment and operating system
- command or action executed
- result and exit code
- relevant test counts or thresholds
- raw artifact or CI location when applicable
- limitations and unexecuted checks
- redaction confirmation

A mutable branch name alone is not evidence. An unexecuted command, checked box, ignored file, or verbal claim is not evidence.