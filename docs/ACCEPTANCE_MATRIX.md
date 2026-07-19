# Acceptance Matrix

The binding contract is `IMPLEMENTATION_CONTRACT.md`.

| Area | Required evidence | Completion level |
|---|---|---|
| Contract consistency | Reviewed documents aligned with the simple IT archival-copy workflow and no stale superseded requirement | Documentation |
| Solution structure | Root solution inventory, deterministic restore, Windows CI | Source |
| Authentication | Unit tests, tenant validation, authorized-account allowlist, token-cache protection, no employee-password collection, real Windows sign-in | Source + Production |
| Employee source input | Employee UPN and OneDrive-root URL tests, durable employee and drive identity, real-tenant validation | Source + Production |
| Source rejection | Unknown user, unprovisioned OneDrive, file, subfolder, shared source, consumer OneDrive, SharePoint, Teams, and external-tenant rejection | Source + Production |
| Mandatory dry run | Complete delta inventory, file and folder counts, known bytes, unsupported items, path warnings, destination checks, storage checks, scan invalidation after input change | Source + Production |
| Package items | OneNote and other package items classified as `Unsupported`, reported, and never silently claimed as copied | Source + Production |
| Delta inventory | Page processing, checkpoint persistence, recovery, source rename, move, deletion, and continued-change tests | Source |
| Local destination | Local-path rejection tests, system-folder rejection, source binding, write checks, disk headroom, and disk-full behavior | Source + Windows |
| Authorized resume | Another authorized operator can resume only when tenant, employee, drive, destination, state, and authorization checks match | Source + Production |
| Destination locking | Cross-process and cross-session Windows test | Production |
| Path safety | Exact `PathMappingVersion = 1` mapping, deterministic suffix expansion, containment, reparse-point, traversal, hard-link, and unrelated-file tests | Source + Windows |
| SQLite state | Scan state, transactions, integrity check, migration backup, crash recovery, schema-version rejection, and rerun lookup | Source |
| Transfer and resume | Streaming, Range, restart, partial, retry, `Retry-After`, throttle, and safe-cancellation tests | Source |
| Credential isolation | No Graph credentials sent to temporary download hosts and no employee credentials requested or processed | Source |
| Integrity | Source-hash tests, local SHA-256, corruption detection, and existing-file revalidation | Source |
| Timestamp preservation | File and folder source timestamp preservation plus explicit non-content warning behavior | Source + Production |
| Run-state rules | Exact `Completed`, `CompletedWithWarnings`, `Incomplete`, `Failed`, `Cancelled`, and `Interrupted` outcomes | Source |
| Archive completeness | Supported failure, unsupported content, or unstable source must produce `Incomplete`; clean completion cannot hide missing content | Source + Production |
| Reconciliation | Graph delta changes, maximum three passes, stable and incomplete outcomes | Source + Production |
| UI | View-model tests, WPF startup, signed-in operator, UPN-or-URL input, mandatory scan, disabled stale copy, confirmation, unsupported count, and operational review | Production |
| Reports | Unique per-run directory, `docs/REPORT_SCHEMA.md`, UTF-8, escaping, formula protection, unsupported items, warnings, and terminal state | Source |
| Report authority | SQLite remains operational source; CSV and JSON are audit outputs only | Source |
| Supply chain | Dependency lock strategy, vulnerability scan, secret scan, SBOM where available | Release |
| Publish | Self-contained `win-x64` tied to exact source commit | Production |
| Windows Server | Published application executes on Windows Server 2019 | Production |
| Access removal | Temporary Site Collection Administrator access removed, verified, and externally recorded | Production |
| Storage protection | Restricted NTFS access and BitLocker, approved equivalent, or approved exception | Production |

## Documentation Ready

Requires:

- one binding contract;
- internal archival-copy purpose clearly defined;
- simple UPN-or-URL workflow clearly defined;
- employee-password collection and employee impersonation explicitly prohibited;
- mandatory dry run defined;
- no unresolved binding contradictions;
- no active control that still requires a superseded custom index or five-million-item benchmark;
- package-item, incomplete-result, disk-space, timestamp, run-state, reporting, path-mapping, account-authorization, and SQLite-recovery policies defined;
- report schema committed;
- corrected documentation reviewed and merged; and
- valid documentation evidence tied to an exact validated commit.

## Source Implementation Complete

Requires:

- complete application source;
- Windows CI restore, Release build, and automated tests;
- implemented IT-operator authentication with no employee-password path;
- implemented employee UPN and OneDrive-root URL resolution;
- implemented mandatory dry run;
- implemented Graph delta inventory;
- implemented local SQLite state, integrity checks, and recovery;
- implemented copy, resume, integrity, timestamp preservation, destination binding, UI, and reports;
- implemented exact `Incomplete` behavior;
- implemented `docs/REPORT_SCHEMA.md`;
- deterministic `PathMappingVersion = 1` behavior with suffix expansion;
- no placeholder or fake production behavior; and
- committed evidence for each completed source milestone.

This status must not be represented as Production Ready.

## Production Ready

Requires actual evidence for:

- Windows Release build and automated tests;
- WPF startup;
- Microsoft interactive sign-in using an authorized transfer account;
- absence of employee-password collection;
- real employee UPN and OneDrive-root URL validation;
- complete mandatory dry run;
- complete copy to local storage;
- correct `Incomplete` reporting for package items, failed content, and unstable source;
- interruption and resume;
- source-change reconciliation;
- authorized-operator resume;
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
