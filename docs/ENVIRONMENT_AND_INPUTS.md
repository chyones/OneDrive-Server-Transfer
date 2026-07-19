# Environment and Required Inputs

Do not store passwords, tokens, client secrets, employee content, temporary download URLs, production state databases, or unredacted reports in this document.

The application must never request or use an employee password. Employee UPN and OneDrive root URL are source identifiers only.

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
| Redirect URI `http://localhost` | Yes | Not confirmed |
| Delegated permissions approved | Yes | Not confirmed |
| Site Collection Administrator grant/removal procedure assigned | Yes | Not confirmed |

Approved delegated scopes:

```text
User.Read
Files.Read.All
Sites.Read.All
offline_access
openid
profile
```

Do not create a client secret, add write permissions, request an employee password, authenticate as the employee, or authorize accounts by display name or mutable email address alone.

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
| OneNote notebook or other package item prepared | Yes | Not confirmed |
| Timestamp-preservation cases prepared | Yes | Not confirmed |
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
| Windows execution account | Yes | Not provided |
| RDP access | Yes | Not confirmed |
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
| Antivirus/application-control constraints reviewed | Yes | Not confirmed |
| Authenticode certificate available | Yes/No | Not provided |

## Required endpoints

```text
https://login.microsoftonline.com
https://graph.microsoft.com
https://YOURTENANT-my.sharepoint.com
```

Temporary Microsoft download hosts vary. Requests to them must not include Graph bearer tokens, cookies, or Graph-specific authorization headers.

## Repository and CI

| Value | Required | Status |
|---|---:|---|
| Original contract correction reviewed and merged | Before M1 | Complete — PR #2 |
| Pre-implementation hardening reviewed and merged | Before M1 | Complete — PR #3, commit `e9434ff54c373e1d0129ba2583027897f6f3ff25` |
| Workflow alignment reviewed and merged | Before M1 | In progress on documentation branch |
| Replacement M0 evidence tied to workflow-alignment commit | Before M1 | Not yet committed |
| Windows GitHub Actions workflows | Before source completion | Not started |
| Main-branch required checks | Before protected implementation merges | Not confirmed |
| Deterministic dependency restore | Yes | Not started |
| Vulnerability and secret scanning | Yes | Not started |
| SBOM generation | Before internal release | Not started |

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

Do not add an employee password, employee credential, client secret, Microsoft 365 write scope, or fixed hard-coded archive drive to configuration.

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
