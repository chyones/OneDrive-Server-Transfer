# AI Implementation Start Prompt

Use this prompt with an implementation-capable agent only after the workflow-alignment documentation evidence is committed and the pull request is merged.

```text
Work on the repository chyones/OneDrive-Server-Transfer.

The product is a simple internal Windows archival-copy application. An authorized IT operator signs in, enters one employee UPN or OneDrive for Business root URL, selects a local destination on the same Windows Server, runs a mandatory Scan dry run, confirms the employee, operator, destination, counts, known size, unsupported items, path warnings, and storage warnings, then presses Start Copy and reviews the result.

The application must never request, collect, store, log, transmit, or process an employee password. It must never authenticate as the employee. Microsoft 365 access is read-only and source content is never deleted or modified.

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
12. docs/REPORT_SCHEMA.md
13. .github/pull_request_template.md

IMPLEMENTATION_CONTRACT_AMENDMENTS.md is superseded and must not override IMPLEMENTATION_CONTRACT.md. Do not revive the superseded custom disk index, JSONL state engine, or five-million-item release benchmark.

Confirm that M0 is DOCUMENTATION_COMPLETE using the exact evidence and validated commit recorded in .ai/PHASE_STATUS.md. The next implementation phase is M1 — Solution and CI foundation. Mark M1 IN_PROGRESS before creating source files.

Implement M1 only. Create ./OneDriveServerTransfer.sln directly at repository root, create the WPF application and test projects, configure .NET 10 Windows targeting, MVVM, dependency injection, structured logging, configuration, SQLite foundation, deterministic restore, and mandatory Windows GitHub Actions.

During M1, establish boundaries that support the approved later workflow: employee UPN or OneDrive root URL input, authorized IT operator authentication, mandatory Scan before Start Copy, local destination selection, SQLite operational state, reports following docs/REPORT_SCHEMA.md, and exact run states including Incomplete.

Do not implement authentication, employee source resolution, Graph inventory, scan behavior, transfer behavior, production services, or fake successful placeholders during M1. Define interfaces and configuration boundaries only where M1 requires them.

Do not redesign the product and do not add dashboards, scheduling, batch employee processing, remote destinations, service mode, central reporting, email notifications, employee-password handling, or other unapproved features.

The binding later-phase requirements include configured-tenant and authorized-operator validation, UPN-or-URL source resolution, Graph delta dry-run inventory, explicit Unsupported handling for OneNote and other package items, Incomplete reporting for missing content, local destination source binding, a fixed 5 GiB disk reserve, deterministic PathMappingVersion = 1 with suffix expansion, streaming with fixed concurrency of three, .partial resume, Retry-After handling, source revalidation, local SHA-256, source timestamp preservation, SQLite integrity and migration recovery, exact run states, and isolated Runs/<RunId> reports.

Microsoft 365 access is read-only. Never add write permissions, a client secret, application-only authentication, employee impersonation, or source modification behavior. Never log or persist passwords, tokens, cookies, authorization headers, temporary download URLs, employee content, production state databases, or unredacted production reports.

Windows CI restore, Release build, and automated tests are mandatory before Source Implementation Complete. Real-tenant and interactive production checks remain separate.

Before marking M1 complete, execute all required checks, commit a redacted evidence summary tied to the exact validated source commit, and update phase status, decision log, project memory, and handoff.

Do not claim Windows build, WPF execution, Microsoft sign-in, employee source resolution, real dry run, real OneDrive copy, resume, publish, or Production Ready unless each action was actually executed in a compatible environment with evidence.
```
