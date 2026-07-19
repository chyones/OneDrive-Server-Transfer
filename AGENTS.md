# AGENTS.md

These instructions apply to every implementation agent working in this repository.

## Authority order

When instructions conflict, use this order:

1. Explicit current instruction from the repository owner.
2. `IMPLEMENTATION_CONTRACT.md`.
3. Approved non-conflicting decisions in `.ai/DECISION_LOG.md`.
4. This file.
5. `.ai/PHASE_STATUS.md`, `.ai/HANDOFF.md`, and `docs/IMPLEMENTATION_PLAN.md`.
6. Other repository documentation.

`IMPLEMENTATION_CONTRACT_AMENDMENTS.md` is superseded and retained only for historical traceability.

The Microsoft platform documents listed below are mandatory non-conflicting implementation controls. They operationalize the binding contract and do not authorize product-scope expansion:

- `docs/MICROSOFT_PLATFORM_BASELINE.md`
- `docs/AUTHENTICATION_AND_TOKEN_POLICY.md`
- `docs/GRAPH_ENDPOINT_PERMISSION_MATRIX.md`
- `docs/GRAPH_DELTA_AND_RECONCILIATION_POLICY.md`
- `docs/GRAPH_RESILIENCY_POLICY.md`
- `docs/DOWNLOAD_AND_INTEGRITY_POLICY.md`
- `docs/PATCHING_AND_RELEASE_LIFECYCLE.md`

## Current repository state

- M0 contract simplification and pre-implementation hardening is `DOCUMENTATION_COMPLETE`.
- Current committed M0 evidence: `artifacts/evidence/M00_microsoft-platform-baseline_20260719T172157Z.json`.
- Validated documentation source commit: `50e25cc9501ef22ad05ebe6abc1e7a96603efce2`.
- Prior workflow-alignment evidence is retained for historical traceability.
- Application implementation has not started.
- Current phase: `M1 — Solution and CI foundation`.
- M1 is authorized to start and is currently `NOT_STARTED`.
- Mark M1 `IN_PROGRESS` before creating or changing source files.
- Do not claim source code, Windows build, WPF execution, Microsoft sign-in, employee source resolution, dry run, OneDrive copy, resume, publish, or production validation without executed evidence.

## Product boundary

Implement one simple internal WPF archival-copy application used by an authorized IT administrator to:

1. sign in with Microsoft using the authorized IT transfer account;
2. enter one employee UPN or paste one employee OneDrive for Business root URL;
3. select a local destination on the same Windows Server;
4. press `Scan` for the mandatory dry run;
5. review the resolved employee, operator, destination, counts, known size, unsupported items, path warnings, and storage warnings;
6. press `Start Copy` only after the scan succeeds; and
7. monitor progress and review the result and reports.

Do not add dashboards, multiple pages, scheduling, batch employee import, user management, remote destinations, service mode, central reporting, or other unapproved features.

The application copies and archives data only. It never deletes or modifies Microsoft 365 source content.

## Employee credentials are forbidden

- Never request, collect, store, log, transmit, or process an employee password.
- Never authenticate as the employee.
- Never add an employee-password UI control, model, configuration key, service input, test fixture, or logging field.
- The employee is identified only by UPN or OneDrive root URL.
- The signed-in authorized IT operator remains the authenticated actor.

## Microsoft platform controls

- Use Microsoft Graph `v1.0` only. Do not use Graph beta endpoints, beta SDK models, or preview-only behavior.
- Use delegated interactive MSAL authentication for a single-tenant public-client desktop application.
- Prefer WAM and support the MSAL system-browser fallback path.
- Never add ROPC, device-code flow, client credentials, client secrets, certificates, managed identity, workload identity, application permissions, or Microsoft 365 write permissions.
- Implement only endpoints and permissions approved in `docs/GRAPH_ENDPOINT_PERMISSION_MATRIX.md`.
- Do not add directory-wide permissions for convenience.
- Preserve Graph next links and delta links as opaque values.
- Handle supported delta `410 Gone` reset through fresh enumeration and reconciliation, not database reset.
- Configure exactly one automatic retry owner per HTTP request category.
- Respect `Retry-After`; do not stack SDK and application retries unknowingly.
- Use a separate unauthenticated HTTP client for temporary download hosts.
- Never send Graph bearer tokens, cookies, or Graph middleware headers to a temporary download URL.
- Apply Range requests to the actual temporary URL and validate `206 Partial Content` and `Content-Range`.
- Ignore Microsoft Graph `sha256Hash`; use supported Microsoft source hashes when available and keep local SHA-256 separate.
- Tolerate unknown JSON properties and enum values safely; unknown content semantics must not be guessed as copied content.
- Generate request correlation IDs and keep protected diagnostics free of secrets and raw Graph responses.
- Keep self-contained releases current on supported .NET servicing patches and record the bundled runtime version.
- Recheck the current official Microsoft documentation listed in `docs/MICROSOFT_PLATFORM_BASELINE.md` before completing affected milestones.

## Required implementation behavior

- Use C#, .NET 10 LTS, WPF, MVVM, Microsoft Graph v1.0, MSAL, dependency injection, local SQLite state, and automated tests.
- Keep Microsoft 365 access strictly read-only.
- Validate the configured tenant and authorized transfer-account object-ID allowlist when configured.
- Accept one employee UPN or OneDrive root URL and resolve the final source to Tenant ID, employee Entra object ID, and source Drive ID.
- Require a successful mandatory scan before enabling `Start Copy`.
- Invalidate the scan whenever source or destination input changes.
- Use Graph drive delta for dry-run inventory and reconciliation.
- Keep file and metadata processing bounded in memory.
- Classify OneNote and other Graph package items as `Unsupported`; report them and mark the archive `Incomplete`.
- Use a fixed maximum of three simultaneous downloads.
- Use `.partial` files, safe Range resume, source metadata revalidation, supported source hashes, and local SHA-256.
- Respect `Retry-After` and use bounded retry.
- Preserve supported source timestamps and report failures as non-content warnings.
- Bind every destination to one tenant, employee Entra object ID, and source drive.
- Record operator identity for audit without permanently binding the archive to one operator.
- Permit another authorized operator to resume only after all binding, state, and authorization checks succeed.
- Reject UNC, mapped, NAS, SMB, remote, Windows system, and application installation destinations.
- Require known remaining bytes plus the fixed 5 GiB destination reserve and fail safely on disk-full.
- Implement the exact binding `PathMappingVersion = 1` rules, including deterministic suffix expansion, and persist mappings.
- Prevent traversal, unsafe reparse-point redirection, hard-link overwrite, and writes outside the selected destination.
- Validate SQLite integrity before resume, back up before migration, migrate transactionally, and never silently reset corrupt state.
- Keep SQLite as the operational source for scan, resume, recovery, and source binding.
- Keep CSV and JSON as audit reports only.
- Use the approved item states and run states exactly, including `Incomplete`.
- Store every run's reports under `_TransferReport/Runs/<RunId>` without overwriting earlier reports.
- Implement `docs/REPORT_SCHEMA.md`.
- Keep technical details in protected logs and show simple reference-coded errors in the UI.
- Require removal and verification of temporary Site Collection Administrator access after it is no longer required.
- Require BitLocker, approved equivalent, or documented approved exception for production storage.

## Result honesty

- `Completed` means the supported archive is complete with no warning.
- `CompletedWithWarnings` means every supported item was copied or validly skipped and only non-content warnings remain.
- `Incomplete` means supported content failed, unsupported content exists, or the source did not stabilize.
- Never report `Incomplete` as a successful complete archive.

## Milestone discipline

Work phase by phase according to `docs/IMPLEMENTATION_PLAN.md`.

At the start of a phase:

1. Set the phase to `IN_PROGRESS`.
2. Record exact scope in `.ai/HANDOFF.md`.
3. Confirm the previous phase has valid committed evidence.
4. Revalidate the Microsoft platform documents relevant to the phase.

At the end of a phase:

1. Execute every required validation.
2. Commit a redacted evidence summary under `artifacts/evidence`.
3. Record the exact validated source commit.
4. Update phase status, decision log, project memory, and handoff.
5. Review for false claims, placeholders, secrets, stale superseded controls, out-of-scope work, beta APIs, unapproved permissions, duplicate retry layers, and outdated Microsoft platform assumptions.

Windows CI restore, Release build, and automated tests are mandatory before `Source Implementation Complete`.

## Evidence honesty

- Cross-platform restore is not a Windows build.
- Mock sign-in is not real Microsoft interactive sign-in.
- A synthetic scan is not a real tenant dry run.
- Synthetic copy tests are not a real employee OneDrive archive operation.
- A publish command is not a successful publish.
- Source completion is not production readiness.
- An unexecuted check is not passed.
- A mutable branch name is not immutable evidence.
- Unsupported package content is not copied content.
- A missing supported item cannot be hidden as a warning.
- A platform-document review that was not executed against current official Microsoft documentation is not current validation.

## Security

Never commit or print passwords, tokens, client secrets, authorization headers, cookies, temporary download URLs, private keys, employee content, production state databases, or unredacted production logs and reports.

## Final implementation report

Report only the justified completion label, phase status, files changed, evidence paths and validated commits, restore/build/test/Windows/sign-in/source-resolution/scan/copy/resume/publish results, required configuration, exact approved Graph scopes and endpoint templates, Microsoft platform versions, unsupported items, incomplete states, warning states, and genuine blockers.
