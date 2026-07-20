# AI Handoff

## Current position

- Documentation baseline: complete.
- Application source: M1 solution and CI foundation complete; later-phase behavior not implemented.
- Current phase: `M2 — Microsoft authentication`.
- Status: `NOT_STARTED`. M2 requires explicit owner instruction before work begins.
- M1 evidence: `artifacts/evidence/M01_solution-foundation_20260720T055700Z.json` on validated source commit `6940eb7b43d868c419bfa814724b5d2a9316dcbc` (Windows CI run 29720061002, all checks passed, 26/26 tests).

The exact evidence pointer is maintained only in `.ai/PHASE_STATUS.md`.

## M1 outcome (completed)

Implemented on branch `agent/m1-solution-foundation`:

- root `OneDriveServerTransfer.sln` with `src/OneDriveServerTransfer.App` (WPF, net10.0-windows) and `tests/OneDriveServerTransfer.Tests` (xunit, net10.0-windows);
- MVVM shell, generic-host dependency injection, Serilog structured file logging, validated `appsettings.example.json`;
- SQLite schema foundation (metadata only, `StateSchemaVersion = 1`, `PathMappingVersion = 1`);
- later-phase interfaces for authentication, Graph metadata, temporary downloads, retry ownership, hashing, local storage, transfer state, and reports (no implementations, no fakes);
- central package management with committed lock files; `global.json` pins the 10.0.3xx SDK band;
- Windows CI (`.github/workflows/windows-ci.yml`): locked restore, Release build with analyzers and warnings as errors, tests, vulnerability review, prohibited authentication/API checks (`scripts/Test-ProhibitedContent.ps1`), and gitleaks secret detection.

## M2 task (not started)

Before changing source files, mark M2 `IN_PROGRESS`. Read `docs/AUTHENTICATION_AND_TOKEN_POLICY.md` and `docs/MICROSOFT_PLATFORM_BASELINE.md` first.

Implement M2 only:

- delegated interactive MSAL sign-in for the authorized IT operator, WAM preferred with system-browser fallback;
- MFA and Conditional Access support;
- tenant and optional operator object-ID allowlist validation;
- silent token renewal and DPAPI-protected application token cache;
- truthful sign-out semantics and correct consent, `401`, and `403` handling.

## M2 boundaries

Do not implement employee resolution, Graph inventory, Scan, file transfer, resume, reports, or production behavior during M2. Prohibited paths remain: Graph beta, application permissions, write permissions, ROPC, device-code flow, client secrets, certificates, employee-password handling.

## Completion

A phase is complete only when its exit criteria in `docs/IMPLEMENTATION_PLAN.md` pass, Windows CI validates the exact source commit, and committed evidence references that commit.

Real tenant values and acceptance inputs remain external; see `docs/ENVIRONMENT_AND_INPUTS.md`.
