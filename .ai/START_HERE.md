# AI Start Here

## Current state

- Repository preparation and pre-implementation hardening are complete.
- Application code has not been implemented.
- Current milestone: `M0`.
- Current completion label: `Documentation Ready`.
- M0 status: `DOCUMENTATION_COMPLETE`.
- The next implementation milestone is `M1 — Solution foundation and enforceable CI foundation`.

## Required reading order

1. `/AGENTS.md`
2. `/IMPLEMENTATION_CONTRACT_AMENDMENTS.md`
3. `/IMPLEMENTATION_CONTRACT.md`
4. `/.ai/PROJECT_MEMORY.md`
5. `/.ai/PHASE_STATUS.md`
6. `/.ai/HANDOFF.md`
7. `/.ai/DECISION_LOG.md`
8. `/docs/IMPLEMENTATION_PLAN.md`
9. `/docs/ACCEPTANCE_MATRIX.md`
10. `/docs/EVIDENCE_POLICY.md`
11. `/docs/SECURITY_AND_INTEGRITY_REQUIREMENTS.md`
12. `/docs/ENVIRONMENT_AND_INPUTS.md`

## First implementation action

Create the .NET 10 WPF solution foundation directly under the repository root according to M1.

Required solution path:

```text
./OneDriveServerTransfer.sln
```

Do not create a nested `./OneDriveServerTransfer` project directory.

Before writing code:

- Confirm the repository contains no pre-existing implementation.
- Update M1 to `IN_PROGRESS` in `.ai/PHASE_STATUS.md`.
- Update `.ai/HANDOFF.md` with the active task.
- Preserve the local-only, read-only, one-employee-OneDrive scope.
- Plan enforceable CI and evidence-summary generation as part of M1.

## Working rule

Proceed milestone by milestone. Every completed milestone requires a committed redacted evidence summary under `artifacts/evidence` before the next milestone starts.

Do not claim Windows validation when working on macOS or Linux.