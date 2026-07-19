# Agent Instructions

These rules apply to every implementation agent in this repository.

## Authority

When instructions conflict, follow this order:

1. Current explicit instruction from the repository owner.
2. `IMPLEMENTATION_CONTRACT.md`.
3. Approved active decisions in `.ai/DECISION_LOG.md`.
4. This file.
5. `.ai/PHASE_STATUS.md`, `.ai/HANDOFF.md`, and `docs/IMPLEMENTATION_PLAN.md`.
6. Other documentation.

Do not infer requirements from deleted files, Git history, old pull requests, or superseded evidence.

## Required startup

Read in this order:

1. `IMPLEMENTATION_CONTRACT.md`
2. `.ai/PHASE_STATUS.md`
3. `.ai/HANDOFF.md`
4. `.ai/PROJECT_MEMORY.md`
5. `.ai/DECISION_LOG.md`
6. the documents listed for the current phase in `docs/IMPLEMENTATION_PLAN.md`
7. `docs/ACCEPTANCE_MATRIX.md`
8. `docs/EVIDENCE_POLICY.md`

`.ai/START_HERE.md` is the short entry point. Do not search for another AI prompt or instruction file.

## Scope discipline

- Work on one phase only.
- Mark the phase `IN_PROGRESS` before changing source files.
- Do not begin the next phase until the current exit criteria and evidence are complete.
- Do not redesign the one-window workflow or add unapproved features.
- Do not implement later-phase behavior as a placeholder.
- Do not modify the binding contract merely to make an implementation pass.

## Non-negotiable controls

- The application is read-only against Microsoft 365.
- Never request, process, store, or log an employee password.
- Never authenticate as the employee.
- Version 1 uses delegated interactive MSAL only, Microsoft Graph `v1.0` only, and approved read scopes only.
- Application permissions, Graph beta, Microsoft 365 write permissions, ROPC, device-code flow, client secrets, and certificates are prohibited.
- Implement only endpoints and permissions listed in `docs/GRAPH_ENDPOINT_PERMISSION_MATRIX.md`.
- Use SQLite as operational state; CSV and JSON are audit outputs only.
- Never expose secrets, temporary download URLs, raw Graph responses, employee content, or production state.
- Never claim successful completion when supported content is missing or unsupported content exists.

Detailed behavior belongs in the binding contract and phase-specific documents; do not duplicate or reinterpret it here.

## Evidence and completion

At phase completion:

1. Execute every required validation.
2. Commit a redacted evidence summary under `artifacts/evidence`.
3. Record the exact validated source commit.
4. Update `.ai/PHASE_STATUS.md` and `.ai/HANDOFF.md`.
5. Update `.ai/PROJECT_MEMORY.md` only for durable facts.
6. Update `.ai/DECISION_LOG.md` only for a new approved material decision.

An unexecuted command, mutable branch, checked box, mock, or verbal statement is not evidence. Windows CI does not prove real-tenant behavior, and source completion does not mean Production Ready.

## Stop conditions

Stop only for a genuine external blocker, unresolved contract contradiction, missing required environment access, or an owner decision. Complete all unblocked work and report the blocker precisely.
