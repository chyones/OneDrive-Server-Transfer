# AI Implementation Start Prompt

Use this prompt with an implementation-capable AI agent after granting it access to this repository.

```text
Work on the repository chyones/OneDrive-Server-Transfer.

This repository contains a binding implementation contract, approved amendments, and project-control files. Do not redesign the product and do not add features.

Before changing anything, read these files completely in this order:

1. AGENTS.md
2. IMPLEMENTATION_CONTRACT_AMENDMENTS.md
3. IMPLEMENTATION_CONTRACT.md
4. .ai/START_HERE.md
5. .ai/PROJECT_MEMORY.md
6. .ai/PHASE_STATUS.md
7. .ai/HANDOFF.md
8. .ai/DECISION_LOG.md
9. docs/IMPLEMENTATION_PLAN.md
10. docs/ACCEPTANCE_MATRIX.md
11. docs/EVIDENCE_POLICY.md
12. docs/SECURITY_AND_INTEGRITY_REQUIREMENTS.md
13. docs/ENVIRONMENT_AND_INPUTS.md

The amendments prevail when they conflict with the base contract.

The repository root is the project root. Create ./OneDriveServerTransfer.sln directly in the repository root. Do not create a nested ./OneDriveServerTransfer project directory.

Implement the application milestone by milestone. Before starting the next milestone, complete the current exit criteria, execute available validation, commit a redacted evidence summary under artifacts/evidence, and update .ai/PHASE_STATUS.md, .ai/PROJECT_MEMORY.md, .ai/DECISION_LOG.md, and .ai/HANDOFF.md.

Do not create one unreviewable implementation commit covering all source milestones. Keep milestone changes intentional and reviewable.

Do not stop at planning, scaffolding, pseudocode, or sample code. Do not add placeholders or fake validation.

The current repository state is Documentation Ready. M0 is DOCUMENTATION_COMPLETE. Application implementation has not started. Begin with M1 — Solution foundation and enforceable CI foundation.

Do not claim Windows build, WPF execution, Microsoft interactive sign-in, real OneDrive transfer, publish, benchmark acceptance, security acceptance, or Production Ready unless each was actually executed in a compatible environment and the required evidence exists.

Do not request or store passwords, client secrets, access tokens, employee files, temporary download URLs, or sensitive production reports.

At the end, return the concise implementation report required by the binding contract and AGENTS.md.
```