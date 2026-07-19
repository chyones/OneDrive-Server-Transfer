# AI Execution Rules

## Goal

Build the approved application completely while preserving the contract and producing evidence at each milestone.

## Before each work session

- Read the binding contract.
- Read current memory, phase status, decision log, and handoff.
- Inspect the repository rather than assuming prior work.
- Identify the current phase and its exit criteria.

## During implementation

- Work on the current phase and continue when its exit criteria are met.
- Keep production code separate from test doubles.
- Use the production components in the synthetic benchmark.
- Do not simplify away large-scale, resume, integrity, reconciliation, or recovery requirements.
- Keep queues bounded.
- Keep user-facing errors simple and reference coded.
- Keep technical details in protected logs.
- Do not add code for future out-of-scope features.

## Evidence

Store source-level evidence under:

```text
artifacts/source
```

Store successful Windows publish output under:

```text
artifacts/win-x64
```

Generated evidence must identify environment, command, result, time, and limitations.

## Memory updates

After meaningful progress:

- `PROJECT_MEMORY.md`: durable facts only
- `PHASE_STATUS.md`: status and evidence
- `DECISION_LOG.md`: material decisions and approved operational corrections
- `HANDOFF.md`: exact current state and next action

Do not use memory files to override the contract.

## Stop conditions

Do not stop at planning or scaffolding.

A genuine blocker may stop progress only when:

- Required credentials or real tenant values are absent
- A compatible Windows environment is required for the next validation
- A contract contradiction cannot be resolved safely
- A security-sensitive change requires user approval

When blocked, complete every unblocked task, record the blocker precisely, and never substitute fake evidence.
