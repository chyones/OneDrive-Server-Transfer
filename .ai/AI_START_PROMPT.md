# AI Implementation Start Prompt

Use this prompt with an implementation-capable agent to begin the current implementation phase.

```text
Work on the repository chyones/OneDrive-Server-Transfer.

The product is a simple internal Windows application. An authorized IT administrator signs in, pastes one employee OneDrive for Business root URL, selects a local destination on the same Windows Server, confirms the employee, authorized transfer account, and destination, presses Copy Data, and reviews the result.

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
12. .github/pull_request_template.md

IMPLEMENTATION_CONTRACT_AMENDMENTS.md is superseded and must not override IMPLEMENTATION_CONTRACT.md. Do not revive the superseded custom disk index, JSONL state engine, or five-million-item release benchmark.

M0 is DOCUMENTATION_COMPLETE only when .ai/PHASE_STATUS.md points to committed evidence tied to an exact reviewed commit. The current implementation phase is M1 — Solution and CI foundation. Mark M1 IN_PROGRESS before creating source files.

Implement M1 only. Create ./OneDriveServerTransfer.sln directly at repository root, create the WPF application and test projects, configure .NET 10 Windows targeting, MVVM, dependency injection, structured logging, configuration, SQLite foundation, deterministic restore, and mandatory Windows GitHub Actions.

Do not implement authentication, OneDrive resolution, transfer behavior, production services, or fake placeholders during M1. Define interfaces and configuration boundaries only where M1 requires them.

Do not redesign the product and do not add dashboards, scheduling, batch employee processing, remote destinations, service mode, central reporting, email notifications, or other unapproved features.

The binding later-phase requirements include configured-tenant and authorized-account validation, Graph delta inventory, explicit Unsupported handling for OneNote and other package items, local destination source binding, a fixed 5 GiB disk reserve, deterministic PathMappingVersion = 1, streaming with fixed concurrency of three, .partial resume, source revalidation, local SHA-256, source timestamp preservation, SQLite integrity and migration recovery, exact run states, and isolated Runs/<RunId> reports. Do not weaken or replace these requirements while establishing the M1 architecture.

Microsoft 365 access is read-only. Never add write permissions, a client secret, application-only authentication, or source modification behavior. Never log or persist tokens, cookies, authorization headers, temporary download URLs, employee content, production state databases, or unredacted production reports.

Windows CI restore, Release build, and automated tests are mandatory before Source Implementation Complete. Real-tenant and interactive production checks remain separate.

Before marking M1 complete, execute all required checks, commit a redacted evidence summary tied to the exact validated source commit, and update phase status, decision log, project memory, and handoff.

Do not claim Windows build, WPF execution, Microsoft sign-in, real OneDrive copy, publish, or Production Ready unless each action was actually executed in a compatible environment with evidence.
```
