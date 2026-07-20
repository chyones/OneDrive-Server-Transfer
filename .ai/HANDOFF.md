# AI Handoff

## Current position

- Documentation baseline: complete.
- Application source: M1 foundation and M2 authentication complete; later-phase behavior not implemented.
- Current phase: `M3 — Employee source resolution`.
- Status: `NOT_STARTED`. M3 requires explicit owner instruction before work begins.
- M2 evidence: `artifacts/evidence/M02_authentication_20260720T095437Z.json` on validated source commit `a1afd839e79f86e01e44a9f40a46b4b46363a988` (Windows CI run 29732929639, all checks passed, 136/136 tests).

The exact evidence pointer is maintained only in `.ai/PHASE_STATUS.md`.

## M2 outcome (completed)

Implemented on branch `agent/m2-microsoft-authentication`:

- delegated interactive MSAL sign-in (Microsoft.Identity.Client and Microsoft.Identity.Client.Broker 4.86.1), single-tenant public client, WAM preferred with MSAL system-browser fallback;
- tenant validation, guest-account rejection, optional operator object-ID allowlist, approved-scope check, Graph `/me` operator validation (GRAPH-AUTH-001 only);
- silent token acquisition with controlled reauthentication; consent, cancellation, `401`, `403`, tenant-mismatch, unauthorized-operator, cache-corruption, and broker-unavailable handling;
- DPAPI-protected persistent token cache for the current Windows user, ACL-restricted, with corruption fail-safe; accurate remember-sign-in and truthful application-only sign-out;
- reference-coded user errors and sanitized structured auth logging;
- WPF shell sign-in, operator display, remember sign-in, and sign-out (MVVM);
- `appsettings.example.json` placeholders only; startup configuration validation fails safely.

## M3 task (not started)

Before changing source files, mark M3 `IN_PROGRESS`. Read `docs/GRAPH_ENDPOINT_PERMISSION_MATRIX.md` and `docs/MICROSOFT_PLATFORM_BASELINE.md` first.

Implement M3 only:

- accept employee UPN or OneDrive root URL;
- use approved Graph `v1.0` endpoints and delegated scopes only;
- resolve tenant, employee object ID, and business drive ID;
- reject invalid, unprovisioned, consumer, shared, file, subfolder, SharePoint, Teams, and external-tenant sources;
- classify package and unknown content semantics safely;
- generate protected request correlation data.

## M3 boundaries

Do not implement destination binding, Scan, file transfer, resume, reports, or production behavior during M3. Prohibited paths remain: Graph beta, application permissions, write permissions, ROPC, device-code flow, client secrets, certificates, employee-password handling.

## Completion

A phase is complete only when its exit criteria in `docs/IMPLEMENTATION_PLAN.md` pass, Windows CI validates the exact source commit, and committed evidence references that commit.

Real tenant values and acceptance inputs remain external; see `docs/ENVIRONMENT_AND_INPUTS.md`.
