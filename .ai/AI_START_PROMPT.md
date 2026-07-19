# AI Implementation Start Prompt

Use this prompt with an implementation-capable agent to begin the current implementation phase.

```text
Work on the repository chyones/OneDrive-Server-Transfer.

The product is a simple internal Windows application. An IT administrator signs in, pastes one employee OneDrive for Business root URL, selects a local destination on the same Windows Server, presses Copy Data, and reviews the result.

Before changing anything, read:

1. AGENTS.md
2. IMPLEMENTATION_CONTRACT.md
3. .ai/PROJECT_MEMORY.md
4. .ai/PHASE_STATUS.md
5. .ai/HANDOFF.md
6. .ai/DECISION_LOG.md
7. docs/IMPLEMENTATION_PLAN.md
8. docs/ACCEPTANCE_MATRIX.md
9. docs/EVIDENCE_POLICY.md
10. docs/SECURITY_AND_INTEGRITY_REQUIREMENTS.md
11. docs/ENVIRONMENT_AND_INPUTS.md

IMPLEMENTATION_CONTRACT_AMENDMENTS.md is superseded and must not override IMPLEMENTATION_CONTRACT.md.

M0 is DOCUMENTATION_COMPLETE. The current phase is M1 — Solution and CI foundation. Mark M1 IN_PROGRESS before creating source files.

Implement M1 only. Create ./OneDriveServerTransfer.sln directly at repository root, create the WPF application and test projects, configure .NET 10 Windows targeting, MVVM, dependency injection, structured logging, configuration, SQLite foundation, deterministic restore, and mandatory Windows GitHub Actions.

Do not redesign the product and do not add dashboards, scheduling, batch employee processing, remote destinations, service mode, central reporting, or other unapproved features.

Use C#, .NET 10 LTS, WPF, MVVM, Microsoft Graph v1.0, MSAL, dependency injection, automated tests, and local SQLite state under _TransferReport.

Microsoft 365 access is read-only. Never add write permissions, a client secret, or source modification behavior. Never log or persist tokens, cookies, authorization headers, temporary download URLs, employee content, or production state databases.

Windows CI restore, Release build, and automated tests are mandatory before Source Implementation Complete. Real-tenant and interactive production checks remain separate.

Before marking M1 complete, execute all required checks, commit a redacted evidence summary tied to the exact validated source commit, and update phase status, decision log, project memory, and handoff.

Do not claim Windows build, WPF execution, Microsoft sign-in, real OneDrive copy, publish, or Production Ready unless each action was actually executed in a compatible environment with evidence.
```