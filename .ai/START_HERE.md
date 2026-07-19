# AI Start Here

## Current state

- Application implementation has not started.
- M0 contract correction is `DOCUMENTATION_COMPLETE`.
- Current completion label: `Documentation Ready`.
- Next milestone: `M1 — Solution and CI foundation`.
- M0 evidence: `artifacts/evidence/M00_contract-correction_20260719T110925Z.json`.

## Required reading order

1. `/AGENTS.md`
2. `/IMPLEMENTATION_CONTRACT.md`
3. `/.ai/PROJECT_MEMORY.md`
4. `/.ai/PHASE_STATUS.md`
5. `/.ai/HANDOFF.md`
6. `/.ai/DECISION_LOG.md`
7. `/docs/IMPLEMENTATION_PLAN.md`
8. `/docs/ACCEPTANCE_MATRIX.md`
9. `/docs/EVIDENCE_POLICY.md`
10. `/docs/SECURITY_AND_INTEGRITY_REQUIREMENTS.md`
11. `/docs/ENVIRONMENT_AND_INPUTS.md`

`IMPLEMENTATION_CONTRACT_AMENDMENTS.md` is superseded and retained only for historical traceability.

## Product summary

The IT administrator opens one WPF window, signs in with Microsoft, pastes one employee OneDrive for Business root URL, selects a local destination on the same Windows Server, presses `Copy Data`, monitors progress, and reviews the result.

The product is read-only against Microsoft 365 and does not include dashboards, scheduling, batch processing, remote destinations, or service mode.

## Next exact action

Begin M1 only:

1. Mark M1 `IN_PROGRESS`.
2. Create `./OneDriveServerTransfer.sln` at repository root.
3. Create WPF application and test projects.
4. Configure .NET 10, MVVM, dependency injection, logging, configuration, SQLite, and deterministic restore.
5. Add mandatory Windows CI for restore, Release build, tests, static checks, vulnerability review, and secret detection.
6. Commit valid M1 evidence before beginning M2.

Do not create a nested project container. Never report Windows, Microsoft sign-in, real OneDrive copy, or production validation without executed evidence.