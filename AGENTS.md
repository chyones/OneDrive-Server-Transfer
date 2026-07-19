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

- M0 contract correction is `DOCUMENTATION_COMPLETE`.
- Completion label: `Documentation Ready`.
- Application implementation has not started.
- Next phase: `M1 — Solution and CI foundation`.
- M0 evidence: `artifacts/evidence/M00_contract-correction_20260719T110925Z.json`.
- Do not claim source code, Windows build, WPF execution, Microsoft sign-in, OneDrive transfer, publish, or production validation without executed evidence.

## Product boundary

Implement one simple internal WPF application used by an IT administrator to:

1. sign in with Microsoft;
2. paste one employee OneDrive root URL;
3. select a local destination on the same Windows Server;
4. confirm the resolved employee and destination;
5. press `Copy Data`; and
6. monitor progress and review the result.

Do not add dashboards, multiple pages, scheduling, batch employee import, user management, remote destinations, service mode, central reporting, or other unapproved features.

## Required implementation behavior

- Use C#, .NET 10 LTS, WPF, MVVM, Microsoft Graph v1.0, MSAL, dependency injection, local SQLite state, and automated tests.
- Keep Microsoft 365 access strictly read-only.
- Use Graph drive delta for initial inventory and reconciliation.
- Keep file and metadata processing bounded in memory.
- Use a fixed maximum of three simultaneous downloads.
- Use `.partial` files, safe Range resume, source metadata revalidation, supported source hashes, and local SHA-256.
- Bind every destination to one tenant, employee, and drive.
- Reject UNC, mapped, NAS, SMB, and remote destinations.
- Prevent traversal, unsafe reparse-point redirection, hard-link overwrite, and writes outside the selected destination.
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
5. Review for false claims, placeholders, secrets, and out-of-scope work.

Windows CI restore, Release build, and automated tests are mandatory before `Source Implementation Complete`.

## Evidence honesty

- Cross-platform restore is not a Windows build.
- Mock sign-in is not real Microsoft interactive sign-in.
- Synthetic copy tests are not a real employee OneDrive transfer.
- A publish command is not a successful publish.
- Source completion is not production readiness.
- An unexecuted check is not passed.

## Security

Never commit or print passwords, tokens, client secrets, authorization headers, cookies, temporary download URLs, private keys, employee content, production state databases, or unredacted production logs and reports.

## Final implementation report

Report only the justified completion label, phase status, files changed, evidence paths and validated commits, restore/build/test/Windows/sign-in/transfer/resume/publish results, required configuration, and genuine blockers.