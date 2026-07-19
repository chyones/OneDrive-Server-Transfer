# Change Control

## Contract status

`IMPLEMENTATION_CONTRACT.md` is the approved implementation contract.

The implementation agent must not edit it merely to simplify development.

## Changes requiring explicit approval

Explicit user approval is required before changing:

- Product purpose or backup scope
- Windows Server target
- WPF or .NET version
- Microsoft authentication method
- Microsoft Graph permissions
- Microsoft API surface
- Local-only destination rule
- Concurrency invariant
- Manifest or path-mapping compatibility
- Production acceptance requirements
- Any future-version item currently out of scope

## Change procedure

1. Record the proposal in `.ai/DECISION_LOG.md` as `PROPOSED`.
2. Identify affected contract sections.
3. Describe security, compatibility, test, and migration impact.
4. Do not implement until approval is explicit.
5. After approval, update:
   - `IMPLEMENTATION_CONTRACT.md`
   - Dependent `docs/` files
   - `.ai/PROJECT_MEMORY.md`
   - `.ai/DECISION_LOG.md`
   - `.ai/PHASE_STATUS.md` when phase scope changes
6. Add or update tests required by the decision.

## Operational corrections

A small operational correction may be implemented without changing scope when it:

- Is required to make an approved requirement work
- Does not weaken security
- Does not add a feature
- Does not change the platform or permissions
- Is documented in the decision log and final report
