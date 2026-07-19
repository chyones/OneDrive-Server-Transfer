# AI Handoff

## Current position

The application has not been implemented. The repository is correcting and simplifying its binding documentation before source development begins.

## Completion label

`Not Complete`

## Current phase

`M0 — Contract simplification and correction`

Status: `IN_PROGRESS`

## Completed on the correction branch

- Replaced the oversized base contract with a concise binding contract matching the real IT workflow.
- Made `IMPLEMENTATION_CONTRACT.md` the single binding contract.
- Marked the separate amendment layer as superseded.
- Approved one local SQLite state database under `_TransferReport`.
- Required Graph v1.0 drive delta for initial inventory and reconciliation.
- Added destination binding to tenant, employee, and drive.
- Removed the custom five-million-item benchmark and custom index engine as first-release blockers.
- Simplified milestones and acceptance criteria.
- Corrected the AI authority order so current owner instructions take priority.
- Marked the former M0 evidence as invalid and reset M0 to `IN_PROGRESS`.

## Next exact action

1. Review the complete correction-branch diff.
2. Confirm all repository controls agree with the simple product workflow.
3. Address review findings before merge.
4. Merge the correction pull request only after review.
5. Create a new M0 evidence summary referencing the exact merged commit.
6. Mark M0 `DOCUMENTATION_COMPLETE` only after the new evidence is committed.
7. Begin M1 solution and CI foundation.

## Future M1 scope

- Create `./OneDriveServerTransfer.sln` at repository root.
- Create WPF application and test projects.
- Configure .NET 10, MVVM, dependency injection, structured logging, and configuration.
- Add local SQLite dependency and schema foundation without implementing fake services.
- Add Windows CI for restore, Release build, tests, static checks, vulnerability review, and secret detection.

## Known missing real-world inputs

See `docs/ENVIRONMENT_AND_INPUTS.md`.

## Prohibited assumptions

- Do not assume M0 is complete before corrected post-merge evidence exists.
- Do not assume Tenant ID, Client ID, administrator access, or production destination.
- Do not assume Windows build, WPF startup, Microsoft sign-in, or OneDrive copy succeeded.
- Do not mark the project Production Ready.