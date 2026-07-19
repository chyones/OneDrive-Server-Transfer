# Acceptance Matrix

The binding contract is `IMPLEMENTATION_CONTRACT.md`.

The Microsoft platform documents referenced by `AGENTS.md` are mandatory non-conflicting implementation controls and must be validated against current official Microsoft documentation before affected milestones are completed.

| Area | Required evidence | Completion level |
|---|---|---|
| Contract consistency | Reviewed documents aligned with the simple IT archival-copy workflow and no stale superseded requirement | Documentation |
| Microsoft platform baseline | Current official Microsoft references reviewed; Graph v1.0, delegated-only version 1, WAM preference, permission matrix, delta reset, retry ownership, temporary URL isolation, source-hash rules, and servicing lifecycle documented | Documentation |
| Solution structure | Root solution inventory, deterministic restore, Windows CI | Source |
| Authentication | Unit tests, tenant validation, authorized-account allowlist, token-cache protection, no employee-password collection, real Windows sign-in | Source + Production |
| Authentication broker | WAM configuration, supported system-browser fallback, MFA and Conditional Access behavior, and no ROPC/device-code/client-credential path | Source + Production |
| Graph permissions | Exact endpoint inventory mapped to approved delegated scopes; no write scope, application permission, beta endpoint, or unapproved directory permission | Source + Production |
| Employee source input | Employee UPN and OneDrive-root URL tests, durable employee and drive identity, real-tenant validation | Source + Production |
| Source rejection | Unknown user, unprovisioned OneDrive, file, subfolder, shared source, consumer OneDrive, SharePoint, Teams, and external-tenant rejection | Source + Production |
| Mandatory dry run | Complete delta inventory, file and folder counts, known bytes, unsupported items, path warnings, destination checks, storage checks, scan invalidation after input change | Source + Production |
| Package items | OneNote and other package items classified as `Unsupported`, reported, and never silently claimed as copied | Source + Production |
| Delta inventory | Page processing, opaque next/delta links, checkpoint persistence, recovery, duplicate-item handling, source rename, move, deletion, and continued-change tests | Source |
| Delta reset | Supported `410 Gone` fresh enumeration and reconciliation without SQLite reset or local archive deletion | Source + Production |
| Graph compatibility | `$select` discipline, unknown JSON properties and enum values handled safely, no undocumented property-order dependency, Graph v1.0 only | Source |
| Graph correlation | Unique client request IDs plus Microsoft request IDs and response dates in protected logs, with no tokens or raw responses | Source |
| Graph resilience | Single automatic retry owner, `Retry-After`, bounded exponential backoff with jitter, `401`/`403` classification, `503`, cancellation during delay, and persisted attempt budget | Source |
| Local destination | Local-path rejection tests, system-folder rejection, source binding, write checks, disk headroom, and disk-full behavior | Source + Windows |
| Authorized resume | Another authorized operator can resume only when tenant, employee, drive, destination, state, and authorization checks match | Source + Production |
| Destination locking | Cross-process and cross-session Windows test | Production |
| Path safety | Exact `PathMappingVersion = 1` mapping, deterministic suffix expansion, containment, reparse-point, traversal, hard-link, and unrelated-file tests | Source + Windows |
| SQLite state | Scan state, transactions, integrity check, migration backup, crash recovery, schema-version rejection, and rerun lookup | Source |
| Transfer and resume | Streaming, Range, restart, partial, retry, `Retry-After`, throttle, and safe-cancellation tests | Source |
| Temporary download URLs | Short-lived URL not persisted or logged, separate unauthenticated client, no Graph credentials, fresh-URL recovery, Range applied to actual temporary URL | Source + Production |
| Range correctness | Valid `206` and `Content-Range`, ignored Range returning `200` restarts from zero, invalid or `416` behavior is safe | Source |
| Credential isolation | No Graph credentials sent to temporary download hosts and no employee credentials requested or processed | Source |
| Integrity | Supported Microsoft source-hash tests, Microsoft `sha256Hash` ignored, local SHA-256 kept separate, corruption detection, and existing-file revalidation | Source |
| Timestamp preservation | File and folder source timestamp preservation plus explicit non-content warning behavior | Source + Production |
| Run-state rules | Exact `Completed`, `CompletedWithWarnings`, `Incomplete`, `Failed`, `Cancelled`, and `Interrupted` outcomes | Source |
| Archive completeness | Supported failure, unsupported content, or unstable source must produce `Incomplete`; clean completion cannot hide missing content | Source + Production |
| Reconciliation | Graph delta changes, maximum three passes, stable and incomplete outcomes | Source + Production |
| UI | View-model tests, WPF startup, signed-in operator, UPN-or-URL input, mandatory scan, disabled stale copy, confirmation, unsupported count, and operational review | Production |
| Reports | Unique per-run directory, `docs/REPORT_SCHEMA.md`, UTF-8, escaping, formula protection, unsupported items, warnings, and terminal state | Source |
| Report authority | SQLite remains operational source; CSV and JSON are audit outputs only | Source |
| Supply chain | Dependency lock strategy, vulnerability scan, secret scan, SBOM where available | Release |
| Runtime servicing | Exact .NET SDK and bundled self-contained runtime patch recorded; currently supported .NET 10 servicing patch used | Release |
| SDK servicing | MSAL and Graph SDK release notes reviewed; authentication, retry, serialization, redirects, correlation, and redaction regression tests executed | Release |
| Publish | Self-contained `win-x64` tied to exact source commit and artifact hash | Production |
| Windows Server | Published application executes on Windows Server 2019 | Production |
| Platform support | Target Windows and bundled .NET runtime remain in support; no Production Ready label after end of support | Release |
| Secondary compatibility | Windows Server 2022 or 2025 compatibility check when an environment is available; does not replace Server 2019 acceptance | Release |
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
- Microsoft platform baseline, authentication, endpoint-permission, delta, resilience, download-integrity, and patch-lifecycle policies committed and referenced by agent instructions;
- Microsoft Graph beta, application permissions, write scopes, ROPC, device-code flow, client secrets, and duplicate retry ownership explicitly prohibited for version 1;
- report schema committed;
- corrected documentation reviewed and merged; and
- valid documentation evidence tied to an exact validated commit.

## Source Implementation Complete

Requires:

- complete application source;
- Windows CI restore, Release build, and automated tests;
- implemented IT-operator authentication with no employee-password path;
- implemented WAM-preferred interactive sign-in and system-browser fallback boundary;
- implemented exact approved Graph endpoints and delegated permissions only;
- implemented employee UPN and OneDrive-root URL resolution;
- implemented mandatory dry run;
- implemented Graph delta inventory, opaque paging links, and supported 410 reset recovery;
- implemented one retry owner, throttling, correlation, and temporary-host credential isolation;
- implemented local SQLite state, integrity checks, and recovery;
- implemented copy, resume, supported source hashes, separate local SHA-256, timestamp preservation, destination binding, UI, and reports;
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
- WAM behavior and controlled browser fallback where testable;
- absence of employee-password collection;
- no application permission, write scope, or beta endpoint in the deployed app registration and request inventory;
- real employee UPN and OneDrive-root URL validation;
- complete mandatory dry run;
- complete copy to local storage;
- correct `Incomplete` reporting for package items, failed content, unknown content semantics, and unstable source;
- interruption and resume;
- delta `410` reset recovery;
- source-change reconciliation;
- authorized-operator resume;
- destination locking across processes or sessions;
- throttling and retry behavior;
- temporary-download URL expiration and credential isolation;
- Range `206` and ignored-Range `200` behavior;
- supported Microsoft source-hash and local SHA-256 behavior;
- disk-space and disk-full behavior;
- timestamp preservation;
- destination containment and NTFS access review;
- temporary administrative-access removal and verification;
- self-contained `win-x64` publish using a supported .NET 10 servicing patch;
- execution on Windows Server 2019; and
- release traceability, artifact hash, SBOM status, platform-support status, and documented signing status.

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
- exact MSAL, Graph SDK, .NET SDK, and bundled runtime versions when relevant;
- exact approved Graph scope set and redacted endpoint templates when relevant;
- Microsoft platform documentation review date when relevant;
- limitations and unexecuted checks; and
- redaction confirmation.

A mutable branch name alone is not evidence. An unexecuted command, checked box, ignored file, verbal claim, or platform review based only on model memory is not evidence.
