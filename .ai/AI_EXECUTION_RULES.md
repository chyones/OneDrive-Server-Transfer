# AI Execution Rules

## Goal

Implement the simple internal OneDrive-to-local-server archival-copy application defined in `IMPLEMENTATION_CONTRACT.md` and produce honest evidence at each milestone.

## Before each session

- Read `AGENTS.md` and `IMPLEMENTATION_CONTRACT.md` first.
- Read project memory, phase status, handoff, decision log, implementation plan, acceptance matrix, evidence policy, security requirements, environment inputs, and report schema.
- Treat `IMPLEMENTATION_CONTRACT_AMENDMENTS.md` as superseded history.
- Inspect the repository instead of assuming prior work.
- Identify the current phase and its exit criteria.
- Confirm the previous completed phase has committed evidence tied to an exact immutable commit.

## During implementation

- Work on one controlled phase scope at a time.
- Keep commits and pull requests intentional and reviewable.
- Do not expand the simple one-window user workflow.
- Keep production code separate from mocks and tests.
- Never request, collect, store, log, transmit, or process an employee password.
- Never authenticate as the employee.
- Authenticate only the authorized IT operator through delegated MSAL.
- Accept one employee UPN or OneDrive root URL as source-identification input.
- Require a successful current `Scan` before enabling `Start Copy`.
- Invalidate the scan when source or destination input changes.
- Use Graph v1.0 drive delta for dry-run inventory and reconciliation.
- Use local SQLite state as the operational source for scan, resume, recovery, and source binding.
- Keep CSV and JSON as audit reports only and implement `docs/REPORT_SCHEMA.md`.
- Keep queues, file streams, and metadata bounded in memory.
- Use fixed concurrency of three, `.partial` files, safe Range resume, `Retry-After`, and bounded retries.
- Calculate local SHA-256 and keep it separate from supported source-hash verification.
- Bind each destination to one tenant, employee Entra object ID, and source drive.
- Record operator identity for audit without permanently binding the archive to one operator.
- Revalidate destination containment throughout file operations.
- Preserve source item identity across rename and move.
- Do not delete retained local archive content because the source item was deleted.
- Return `Incomplete` when supported content fails, unsupported content exists, or the source does not stabilize.
- Reserve `CompletedWithWarnings` for non-content warnings after every supported item was copied or validly skipped.
- Keep user-facing errors simple and technical details in protected logs.
- Do not add future-scope placeholders.

## Evidence

Store generated raw validation under:

```text
artifacts/source
```

Store successful Windows publish output under:

```text
artifacts/win-x64
```

Commit small redacted summaries under:

```text
artifacts/evidence
```

Every completion summary must identify the exact validated source commit, environment, command or action, UTC time, result, exit code, relevant counts, raw artifact location where applicable, limitations, and redaction confirmation.

A mutable branch name, checked box, verbal statement, ignored local file, or unexecuted command is not completion evidence.

## Memory updates

After meaningful progress update:

- `.ai/PROJECT_MEMORY.md` with durable facts only
- `.ai/PHASE_STATUS.md` with justified status and evidence path
- `.ai/DECISION_LOG.md` with material approved decisions
- `.ai/HANDOFF.md` with exact current state and next action

Memory files do not override the current owner instruction or binding contract.

## Stop conditions

A phase may stop only for a genuine blocker such as:

- missing credentials or real tenant inputs
- required compatible Windows validation
- unresolved contract contradiction
- security-sensitive change requiring owner approval
- external Microsoft or environment behavior that cannot be validated safely

Complete all unblocked work, record the blocker precisely, and never substitute fake evidence.
