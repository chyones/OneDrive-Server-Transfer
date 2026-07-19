# Acceptance Matrix

The binding contract is `IMPLEMENTATION_CONTRACT.md`.

| Area | Required evidence | Completion level |
|---|---|---|
| Contract consistency | Reviewed documents aligned with the simple IT workflow and no stale superseded requirement | Documentation |
| Solution structure | Root solution inventory, deterministic restore, Windows CI | Source |
| Authentication | Unit tests, tenant validation, authorized-account allowlist, token-cache protection, real Windows sign-in | Source + Production |
| OneDrive validation | URL and drive tests, real tenant validation | Source + Production |
| Package items | OneNote and other package items classified as `Unsupported`, reported, and never silently claimed as copied | Source + Production |
| Delta inventory | Page processing, checkpoint persistence, recovery tests | Source |
| Local destination | Local-path rejection tests, source binding, write checks, disk headroom, and disk-full behavior | Source + Windows |
| Destination locking | Cross-process and cross-session Windows test | Production |
| Path safety | Exact `PathMappingVersion = 1` mapping, containment, reparse-point, traversal, hard-link, and unrelated-file tests | Source + Windows |
| SQLite state | Transactions, integrity check, migration backup, crash recovery, schema-version rejection, and rerun lookup | Source |
| Transfer and resume | Streaming, Range, restart, partial, retry, throttle, and safe-cancellation tests | Source |
| Credential isolation | No Graph credentials sent to temporary download hosts | Source |
| Integrity | Source-hash tests, local SHA-256, corruption detection, and existing-file revalidation | Source |
| Timestamp preservation | File and folder source timestamp preservation plus explicit warning behavior | Source + Production |
| Run-state rules | Exact `Completed`, `CompletedWithWarnings`, `Failed`, `Cancelled`, and `Interrupted` outcomes | Source |
| Reconciliation | Graph delta changes, maximum three passes, stable and warning outcomes | Source + Production |
| UI | View-model tests, WPF startup, authorized-account confirmation, unsupported count, and operational review | Production |
| Reports | Unique per-run directory, UTF-8, escaping, formula protection, unsupported items, warnings, and terminal state | Source |
| Supply chain | Dependency lock strategy, vulnerability scan, secret scan, SBOM where available | Release |
| Publish | Self-contained `win-x64` tied to exact source commit | Production |
| Windows Server | Published application executes on Windows Server 2019 | Production |
| Access removal | Temporary Site Collection Administrator access removed, verified, and externally recorded | Production |
| Storage protection | Restricted NTFS access and BitLocker, approved equivalent, or approved exception | Production |

## Documentation Ready

Requires:

- one binding contract;
- simple workflow clearly defined;
- no unresolved binding contradictions;
- no active control that still requires a superseded custom index or five-million-item benchmark;
- package-item, disk-space, timestamp, run-state, reporting, path-mapping, account-authorization, and SQLite-recovery policies defined;
- corrected documentation reviewed and merged; and
- valid documentation evidence tied to an exact validated commit.

## Source Implementation Complete

Requires:

- complete application source;
- Windows CI restore, Release build, and automated tests;
- implemented Graph delta inventory;
- implemented local SQLite state, integrity checks, and recovery;
- implemented transfer, resume, integrity, timestamp preservation, destination binding, UI, and reports;
- deterministic `PathMappingVersion = 1` behavior;
- no placeholder or fake production behavior; and
- committed evidence for each completed source milestone.

This status must not be represented as Production Ready.

## Production Ready

Requires actual evidence for:

- Windows Release build and automated tests;
- WPF startup;
- Microsoft interactive sign-in using an authorized transfer account;
- real employee OneDrive validation;
- complete copy to local storage;
- correct reporting of package items when present;
- interruption and resume;
- source-change reconciliation;
- destination locking across processes or sessions;
- disk-space and disk-full behavior;
- timestamp preservation;
- destination containment and NTFS access review;
- temporary administrative-access removal and verification;
- self-contained `win-x64` publish;
- execution on Windows Server 2019; and
- release traceability and documented signing status.

## Evidence rules

Every completed milestone evidence summary must contain:

- evidence schema version;
- milestone;
- exact validated source commit;
- UTC execution time;
- environment and operating system;
- command or action executed;
- result and exit code;
- relevant test counts or thresholds;
- raw artifact or CI location when applicable;
- limitations and unexecuted checks; and
- redaction confirmation.

A mutable branch name alone is not evidence. An unexecuted command, checked box, ignored file, or verbal claim is not evidence.
