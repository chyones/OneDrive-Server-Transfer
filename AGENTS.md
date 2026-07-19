# AGENTS.md

These instructions apply to every file and every implementation agent working in this repository.

## Authority order

Use this precedence when instructions conflict:

1. `IMPLEMENTATION_CONTRACT_AMENDMENTS.md`
2. `IMPLEMENTATION_CONTRACT.md`
3. Explicit user instructions
4. `.ai/DECISION_LOG.md`
5. `.ai/PHASE_STATUS.md`
6. `docs/IMPLEMENTATION_PLAN.md`
7. Other repository documentation

The binding contract is the combination of the amendments and the base contract. Never weaken, reinterpret, or silently bypass either document.

## Current repository state

- Documentation and project controls are prepared.
- Application implementation has not started.
- Do not claim that source code, Windows build, WPF execution, Microsoft sign-in, real OneDrive transfer, publish, benchmark acceptance, or production validation exists until evidence is created.
- Do not mark the project `Production Ready` from macOS or Linux validation.
- M0 is `DOCUMENTATION_COMPLETE`, not `SOURCE_COMPLETE`.

## Mandatory startup sequence

Before changing the repository:

1. Read `IMPLEMENTATION_CONTRACT_AMENDMENTS.md` completely.
2. Read `IMPLEMENTATION_CONTRACT.md` completely.
3. Read `.ai/PROJECT_MEMORY.md`.
4. Read `.ai/PHASE_STATUS.md`.
5. Read `.ai/HANDOFF.md`.
6. Read `docs/IMPLEMENTATION_PLAN.md`.
7. Read `docs/ACCEPTANCE_MATRIX.md`.
8. Read `docs/EVIDENCE_POLICY.md`.
9. Read `docs/SECURITY_AND_INTEGRITY_REQUIREMENTS.md`.
10. Identify the current phase and its exit criteria.
11. Inspect existing files before creating replacements.

## Repository-root rule

The repository root is the project root.

Create:

```text
./OneDriveServerTransfer.sln
./src
./tests
./scripts
./docs
./artifacts
```

Do not create a second nested `./OneDriveServerTransfer` project directory.

## Implementation behavior

- Implement the requested product, not a prototype.
- Do not add features outside the binding contract.
- Do not replace WPF, .NET 10, Microsoft Graph v1.0, MSAL, or MVVM.
- Do not use Microsoft Graph beta, SharePoint REST, CSOM, or undocumented Microsoft endpoints.
- Do not add a database unless the user explicitly approves a contract amendment.
- Before M5, define and approve a genuine disk-based manifest lookup design suitable for five million items.
- Do not add write permissions to Microsoft 365.
- Do not add UNC, NAS, SMB, mapped-drive, remote-server, service-mode, batch-processing, scheduling, or dashboard features.
- Do not store secrets.
- Do not use placeholders, fake success paths, simulated production results, or misleading test claims.
- Calculate and persist local SHA-256 for every completed file.
- Treat destination containment and reparse-point protection as continuous runtime invariants.
- Solve operational implementation problems using the smallest reliable correction that preserves the contract.

## Milestone discipline

Implementation must proceed milestone by milestone. An agent may continue through several milestones in one working session only after independently completing and evidencing each milestone.

At the start of every phase:

1. Set the phase to `IN_PROGRESS`.
2. Record the exact scope in `.ai/HANDOFF.md`.
3. Confirm the previous phase has a committed redacted evidence summary.

At the end of every phase:

1. Execute all available validation for that phase.
2. Commit a redacted evidence summary under `artifacts/evidence` according to `docs/EVIDENCE_POLICY.md`.
3. Update `.ai/PHASE_STATUS.md` with the exact evidence path.
4. Add material decisions to `.ai/DECISION_LOG.md`.
5. Update `.ai/PROJECT_MEMORY.md` only with durable facts.
6. Update `.ai/HANDOFF.md` with the current state and next exact action.
7. Review the diff for scope, security, placeholders, TODOs, and false completion claims.
8. Do not mark the phase complete without its exit evidence.

Do not create one unreviewable commit covering all source milestones. Keep milestone changes intentional and reviewable.

Allowed phase states:

- `NOT_STARTED`
- `IN_PROGRESS`
- `BLOCKED`
- `DOCUMENTATION_COMPLETE`
- `SOURCE_COMPLETE`
- `WINDOWS_VALIDATED`
- `PRODUCTION_VALIDATED`

## Evidence rules

Generated raw evidence belongs under `artifacts/source` or `artifacts/win-x64` and may be uploaded as GitHub Actions artifacts.

Small redacted durable summaries belong under `artifacts/evidence` and must be committed.

A pull-request checkbox, verbal statement, unexecuted command, or ignored local file is not evidence.

## Change control

Changing the binding contract requires explicit user approval.

When a requested change affects scope, security, permissions, platform, storage destination, authentication, integrity, manifest architecture, or production acceptance:

1. Record the proposed change in `.ai/DECISION_LOG.md`.
2. Identify affected contract and amendment sections.
3. Do not implement the change until approval is explicit.
4. After approval, update the amendments or contract and dependent documentation together.

## Validation honesty

Use exact evidence-based language.

- Cross-platform restore is not a Windows build.
- Static analysis is not WPF execution.
- Mock authentication is not Microsoft interactive sign-in.
- Synthetic transfer is not a real employee OneDrive transfer.
- A publish command is not a successful publish.
- Source completion is not production readiness.
- An unexecuted test is not passed.
- A checked PR-template item is not an automated quality gate.
- File size and metadata are not source cryptographic verification.

## Security

Never commit or print:

- Passwords
- Access tokens
- Refresh tokens
- Client secrets
- Authentication headers
- Cookies
- Temporary download URLs
- Private keys
- Employee OneDrive contents
- Production reports containing sensitive employee data

Use redacted examples and placeholders only.

## Final response format

When implementation work ends, report:

- Current completion label
- Phase status
- Files changed
- Committed evidence-summary paths
- Restore, build, test, benchmark, supply-chain, and publish evidence
- Windows steps executed and not executed
- Configuration values still required
- Genuine limitations and blockers

Do not repeat the full contract.