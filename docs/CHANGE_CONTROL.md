# Change Control

## Contract status

The binding implementation contract consists of:

1. `IMPLEMENTATION_CONTRACT_AMENDMENTS.md`
2. `IMPLEMENTATION_CONTRACT.md`

The amendments prevail when the two documents conflict. Requirements not changed by the amendments remain binding.

The implementation agent must not edit either document merely to simplify development.

## Changes requiring explicit approval

Explicit user approval is required before changing:

- Product purpose or backup scope
- Windows Server target
- WPF or .NET version
- Microsoft authentication method
- Microsoft Graph permissions
- Microsoft API surface
- Local-only destination rule
- Concurrency or retry invariants
- Local integrity requirements
- Reparse-point and destination-containment requirements
- NTFS or storage-protection requirements
- Manifest, disk-index, or path-mapping compatibility
- Evidence and completion-state requirements
- Supply-chain or production acceptance requirements
- Any future-version item currently out of scope

## Change procedure

1. Record the proposal in `.ai/DECISION_LOG.md` as `PROPOSED`.
2. Identify affected contract and amendment sections.
3. Describe security, compatibility, test, evidence, and migration impact.
4. Do not implement until approval is explicit.
5. After approval, update together:
   - `IMPLEMENTATION_CONTRACT_AMENDMENTS.md` or `IMPLEMENTATION_CONTRACT.md` as appropriate
   - dependent `docs/` files
   - `AGENTS.md` and AI startup files when authority or workflow changes
   - `.ai/PROJECT_MEMORY.md`
   - `.ai/DECISION_LOG.md`
   - `.ai/PHASE_STATUS.md` when phase scope changes
6. Add or update tests and evidence requirements created by the decision.
7. Preserve superseded decisions for traceability.

## Operational corrections

A small operational correction may be implemented without changing product scope only when it:

- is required to make an approved requirement work
- does not weaken security, integrity, evidence, or audit requirements
- does not add a feature
- does not change platform, permissions, destination type, or compatibility format
- is documented in the decision log and final report
- is supported by tests and evidence appropriate to the affected phase

An operational correction cannot be used to bypass the manifest-index design gate, production security baseline, or milestone evidence requirements.