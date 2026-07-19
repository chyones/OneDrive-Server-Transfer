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

`IMPLEMENTATION_CONTRACT_AMENDMENTS.md` is superseded and is retained only for historical traceability.

## Current repository state

- Application implementation has not started.
- M0 contract correction is in progress.
- Do not claim source code, Windows build, WPF execution, Microsoft sign-in, OneDrive transfer, publish, or production validation without executed evidence.

## Product boundary

Implement a simple internal WPF application used by an IT administrator to:

1. sign in with Microsoft
2. paste one employee OneDrive root URL
3. select a local destination on the same Windows Server
4. press `Copy Data`
5. monitor progress and review the result

Do not add dashboards, multiple pages, scheduling, batch employee import, user management, remote destinations, service mode, central reporting, or other unapproved features.

## Required implementation behavior

- Use C#, .NET 10 LTS, WPF, MVVM, Microsoft Graph v1.0, MSAL, dependency injection, and automated tests.
- Keep Microsoft 365 access strictly read-only.
- Use the Graph drive delta flow for initial inventory and reconciliation.
- Use local SQLite state under `_TransferReport` for resume and crash recovery.
- Keep file and metadata processing bounded in memory.
- Use a fixed maximum of three simultaneous file downloads.
- Use `.partial` files, safe Range resume, source metadata revalidation, supported source hashes, and local SHA-256.
- Bind every destination to one tenant, employee, and drive.
- Reject UNC, mapped, NAS, SMB, and remote destinations.
- Prevent path traversal, unsafe reparse-point redirection, and writes outside the selected destination.
- Keep technical details in protected logs and show simple reference-coded errors in the UI.

## Milestone discipline

Work phase by phase according to `docs/IMPLEMENTATION_PLAN.md`.

At the start of a phase:

1. Set the phase to `IN_PROGRESS`.
2. Record the exact scope in `.ai/HANDOFF.md`.
3. Confirm previous completion claims have valid committed evidence.

At the end of a phase:

1. Execute all available validation.
2. Commit a small redacted evidence summary under `artifacts/evidence`.
3. Record the exact source commit or validated commit in the evidence.
4. Update phase status, decision log, project memory, and handoff.
5. Review for false claims, placeholders, secrets, and out-of-scope work.

Do not mark a phase complete merely because files were created or boxes were checked.

## Evidence honesty

- Cross-platform restore is not a Windows build.
- Mock sign-in is not real Microsoft interactive sign-in.
- Synthetic copy tests are not a real employee OneDrive transfer.
- A publish command is not a successful publish.
- Source completion is not production readiness.
- An unexecuted check is not passed.

## Security

Never commit or print:

- passwords
- access or refresh tokens
- client secrets
- authentication headers or cookies
- temporary download URLs
- private keys
- employee OneDrive content
- production transfer-state databases
- unredacted production logs or reports

## Final implementation report

Report only:

- justified completion label
- phase status
- files changed
- evidence paths and validated source commits
- restore, build, test, Windows, sign-in, transfer, resume, and publish results
- configuration still required
- genuine blockers or limitations