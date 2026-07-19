# Start Here

Application implementation has not started. The current phase and valid evidence are defined only in `.ai/PHASE_STATUS.md`.

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

## Current action

Begin M1 only after confirming `.ai/PHASE_STATUS.md` records M0 as `DOCUMENTATION_COMPLETE` with current committed evidence.

1. Mark M1 `IN_PROGRESS`.
2. Create `OneDriveServerTransfer.sln` at repository root.
3. Create the WPF application and automated-test projects.
4. Configure .NET 10 Windows targeting, MVVM, dependency injection, logging, configuration, SQLite foundation, and deterministic restore.
5. Define clean interfaces required by later phases without implementing later-phase behavior.
6. Add mandatory Windows CI.
7. Complete M1 validation and evidence before starting M2.

Never claim Windows execution, Microsoft sign-in, OneDrive access, copy, resume, publish, or production readiness without executed evidence.
