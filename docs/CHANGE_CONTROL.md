# Change Control

`IMPLEMENTATION_CONTRACT.md` is the single binding contract. Current explicit owner instructions have highest authority.

## Owner approval required

Explicit approval is required before changing:

- product purpose or copied-data scope;
- platform, target Windows version, authentication method, or Graph permissions;
- local-only destination or one-employee-per-run rules;
- concurrency, retry, state schema, path mapping, or source binding;
- security, integrity, evidence, completion-state, or production requirements;
- any feature currently out of scope.

## Procedure

1. Record the proposed decision in `.ai/DECISION_LOG.md`.
2. Identify affected contract sections and compatibility, security, test, evidence, and migration impact.
3. Obtain explicit owner approval.
4. Update the binding contract and affected active documents together.
5. Add or update tests and evidence.
6. Use a reviewable pull request and resolve blocking findings before merge.
7. Remove superseded wording from active files; historical traceability remains in Git history and merged pull requests.

Small implementation corrections may proceed without scope expansion only when they preserve all binding controls and are covered by tests and evidence. They cannot be used to bypass owner approval or weaken a requirement.
