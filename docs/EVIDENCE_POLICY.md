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

For milestones affected by Microsoft platform behavior, summaries must additionally contain where relevant:

- official Microsoft documentation review UTC date;
- exact official documentation URLs reviewed;
- exact MSAL.NET version;
- exact Microsoft Graph SDK version;
- exact .NET SDK version;
- exact bundled self-contained runtime version;
- exact Windows edition and build;
- approved Graph API version;
- approved delegated scope set;
- redacted endpoint templates actually exercised;
- authentication broker and fallback path tested;
- retry owner and observed attempt count;
- correlation behavior;
- delta reset behavior;
- temporary-host credential-isolation result;
- source-hash and local-hash behavior; and
- platform support and lifecycle status.

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

Microsoft platform documentation evidence must additionally identify:

- official Microsoft sources reviewed;
- review date;
- platform decisions added or changed;
- product-scope impact;
- prohibited flows and permissions confirmed absent;
- current facts that must be revalidated later rather than frozen as permanent assumptions; and
- every Microsoft behavior that remains unexecuted in the real environment.

Documentation evidence must not claim source or production validation.

A platform review based only on model memory, copied old text, a search-result summary, or an unexecuted SDK sample is not current Microsoft platform evidence.

## Completion rule

A milestone cannot be marked complete solely from:

- a checked pull-request checkbox;
- a verbal statement;
- an uncommitted local log;
- a command that was not executed;
- a file path that does not exist in a fresh clone;
- a mutable branch name;
- a dependency version that was not restored and inspected;
- a Microsoft permission that was not verified in the deployed app registration;
- a Microsoft endpoint that was not actually exercised when production evidence is required;
- a current-platform claim based only on model memory; or
- evidence that contradicts the current phase, handoff, project memory, binding contract, or Microsoft platform controls.

A completed milestone requires a committed durable evidence summary and any additional raw artifact required by its acceptance criteria.

## Release evidence

Before internal release, generated or committed evidence must prove:

- deterministic restore;
- Windows Release build and automated tests;
- dependency-vulnerability and secret scans;
- Graph beta and prohibited-authentication-flow checks;
- SBOM generation where supported;
- exact source commit;
- exact published artifact hash;
- exact .NET SDK and bundled runtime patch;
- target Windows support status;
- Authenticode result or approved unsigned limitation; and
- upgrade and rollback instructions.

A self-contained publish from an older runtime patch cannot be represented as updated merely because a newer .NET runtime is installed on the server.

## Security

Never place these values in evidence:

- passwords;
- access or refresh tokens;
- cookies;
- authorization headers;
- temporary Microsoft download URLs;
- client secrets or private keys;
- raw Graph responses;
- employee file contents;
- production state databases; or
- production reports containing employee paths or identities.

Use stable redacted identifiers or hashes when an identity is required for traceability.
