# AI Start Here

## Current state

- Application implementation has not started.
- M0 contract simplification and pre-implementation hardening is documentation-only.
- Current completion label and exact evidence are defined only in `.ai/PHASE_STATUS.md`.
- Do not rely on an evidence filename or source commit copied into another entry-point document.
- Next implementation milestone after valid M0 evidence: `M1 — Solution and CI foundation`.

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
11. `/docs/MICROSOFT_PLATFORM_BASELINE.md`
12. `/docs/AUTHENTICATION_AND_TOKEN_POLICY.md`
13. `/docs/GRAPH_ENDPOINT_PERMISSION_MATRIX.md`
14. `/docs/GRAPH_DELTA_AND_RECONCILIATION_POLICY.md`
15. `/docs/GRAPH_RESILIENCY_POLICY.md`
16. `/docs/DOWNLOAD_AND_INTEGRITY_POLICY.md`
17. `/docs/PATCHING_AND_RELEASE_LIFECYCLE.md`
18. `/docs/ENVIRONMENT_AND_INPUTS.md`
19. `/docs/REPORT_SCHEMA.md`

`IMPLEMENTATION_CONTRACT_AMENDMENTS.md` is superseded and retained only for historical traceability.

The Microsoft platform documents operationalize the binding contract and are mandatory when non-conflicting. They do not authorize scope expansion, application permissions, Microsoft 365 write access, beta APIs, scheduling, service mode, or any employee-password flow.

## Product summary

The authorized IT operator opens one WPF window, signs in with Microsoft, enters one employee UPN or OneDrive for Business root URL, selects a local destination on the same Windows Server, runs a mandatory `Scan` dry run, reviews the resolved employee, operator, destination, counts, known size, unsupported items, path warnings, and storage warnings, then presses `Start Copy`, monitors progress, and reviews the result and reports.

The product is read-only against Microsoft 365. It must never request or process an employee password and must never authenticate as the employee. It does not include dashboards, scheduling, batch processing, remote destinations, or service mode.

## Microsoft platform baseline

Before implementing Microsoft integration:

- use Microsoft Graph `v1.0` only;
- use delegated interactive MSAL authentication;
- prefer WAM and support MSAL system-browser fallback;
- prohibit ROPC, device-code flow, client secrets, certificates, application permissions, and Microsoft 365 write permissions;
- implement only endpoints and scopes approved in `docs/GRAPH_ENDPOINT_PERMISSION_MATRIX.md`;
- preserve delta links as opaque values and handle supported `410 Gone` reset behavior;
- configure exactly one retry owner for each HTTP request category;
- isolate temporary download hosts from Graph credentials;
- ignore Microsoft Graph `sha256Hash`, prefer supported source hashes when available, and keep local SHA-256 separate; and
- keep self-contained releases current on supported .NET servicing patches.

Do not infer current Microsoft behavior from memory. Recheck the official Microsoft references listed in `docs/MICROSOFT_PLATFORM_BASELINE.md` before completing the affected milestone.

## Before M1

Do not begin M1 unless `.ai/PHASE_STATUS.md` records:

- M0 as `DOCUMENTATION_COMPLETE`;
- a committed evidence path;
- an exact immutable validated documentation commit; and
- no unresolved active-control contradiction.

## Next exact implementation action

Begin M1 only:

1. Mark M1 `IN_PROGRESS`.
2. Create `./OneDriveServerTransfer.sln` at repository root.
3. Create WPF application and test projects.
4. Configure .NET 10, MVVM, dependency injection, logging, configuration, SQLite, and deterministic restore.
5. Establish interfaces and configuration boundaries for the approved UPN-or-URL, Scan, Start Copy, SQLite, reporting, authentication broker, Graph request, temporary download client, retry ownership, and integrity workflow without implementing later-phase behavior.
6. Add mandatory Windows CI for restore, Release build, tests, static checks, vulnerability review, and secret detection.
7. Commit valid M1 evidence before beginning M2.

Do not create a nested project container. Never report Windows, Microsoft sign-in, employee resolution, real dry run, real OneDrive copy, or production validation without executed evidence.
