# Change Control

## Contract status

`IMPLEMENTATION_CONTRACT.md` is the single binding repository contract.

`IMPLEMENTATION_CONTRACT_AMENDMENTS.md` is superseded and retained only for historical traceability.

Current explicit instructions from the repository owner take priority and must be incorporated through reviewed documentation changes when they alter durable scope.

## Changes requiring explicit owner approval

Explicit approval is required before changing:

- product purpose or copied-data scope
- Windows Server target
- WPF, .NET, or Microsoft Graph platform choices
- Microsoft authentication method or permissions
- local-only destination rule
- one-employee-per-run workflow
- concurrency or retry invariants
- SQLite state model or compatibility version
- destination source binding
- integrity, path-containment, NTFS, or storage-protection requirements
- evidence and completion-state rules
- any future feature currently out of scope

## Change procedure

1. Record the proposal in `.ai/DECISION_LOG.md` as `PROPOSED`.
2. Identify affected contract sections.
3. Describe security, compatibility, test, evidence, and migration impact.
4. Obtain explicit owner approval.
5. Update the binding contract and dependent documents together.
6. Update project memory, phase status, handoff, and AI instructions.
7. Add or update required tests and evidence.
8. Preserve superseded decisions for traceability.
9. Use a reviewable pull request; do not merge known unresolved blocking findings.

## Operational corrections

A small operational correction may be implemented without expanding scope only when it:

- is required to make an approved requirement work
- does not weaken security, integrity, source binding, or evidence
- does not add a feature
- does not change platform, permissions, destination type, or state compatibility
- is documented in the decision log and implementation report
- is supported by appropriate tests and evidence

Operational corrections cannot be used to bypass owner approval or to make unsupported completion claims.