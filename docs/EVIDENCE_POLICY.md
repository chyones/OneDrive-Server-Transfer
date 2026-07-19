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
M00_<documentation-scope>_<UTC timestamp>.json
M01_solution-foundation_<UTC timestamp>.json
M02_authentication_<UTC timestamp>.json
M03_onedrive-resolution_<UTC timestamp>.json
```

Each summary must contain:

- evidence schema version;
- milestone;
- completion state being justified;
- exact immutable validated source commit;
- UTC execution time;
- environment and operating system;
- exact command or validated action;
- exit code and result;
- test or validation counts where applicable;
- required thresholds where the binding contract defines one;
- raw artifact or CI location where applicable;
- limitations and unexecuted checks; and
- redaction confirmation.

A mutable branch name, pull-request number, or current `main` reference may be included as context but cannot replace the exact validated commit.

Committed evidence must be reproducible, reviewable, and free from secrets or employee data.

### Generated local or CI artifacts

Store generated outputs under:

```text
artifacts/source
artifacts/win-x64
```

These directories may contain raw logs, TRX files, coverage output, binaries, publish directories, security-scan results, SBOM files, or packaging output. Their generated contents remain ignored by Git unless explicitly approved.

The superseded custom five-million-item benchmark and custom disk-index engine are not required evidence for the first release.

GitHub Actions should upload relevant raw outputs as workflow artifacts when workflows are implemented.

## Documentation evidence

Documentation-only evidence must identify:

- the exact reviewed commit containing the aligned binding contract and controls;
- the documents reviewed;
- the contradiction and stale-control checks performed;
- whether application code was present;
- which Windows, WPF, tenant, sign-in, transfer, resume, publish, and production checks were not executed; and
- the absence of secrets and employee data.

Documentation evidence must not claim source or production validation.

## Completion rule

A milestone cannot be marked complete solely from:

- a checked pull-request checkbox;
- a verbal statement;
- an uncommitted local log;
- a command that was not executed;
- a file path that does not exist in a fresh clone;
- a mutable branch name; or
- evidence that contradicts the current phase, handoff, project memory, or binding contract.

A completed milestone requires a committed durable evidence summary and any additional raw artifact required by its acceptance criteria.

## Security

Never place these values in evidence:

- passwords;
- access or refresh tokens;
- cookies;
- authorization headers;
- temporary Microsoft download URLs;
- client secrets or private keys;
- employee file contents;
- production state databases; or
- production reports containing employee paths or identities.

Use stable redacted identifiers or hashes when an identity is required for traceability.
