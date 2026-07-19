# Microsoft Platform Baseline

This document operationalizes `IMPLEMENTATION_CONTRACT.md` against current Microsoft platform guidance without changing the approved product workflow or expanding version 1 scope.

Validated against official Microsoft documentation on **2026-07-19**. Microsoft documentation and supported patch versions change over time. Revalidate this baseline before M2, M3, M5, M7, and every internal release.

## Authority and scope

- `IMPLEMENTATION_CONTRACT.md` remains the single binding product contract.
- This document is a mandatory non-conflicting implementation control.
- Microsoft Graph `v1.0` is required. Microsoft Graph beta endpoints, beta SDK models, and preview-only behavior are prohibited unless a separately approved contract change exists.
- Version 1 remains an interactive, delegated, single-employee archive tool. It is not a daemon, scheduled backup service, or application-permission crawler.

## Authentication baseline

- Use MSAL.NET interactive delegated authentication.
- Prefer Windows Web Account Manager (WAM) as the primary interactive broker on supported Windows versions.
- Allow MSAL system-browser fallback when WAM cannot be used.
- Do not use an embedded browser unless a separately documented compatibility exception is approved.
- Do not use Resource Owner Password Credentials (ROPC), username/password acquisition, device-code flow, client credentials, certificates, managed identity, or client secrets in version 1.
- The application must never request or process an employee password.
- Support MFA and Conditional Access without attempting to bypass or automate them.
- Treat sign-out as removal of application-owned cached account state only. Do not claim to sign the user out of Windows, browsers, WAM, or every Microsoft session.
- Preserve the existing single-tenant validation and authorized transfer-account object-ID allowlist.

See `docs/AUTHENTICATION_AND_TOKEN_POLICY.md`.

## Permission baseline

- Request only delegated permissions required by the exact Microsoft Graph endpoints implemented.
- Application permissions are prohibited in version 1.
- Microsoft 365 write permissions are prohibited.
- The approved permission set must be justified endpoint by endpoint in `docs/GRAPH_ENDPOINT_PERMISSION_MATRIX.md`.
- Do not add `User.ReadBasic.All`, `User.Read.All`, `Directory.Read.All`, or any additional permission merely for convenience. Add a permission only after the endpoint matrix proves that the implemented endpoint requires it and the repository owner approves the change.
- Delegated permissions never replace SharePoint or OneDrive authorization. The signed-in transfer account must already have access to the employee OneDrive.

## Graph request baseline

- Use HTTPS and `https://graph.microsoft.com/v1.0` only.
- Use `$select` to request only properties required by the contract, state schema, verification, and reporting.
- Do not depend on property ordering, undocumented response fields, or a fixed JSON shape beyond the published contract.
- Ignore unknown JSON properties safely.
- Preserve unknown enum or facet values as `Unknown`; do not crash deserialization or silently classify unknown content as copied.
- Unknown content-affecting facets must be reported and must produce a safe `Incomplete` or `Failed` result according to impact.
- Generate a unique `client-request-id` for each Graph request and record Microsoft `request-id` and response date in protected logs.
- Never log access tokens, cookies, authorization headers, raw Graph responses, temporary download URLs, tenant IDs, drive IDs, or employee object IDs in normal user-facing output.

## Delta inventory baseline

- Use drive-item delta for initial inventory and reconciliation.
- Follow returned `@odata.nextLink` and `@odata.deltaLink` values exactly as opaque URLs.
- Do not parse, edit, rebuild, normalize, or concatenate delta tokens.
- Persist checkpoints per bound source drive.
- The same item can appear more than once; process by Drive Item ID and use the last occurrence in a completed delta sequence.
- Track hierarchy by Drive Item ID and parent ID, not only by path.
- Treat the `deleted` facet as a source-state change. Never automatically delete an already archived local file.
- Handle `HTTP 410 Gone` by following the returned fresh enumeration location, rebuilding the current source inventory, and reconciling it with application state.
- A reset delta token is not SQLite corruption and must not cause silent state deletion.

See `docs/GRAPH_DELTA_AND_RECONCILIATION_POLICY.md`.

## Resilience baseline

- Respect `Retry-After` for `429 Too Many Requests` and other responses that provide it.
- When `Retry-After` is absent for a retryable response, use bounded exponential backoff with jitter.
- Do not retry permanent validation, authorization, binding, or unsupported-content failures.
- Exactly one layer owns automatic Graph retry behavior. Do not stack custom retry loops over an SDK retry handler without disabling or accounting for one of them.
- Keep a single observable retry budget per operation and per file.
- Do not retry immediately while throttled.
- Keep the contract maximum of five attempts per file and fixed maximum of three simultaneous downloads.

See `docs/GRAPH_RESILIENCY_POLICY.md`.

## Download baseline

- Treat `@microsoft.graph.downloadUrl` and `/content` redirect targets as short-lived, preauthenticated secrets.
- Use them immediately and never cache or persist them.
- Do not store them in SQLite, reports, evidence, telemetry, crash dumps, or logs.
- Use a separate unauthenticated HTTP client for temporary download hosts.
- Never forward Graph bearer tokens, cookies, or Graph-specific headers to a temporary download host.
- Apply `Range` to the actual temporary download URL, not to the Graph `/content` request.
- Resume only after validating `206 Partial Content` and matching `Content-Range`.
- If a range request returns `200 OK`, discard the unsafe partial continuation decision and restart that file from byte zero.
- Obtain a fresh temporary URL when the prior URL expires.

## Integrity baseline

- Microsoft Graph `sha256Hash` is unsupported and must not be used.
- Prefer source `quickXorHash` when available; keep `sha1Hash` or `crc32Hash` only when supplied and explicitly supported.
- Keep Microsoft source hashes separate from the locally calculated SHA-256.
- Absence of a Microsoft source hash is not itself a file failure, but the application must not claim source cryptographic verification.
- Do not require folder `cTag`; delta responses can omit properties, and OneDrive for Business does not provide all file-style tags for folders.

See `docs/DOWNLOAD_AND_INTEGRITY_POLICY.md`.

## SDK and dependency baseline

- Pin direct NuGet dependencies and commit deterministic restore metadata.
- Do not accept floating package versions.
- Review Microsoft Graph SDK and MSAL release notes before upgrades.
- An SDK upgrade must not silently alter retry count, serialization behavior, authentication broker behavior, or request headers.
- Dependency upgrades require restore, Windows Release build, automated tests, vulnerability review, and updated evidence.

## .NET and Windows lifecycle baseline

- Target .NET 10 LTS and remain current on supported .NET 10 servicing patches.
- A self-contained deployment contains its own runtime and must be republished to receive later runtime security fixes.
- Record the exact SDK and bundled runtime patch in release evidence.
- Windows Server 2019 remains the primary approved target and is in extended support through 2029-01-09.
- No release may be marked Production Ready when its Windows target or bundled .NET runtime is out of support.
- Add a secondary compatibility validation target using Windows Server 2022 or Windows Server 2025 when available; this does not replace the required Server 2019 acceptance test for version 1.

See `docs/PATCHING_AND_RELEASE_LIFECYCLE.md`.

## Mandatory source references

Implementation decisions must be checked against the current official pages, including:

- WAM for MSAL.NET: https://learn.microsoft.com/entra/msal/dotnet/acquiring-tokens/desktop-mobile/wam
- MSAL browser guidance: https://learn.microsoft.com/entra/msal/dotnet/acquiring-tokens/using-web-browsers
- Graph best practices: https://learn.microsoft.com/graph/best-practices-concept
- Graph permissions reference: https://learn.microsoft.com/graph/permissions-reference
- Drive item delta: https://learn.microsoft.com/graph/api/driveitem-delta?view=graph-rest-1.0
- Graph throttling: https://learn.microsoft.com/graph/throttling
- Download drive item content: https://learn.microsoft.com/graph/api/driveitem-get-content?view=graph-rest-1.0
- Drive-item hashes: https://learn.microsoft.com/graph/api/resources/hashes?view=graph-rest-1.0
- .NET support policy: https://dotnet.microsoft.com/platform/support/policy/dotnet-core
- Self-contained runtime patch selection: https://learn.microsoft.com/dotnet/core/deploying/runtime-patch-selection
- Windows Server 2019 lifecycle: https://learn.microsoft.com/lifecycle/products/windows-server-2019

## Review evidence

Every platform-baseline review must record:

- review UTC time;
- exact documentation URLs reviewed;
- relevant last-updated dates when published;
- current MSAL, Graph SDK, .NET SDK, and runtime versions;
- permission changes, if any;
- behavioral changes affecting authentication, delta, retry, download, or hashes;
- accepted decisions and rejected changes; and
- exact repository commit containing the reviewed baseline.
