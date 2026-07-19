# Transfer Report Schema

## Purpose

This document defines the human-readable audit-report format for OneDrive Server Transfer.

SQLite at `_TransferReport/TransferState.db` is the operational source of truth for scan, resume, recovery, source binding, and item state. CSV and JSON reports are generated for authorized IT review and audit only. They must never be used as the resume database.

## Run report directory

Every copy run creates a unique directory:

```text
SelectedDestination\_TransferReport\Runs\<RunId>\
├── TransferSummary.json
├── TransferReport.csv
├── FailedFiles.csv
└── TransferLog.log
```

A later run must never overwrite or append to another run's files.

## `TransferReport.csv`

Required header order for schema version 1:

```csv
ReportSchemaVersion,RunId,OperatorUPN,EmployeeUPN,SourceDriveId,SourceItemId,SourcePath,LocalPath,ItemType,SizeBytes,Status,AttemptCount,SourceHashType,SourceHashValue,LocalSha256,TimestampStatus,ErrorCode,ErrorMessage,StartedAtUtc,CompletedAtUtc
```

### Field definitions

| Field | Requirement |
|---|---|
| `ReportSchemaVersion` | Integer. Initial value: `1`. |
| `RunId` | Stable run identifier used by state, logs, and reports. |
| `OperatorUPN` | Normalized signed-in authorized IT operator UPN. |
| `EmployeeUPN` | Normalized employee UPN resolved during scan. Display/audit value only; not the sole durable source identity. |
| `SourceDriveId` | Source Microsoft Graph drive identifier. Protected report field. |
| `SourceItemId` | Source Microsoft Graph Drive Item identifier. Protected report field. |
| `SourcePath` | Source-relative path. Must be CSV escaped and formula-injection protected. |
| `LocalPath` | Destination-relative path under `OneDriveData`. Do not expose an unnecessary full server path. |
| `ItemType` | `File`, `Folder`, `Package`, or another explicitly documented classification. |
| `SizeBytes` | Source size for file items. Empty for folders when not applicable. |
| `Status` | One approved item state. |
| `AttemptCount` | Number of download attempts used for the item. |
| `SourceHashType` | Microsoft-provided comparable source-hash type when available. |
| `SourceHashValue` | Microsoft-provided source-hash value when available. Never substitute size or metadata. |
| `LocalSha256` | Streaming SHA-256 of the verified local file. |
| `TimestampStatus` | Timestamp preservation result or `NotApplicable`. |
| `ErrorCode` | Stable application reference code. |
| `ErrorMessage` | Sanitized plain-language message. No raw Graph response or secret data. |
| `StartedAtUtc` | ISO 8601 UTC timestamp. |
| `CompletedAtUtc` | ISO 8601 UTC timestamp when the item reached a terminal state. |

## Approved item states

```text
Discovered
Mapped
Downloading
Verified
Completed
Skipped
Unsupported
Failed
Cancelled
```

Reports may show `Retrying` as a transient activity message, but `Retrying` is not a persisted terminal item state. Attempt progress remains represented by the current state and `AttemptCount`.

## Approved run states

```text
InProgress
Completed
CompletedWithWarnings
Incomplete
Failed
Cancelled
Interrupted
```

- `Completed` means the supported archive is complete and no warning remains.
- `CompletedWithWarnings` means every supported item was copied or validly skipped and only non-content warnings remain.
- `Incomplete` means supported content failed, unsupported content exists, or the source did not reach a stable reconciled state.
- `Failed` means a fatal validation, storage, state, binding, or security condition prevented safe continuation.

## `FailedFiles.csv`

`FailedFiles.csv` contains rows from `TransferReport.csv` whose status is `Failed` or `Unsupported`. It uses the same schema and column order so automated review does not require a second parser.

## `TransferSummary.json`

The summary must include at minimum:

```text
ReportSchemaVersion
RunId
OperatorUPN
EmployeeUPN
SourceInputMode
SourceDriveId
DestinationDisplayPath
ScanCompletedAtUtc
CopyStartedAtUtc
CopyCompletedAtUtc
DiscoveredCount
FileCount
FolderCount
UnsupportedCount
CompletedCount
SkippedCount
FailedCount
KnownSourceBytes
DownloadedBytes
PathWarningCount
TimestampWarningCount
StorageWarningCount
ReconciliationPasses
FinalRunState
IsArchiveComplete
```

`IsArchiveComplete` is `true` only when `FinalRunState` is `Completed` or `CompletedWithWarnings`. It is always `false` for `Incomplete`, `Failed`, `Cancelled`, `Interrupted`, or `InProgress`.

## Dry-run summary

The mandatory scan must produce the values needed for the UI preflight summary:

- resolved employee;
- signed-in operator;
- source input mode;
- source drive identity;
- file and folder counts;
- known total bytes;
- unsupported count;
- path warnings;
- destination and storage warnings; and
- whether `Start Copy` may be enabled.

The dry-run result must never claim that files were copied.

## CSV safety

- Encode CSV as UTF-8.
- Escape commas, quotes, CR, and LF correctly.
- Protect untrusted values against spreadsheet formula injection.
- Prefix or otherwise neutralize values beginning with `=`, `+`, `-`, `@`, tab, or carriage return according to the approved CSV writer policy.
- Do not write passwords, tokens, cookies, authorization headers, temporary download URLs, raw Graph responses, or employee file content into reports.

## Error redaction

`ErrorMessage` and logs must not expose:

- access or refresh tokens;
- authorization headers;
- cookies;
- temporary download URLs;
- employee passwords;
- raw Graph response bodies;
- stack traces in normal reports;
- client secrets or private keys; or
- protected configuration values not required for remediation.

Use stable reference codes and keep detailed technical diagnostics only in the protected technical log.
