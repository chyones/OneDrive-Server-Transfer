# Environment and Required Inputs

Do not store passwords, tokens, client secrets, employee content, temporary download URLs, production state databases, or unredacted reports in this document.

The application must never request or use an employee password. Employee UPN and OneDrive root URL are source identifiers only.

## Microsoft platform review

| Value | Required | Status |
|---|---:|---|
| Microsoft platform baseline reviewed against official documentation | Before M2, M3, M5, M7, and M8 | Not confirmed |
| Review UTC date | Yes | Not provided |
| MSAL.NET version selected | Before M2 completion | Not provided |
| Microsoft Graph SDK version selected | Before M3 completion | Not provided |
| .NET SDK version selected | Before M1 completion | Not provided |
| Approved .NET 10 servicing patch for release | Before M8 | Not provided |
| Microsoft Graph base version | Yes | `v1.0` required |
| Graph beta dependencies absent | Yes | Not confirmed |
| Endpoint and permission matrix reviewed | Before M3 | Not confirmed |
| Single Graph retry owner selected | Before M5 | Not confirmed |
| Temporary download HTTP client isolated from Graph credentials | Before M5 | Not confirmed |

Use the current official links in `docs/MICROSOFT_PLATFORM_BASELINE.md`. Do not treat model memory, an old SDK sample, or a previously copied documentation statement as current platform validation.

## Microsoft 365

| Value | Required | Status |
|---|---:|---|
| Tenant name | Yes | Not provided |
| Tenant primary domain | Yes | Not provided |
| Tenant ID | Yes | Not provided |
| Entra public-client application Client ID | Yes | Not provided |
| Allowed OneDrive host | Yes | Not provided |
| Authorized transfer-account Entra object ID or IDs | Yes | Not provided |
| Dedicated transfer administrator email | Recommended | Not provided |
| Admin consent completed | Yes | Not confirmed |
| Public-client flow enabled | Yes | Not confirmed |
| System-browser redirect URI `http://localhost` registered | Yes | Not confirmed |
| WAM broker behavior validated on target Windows Server | Yes | Not confirmed |
| MSAL system-browser fallback validated | Yes | Not confirmed |
| MFA and Conditional Access behavior validated | Yes | Not confirmed |
| Delegated permissions approved | Yes | Not confirmed |
| Application permissions absent | Yes | Not confirmed |
| Microsoft 365 write permissions absent | Yes | Not confirmed |
| Site Collection Administrator grant/removal procedure assigned | Yes | Not confirmed |

Approved identity and delegated scopes:

```text
User.Read
Files.Read.All
Sites.Read.All
offline_access
openid
profile
```

The exact endpoints and permission reasons are defined in `docs/GRAPH_ENDPOINT_PERMISSION_MATRIX.md`.

Do not add `User.ReadBasic.All`, `User.Read.All`, `Directory.Read.All`, another Graph permission, a client secret, certificate, application permission, write permission, employee password, employee authentication, device-code flow, or ROPC without a separately approved contract and permission-matrix change.

## Test employee OneDrive

| Value | Required | Status |
|---|---:|---|
| Test employee Entra object ID | Yes | Not provided |
| Test employee UPN | Yes | Not provided |
| Employee OneDrive root URL | Yes for URL-mode acceptance | Not provided |
| Administrator read access granted externally | Yes | Not confirmed |
| Browser access by administrator verified | Yes | Not confirmed |
| UPN resolves to the same default business drive as the root URL | Yes | Not confirmed |
| Nested folders prepared | Yes | Not confirmed |
| Empty folder prepared | Yes | Not confirmed |
| Arabic and Unicode names prepared | Yes | Not confirmed |
| Reserved Windows names and unsafe characters prepared | Yes | Not confirmed |
| Case-insensitive and file-versus-folder collision cases prepared | Yes | Not confirmed |
| Forced residual collision test vectors prepared | Source tests | Not confirmed |
| Long component and long full-path cases prepared | Yes | Not confirmed |
| Large file prepared | Yes | Not confirmed |
| File-change test prepared | Yes | Not confirmed |
| Rename and move test prepared | Yes | Not confirmed |
| Source deletion test prepared | Yes | Not confirmed |
| Continued-change and unstable-source test prepared | Yes | Not confirmed |
| Delta `410 Gone` reset test method prepared | Yes | Not confirmed |
| Duplicate delta-item occurrence test prepared | Source tests | Not confirmed |
| Unknown Graph property and enum test vectors prepared | Source tests | Not confirmed |
| OneNote notebook or other package item prepared | Yes | Not confirmed |
| Timestamp-preservation cases prepared | Yes | Not confirmed |
| Supported Microsoft source-hash case prepared | Yes | Not confirmed |
| Missing source-hash case prepared | Yes | Not confirmed |
| Microsoft Graph unsupported `sha256Hash` ignored in tests | Source tests | Not confirmed |
| Temporary download URL expiration test method prepared | Yes | Not confirmed |
| Range returns `206 Partial Content` test method prepared | Yes | Not confirmed |
| Range ignored and returns `200 OK` test method prepared | Yes | Not confirmed |
| Throttling `429` with `Retry-After` test method prepared | Yes | Not confirmed |
| Transient failure without `Retry-After` test prepared | Source tests | Not confirmed |
| Consent or employee-access revocation test prepared | Yes | Not confirmed |
| Invalid user, unprovisioned user, file, subfolder, consumer, shared, SharePoint, and external-tenant inputs prepared | Yes | Not confirmed |

Production acceptance must exercise both source-input modes:

1. employee UPN; and
2. employee OneDrive for Business root URL.

Both modes must resolve to the same Tenant ID, employee Entra object ID, and source Drive ID.

## Mandatory dry-run acceptance data

The test source must allow validation that `Scan` accurately reports:

- resolved employee;
- signed-in authorized operator;
- input mode;
- file count;
- folder count;
- known source bytes;
- unsupported count;
- path warnings;
- destination warnings;
- storage warnings; and
- whether `Start Copy` may be enabled.

Changing the source or destination after scan must invalidate the result and disable `Start Copy`.

## Windows Server 2019

| Value | Required | Status |
|---|---:|---|
| Server name | Yes | Not provided |
| Windows Server build | Yes | Not provided |
| Desktop Experience | Yes | Not confirmed |
| Current Windows support status reviewed | Before release | Not confirmed |
| Windows execution account | Yes | Not provided |
| RDP access | Yes | Not confirmed |
| WAM available or controlled fallback behavior verified | Yes | Not confirmed |
| Local destination root | Yes | Not provided |
| Destination is not a Windows system or application installation directory | Yes | Not confirmed |
| Free disk space and fixed 5 GiB reserve available | Yes | Not confirmed |
| Disk-full test destination or controlled test method | Yes | Not confirmed |
| Write permission | Yes | Not confirmed |
| Restricted NTFS ACL baseline | Yes | Not confirmed |
| Token-cache location and ACL reviewed | Yes | Not confirmed |
| BitLocker or approved equivalent | Yes | Not confirmed |
| Approved encryption exception | When needed | Not provided |
| Long-path support | Yes | Not confirmed |
| Proxy present | Yes/No | Not provided |
| Outbound HTTPS permitted | Yes | Not confirmed |
| TLS inspection behavior reviewed | When present | Not confirmed |
| Antivirus/application-control constraints reviewed | Yes | Not confirmed |
| Authenticode certificate available | Yes/No | Not provided |
| Windows Server 2022 or 2025 secondary compatibility environment | Recommended | Not provided |

## Required endpoints

```text
https://login.microsoftonline.com
https://graph.microsoft.com
https://YOURTENANT-my.sharepoint.com
```

Temporary Microsoft download hosts vary. Requests to them must not include Graph bearer tokens, cookies, Graph-specific authorization headers, or Graph middleware. Temporary URLs must not be persisted or logged.

Proxy, firewall, TLS inspection, and endpoint-security rules must permit the official Microsoft authentication and Graph endpoints plus dynamic temporary download hosts without weakening TLS validation or broadly disabling protection.

## Repository and CI

| Value | Required | Status |
|---|---:|---|
| Original contract correction reviewed and merged | Before M1 | Complete — PR #2 |
| Pre-implementation hardening reviewed and merged | Before M1 | Complete — PR #3, commit `e9434ff54c373e1d0129ba2583027897f6f3ff25` |
| Workflow alignment reviewed and merged | Before M1 | Complete — PR #5, commit `c93b38b7e41ffbb50c82b4f8389e71ef511ac54d` |
| Replacement M0 evidence tied to workflow-alignment commit | Before M1 | Complete — `artifacts/evidence/M00_workflow-alignment_20260719T124036Z.json` |
| Microsoft platform documentation update reviewed and merged | Before affected milestone completion | Not complete |
| M1 start authorization | Before source changes | Granted; mark M1 `IN_PROGRESS` before implementation |
| Windows GitHub Actions workflows | Before source completion | Not started |
| Main-branch required checks | Before protected implementation merges | Not confirmed |
| Deterministic dependency restore | Yes | Not started |
| Vulnerability and secret scanning | Yes | Not started |
| Graph beta dependency detection | Yes | Not started |
| Prohibited authentication-flow detection | Yes | Not started |
| Permission and endpoint inventory evidence | Before M3 | Not started |
| SBOM generation | Before internal release | Not started |
| Bundled self-contained runtime version evidence | Before internal release | Not started |
| Release artifact hash | Before internal release | Not started |

## Application configuration

Implementation must create `appsettings.example.json` with placeholders. Real values belong in local `appsettings.json` on the server and must not be committed.

Required values:

```text
MicrosoftIdentity.TenantId
MicrosoftIdentity.ClientId
MicrosoftIdentity.RedirectUri = http://localhost
MicrosoftIdentity.AllowedOneDriveHost
MicrosoftIdentity.AuthorizedAccountObjectIds
Transfer.MaximumRetryAttempts = 5
```

`AuthorizedAccountObjectIds` contains one or more approved Entra object IDs. `MaximumRetryAttempts` accepts values from 1 through 5. Download concurrency is fixed at three, and the destination-space safety reserve is fixed at 5 GiB; neither is configurable or shown in the UI.

Do not make WAM preference, Graph API version, download concurrency, retry ownership, temporary-host credential isolation, source-hash algorithm selection, or destination reserve user-configurable. These are implementation controls.

Do not add an employee password, employee credential, client secret, certificate credential, Microsoft 365 write scope, application permission, beta endpoint, device-code flow, ROPC setting, or fixed hard-coded archive drive to configuration.

## Post-archive administrative record

When temporary employee OneDrive administrative access is no longer needed, record externally:

- run ID;
- employee Entra object ID or approved protected reference;
- normalized employee UPN where organizational policy permits;
- signed-in operator;
- access grant time;
- access removal time;
- administrator performing removal; and
- verification result.

The application remains read-only and does not perform this permission change.
