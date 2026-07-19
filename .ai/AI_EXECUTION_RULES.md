# AI Execution Rules

## Goal

Build the approved application completely while preserving the binding contract and producing durable evidence at each milestone.

## Before each work session

- Read `IMPLEMENTATION_CONTRACT_AMENDMENTS.md` before the base contract.
- Read current memory, phase status, decision log, handoff, evidence policy, and security requirements.
- Inspect the repository rather than assuming prior work.
- Identify the current phase and its exit criteria.
- Confirm the previous completed phase has a committed evidence summary.

## During implementation

- Work on one controlled milestone scope at a time.
- Keep milestone commits or pull requests intentional and reviewable.
- Keep production code separate from test doubles.
- Use production components in the synthetic benchmark.
- Do not simplify away large-scale, resume, integrity, reconciliation, recovery, audit, ACL, or reparse-point requirements.
- Keep queues and memory bounded.
- Calculate and store local SHA-256 for every completed file.
- Revalidate destination containment during file operations, not only at startup.
- Keep user-facing errors simple and reference coded.
- Keep technical details in protected logs.
- Do not add code for future out-of-scope features.

## Evidence

Store raw generated source evidence under:

```text
artifacts/source
```

Store successful Windows publish output under:

```text
artifacts/win-x64
```

Commit small redacted evidence summaries under:

```text
artifacts/evidence
```

Generated evidence must identify environment, source commit, command, result, time, exit code, relevant thresholds, raw artifact location, and limitations.

A phase is not complete when its evidence exists only in an ignored local directory.

## Memory updates

After meaningful progress:

- `PROJECT_MEMORY.md`: durable facts only
- `PHASE_STATUS.md`: status and exact committed evidence path
- `DECISION_LOG.md`: material decisions and approved operational corrections
- `HANDOFF.md`: exact current state and next action

Do not use memory files to override the binding contract.

## Stop conditions

Do not stop at planning or scaffolding.

A genuine blocker may stop progress only when:

- required credentials or real tenant values are absent
- a compatible Windows environment is required for the next validation
- a contract contradiction cannot be resolved safely
- a security-sensitive change requires user approval
- the required five-million-item manifest-index design cannot be implemented correctly under the current no-database rule

When blocked, complete every unblocked task, record the blocker precisely, and never substitute fake evidence.