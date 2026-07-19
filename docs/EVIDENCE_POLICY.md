# Validation Evidence Policy

## Purpose

This policy defines which validation evidence must be retained in Git and which generated artifacts remain local or in GitHub Actions.

## Evidence classes

### Durable committed summaries

Store small redacted summaries under:

```text
artifacts/evidence
```

Required naming pattern:

```text
M01_solution-foundation_<UTC timestamp>.json
M02_authentication_<UTC timestamp>.json
M03_onedrive-resolution_<UTC timestamp>.json
```

Each summary must contain:

- schema version
- milestone
- source commit
- UTC execution time
- environment and operating system
- exact command or validated action
- exit code and result
- test or validation counts
- threshold results where applicable
- raw artifact location
- limitations and unexecuted checks
- redaction confirmation

Committed evidence must be reproducible, reviewable, and free from secrets or employee data.

### Generated local or CI artifacts

Store generated outputs under:

```text
artifacts/source
artifacts/win-x64
```

These directories may contain raw logs, TRX files, coverage output, benchmark working data, binaries, publish directories, or packaging output. Their generated contents remain ignored by Git unless explicitly approved.

GitHub Actions should upload relevant raw outputs as workflow artifacts when workflows are implemented.

## Completion rule

A milestone cannot be marked complete solely from:

- a checked pull-request checkbox
- a verbal statement
- an uncommitted local log
- a command that was not executed
- a file path that does not exist in a fresh clone

A completed milestone requires a committed durable evidence summary and any additional raw artifact required by its acceptance criteria.

## Security

Never place these values in evidence:

- passwords
- access or refresh tokens
- cookies
- authorization headers
- temporary Microsoft download URLs
- client secrets or private keys
- employee file contents
- production reports containing employee paths or identities

Use stable redacted identifiers or hashes when an identity is required for traceability.
