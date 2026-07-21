# Start Here

This is the short entry point for every implementation agent in this repository.

## Read first

1. `AGENTS.md`
2. `IMPLEMENTATION_CONTRACT.md`
3. `.ai/PHASE_STATUS.md`
4. `.ai/HANDOFF.md`
5. `.ai/PROJECT_MEMORY.md`
6. `.ai/DECISION_LOG.md`
7. `docs/IMPLEMENTATION_PLAN.md`
8. the phase-specific documents referenced by that plan
9. `docs/ACCEPTANCE_MATRIX.md`
10. `docs/EVIDENCE_POLICY.md`

There is no separate AI prompt or alternate contract. Do not use deleted files, old pull requests, or Git history as active instructions.

## Authoritative current state

`.ai/PHASE_STATUS.md` and `.ai/HANDOFF.md` are the authoritative current-state files. They record which phase is active, its status, whether an explicit owner instruction has authorized starting it, the exact validated source commits, and the current evidence pointer. This file deliberately does not duplicate phase details; always read those two files before deciding what to do.

## Current action

Determine the active phase and its authorization state from `.ai/PHASE_STATUS.md` and `.ai/HANDOFF.md`, then:

1. Confirm the active phase is authorized by an explicit owner instruction; if not, stop.
2. Mark the phase `IN_PROGRESS` before changing source files.
3. Implement only that phase's scope and exit criteria from `docs/IMPLEMENTATION_PLAN.md`, the binding contract, and the acceptance matrix.
4. Run the required validations, commit redacted evidence under `artifacts/evidence`, and record the exact validated source commit.
5. Update `.ai/PHASE_STATUS.md` and `.ai/HANDOFF.md` at completion.

Never claim Windows execution, Microsoft sign-in, OneDrive access, copy, resume, publish, or production readiness without executed evidence.
