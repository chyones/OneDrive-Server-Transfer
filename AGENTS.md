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

## Current repository state

- M0 contract simplification and pre-implementation hardening is `DOCUMENTATION_COMPLETE`.
- Completion label: `Documentation Ready`.
- M0 evidence: `artifacts/evidence/M00_preimplementation-hardening_20260719T113850Z.json`.
- Validated documentation baseline: `e9434ff54c373e1d0129ba2583027897f6f3ff25`.
- Application implementation has not started.
- Next phase: `M1 — Solution and CI foundation`.
- Do not claim source code, Windows build, WPF execution, Microsoft sign-in, OneDrive transfer, publish, or production validation without executed evidence.

## Product boundary

Implement one simple internal WPF application used by an IT administrator to:

1. sign in with Microsoft;
2. paste one employee OneDrive root URL;
3. select a local destination on the same Windows Server;
4. confirm the resolved employee, authorized transfer account, and destination;
5. press `Copy Data`; and
6. monitor progress and review the result.

Do not add dashboards, multiple pages, scheduling, batch employee import, user management, remote destinations, service mode, central reporting, or other unapproved features.

## Required implementation behavior

- Use C#, .NET 10 LTS, WPF, MVVM, Microsoft Graph v1.0, MSAL, dependency injection, local SQLite state, and automated tests.
- Keep Microsoft 365 access strictly read-only.
- Validate the configured tenant and authorized transfer-account object-ID allowlist when configured.
- Use Graph drive delta for initial inventory and reconciliation.
- Keep file and metadata processing bounded in memory.
- Classify OneNote and other Graph package items as `Unsupported`; report them and never silently claim they were copied.
- Use a fixed maximum of three simultaneous downloads.
- Use `.partial` files, safe Range resume, source metadata revalidation, supported source hashes, and local SHA-256.
- Preserve supported source timestamps and report failures as warnings.
- Bind every destination to one tenant, employee, and drive.
- Reject UNC, mapped, NAS, SMB, and remote destinations.
- Require known remaining bytes plus the fixed 5 GiB destination reserve and fail safely on disk-full.
- Implement the exact binding `PathMappingVersion = 1` rules and persist mappings.
- Prevent traversal, unsafe reparse-point redirection, hard-link overwrite, and writes outside the selected destination.
- Validate SQLite integrity before resume, back up before migration, migrate transactionally, and never silently reset corrupt state.
- Use the approved item states and run states exactly.
- Store every run's reports under `_TransferReport/Runs/<RunId>` without overwriting earlier reports.
- Keep technical details in protected logs and show simple reference-coded errors in the UI.
- Require removal and verification of temporary Site Collection Administrator access after it is no longer required.
- Require BitLocker, approved equivalent, or documented approved exception for production storage.

## Milestone discipline

Work phase by phase according to `docs/IMPLEMENTATION_PLAN.md`.

At the start of a phase:

1. Set the phase to `IN_PROGRESS`.
2. Record exact scope in `.ai/HANDOFF.md`.
3. Confirm the previous phase has valid committed evidence.

At the end of a phase:

1. Execute every required validation.
2. Commit a redacted evidence summary under `artifacts/evidence`.
3. Record the exact validated source commit.
4. Update phase status, decision log, project memory, and handoff.
5. Review for false claims, placeholders, secrets, stale superseded controls, and out-of-scope work.

Windows CI restore, Release build, and automated tests are mandatory before `Source Implementation Complete`.

## Evidence honesty

- Cross-platform restore is not a Windows build.
- Mock sign-in is not real Microsoft interactive sign-in.
- Synthetic copy tests are not a real employee OneDrive transfer.
- A publish command is not a successful publish.
- Source completion is not production readiness.
- An unexecuted check is not passed.
- A mutable branch name is not immutable evidence.
- Unsupported package content is not copied content.
- A warning state must not be reported as clean completion.

## Security

Never commit or print passwords, tokens, client secrets, authorization headers, cookies, temporary download URLs, private keys, employee content, production state databases, or unredacted production logs and reports.

## Final implementation report

Report only the justified completion label, phase status, files changed, evidence paths and validated commits, restore/build/test/Windows/sign-in/transfer/resume/publish results, required configuration, unsupported items, warning states, and genuine blockers.
