# AGENTS.md

These instructions apply to every file and every implementation agent working in this repository.

## Authority order

Use this precedence when instructions conflict:

1. `IMPLEMENTATION_CONTRACT.md`
2. Explicit user instructions
3. `.ai/DECISION_LOG.md`
4. `.ai/PHASE_STATUS.md`
5. `docs/IMPLEMENTATION_PLAN.md`
6. Other repository documentation

Never weaken, reinterpret, or silently bypass the implementation contract.

## Current repository state

- Documentation and project controls are prepared.
- Application implementation has not started.
- Do not claim that source code, Windows build, WPF execution, Microsoft sign-in, real OneDrive transfer, publish, or production validation exists until evidence is created.
- Do not mark the project `Production Ready` from macOS or Linux validation.

## Mandatory startup sequence

Before changing the repository:

1. Read `IMPLEMENTATION_CONTRACT.md` completely.
2. Read `.ai/PROJECT_MEMORY.md`.
3. Read `.ai/PHASE_STATUS.md`.
4. Read `.ai/HANDOFF.md`.
5. Read `docs/IMPLEMENTATION_PLAN.md`.
6. Identify the current phase and its exit criteria.
7. Inspect existing files before creating replacements.

## Implementation behavior

- Implement the requested product, not a prototype.
- Do not add features outside the contract.
- Do not replace WPF, .NET 10, Microsoft Graph v1.0, MSAL, or MVVM.
- Do not use Microsoft Graph beta, SharePoint REST, CSOM, or undocumented Microsoft endpoints.
- Do not add a database.
- Do not add write permissions to Microsoft 365.
- Do not add UNC, NAS, SMB, mapped-drive, remote-server, service-mode, batch-processing, scheduling, or dashboard features.
- Do not store secrets.
- Do not use placeholders, fake success paths, simulated production results, or misleading test claims.
- Solve operational implementation problems using the smallest reliable correction that preserves the contract.

## Phase discipline

The implementation agent may work through all phases in one execution, but must treat each phase as a controlled gate.

At the end of every phase:

1. Update `.ai/PHASE_STATUS.md`.
2. Add material decisions to `.ai/DECISION_LOG.md`.
3. Update `.ai/PROJECT_MEMORY.md` only with durable facts.
4. Update `.ai/HANDOFF.md` with the current state and next exact action.
5. Store generated evidence under `artifacts/source` or `artifacts/win-x64` as defined by the contract.
6. Do not mark a phase complete without its exit evidence.

Allowed phase states:

- `NOT_STARTED`
- `IN_PROGRESS`
- `BLOCKED`
- `SOURCE_COMPLETE`
- `WINDOWS_VALIDATED`
- `PRODUCTION_VALIDATED`

## Change control

Changing the implementation contract requires explicit user approval.

When a requested change affects scope, security, permissions, platform, storage destination, authentication, or production acceptance:

1. Record the proposed change in `.ai/DECISION_LOG.md`.
2. Identify affected contract sections.
3. Do not implement the change until approval is explicit.
4. After approval, update the contract and dependent documentation together.

## Validation honesty

Use exact evidence-based language.

- Cross-platform restore is not a Windows build.
- Static analysis is not WPF execution.
- Mock authentication is not Microsoft interactive sign-in.
- Synthetic transfer is not a real employee OneDrive transfer.
- A publish command is not a successful publish.
- Source completion is not production readiness.
- An unexecuted test is not passed.

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
- Restore, build, test, benchmark, and publish evidence
- Windows steps executed and not executed
- Configuration values still required
- Genuine limitations and blockers

Do not repeat the full contract.
