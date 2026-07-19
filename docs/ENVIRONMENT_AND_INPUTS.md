# Environment and Required Inputs

Do not store passwords, tokens, client secrets, employee content, temporary download URLs, production state databases, or unredacted reports in this document.

## Microsoft 365

| Value | Required | Status |
|---|---:|---|
| Tenant name | Yes | Not provided |
| Tenant primary domain | Yes | Not provided |
| Tenant ID | Yes | Not provided |
| Entra public-client application Client ID | Yes | Not provided |
| Allowed OneDrive host | Yes | Not provided |
| Dedicated transfer administrator email | Yes | Not provided |
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

Do not create a client secret or add write permissions.

## Test employee OneDrive

| Value | Required | Status |
|---|---:|---|
| Test employee protected identity | Yes | Not provided |
| Employee OneDrive root URL | Yes | Not provided |
| Administrator access granted externally | Yes | Not confirmed |
| Browser access by administrator verified | Yes | Not confirmed |
| Nested folders prepared | Yes | Not confirmed |
| Empty folder prepared | Yes | Not confirmed |
| Arabic and Unicode names prepared | Yes | Not confirmed |
| Large file prepared | Yes | Not confirmed |
| File-change test prepared | Yes | Not confirmed |
| Invalid file, subfolder, consumer, shared, and SharePoint URLs prepared | Yes | Not confirmed |

## Windows Server 2019

| Value | Required | Status |
|---|---:|---|
| Server name | Yes | Not provided |
| Windows Server build | Yes | Not provided |
| Desktop Experience | Yes | Not confirmed |
| Windows execution account | Yes | Not provided |
| RDP access | Yes | Not confirmed |
| Local destination root | Yes | Not provided |
| Free disk space and reserved headroom | Yes | Not confirmed |
| Write permission | Yes | Not confirmed |
| Restricted NTFS ACL baseline | Yes | Not confirmed |
| Token-cache location and ACL reviewed | Yes | Not confirmed |
| BitLocker or approved equivalent | Yes | Not confirmed |
| Approved encryption exception | When needed | Not provided |
| Long-path support | When needed | Not confirmed |
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
| Contract correction reviewed and merged | Before M1 | Not complete |
| Corrected M0 evidence tied to merged commit | Before M1 | Not complete |
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
Transfer.MaximumRetryAttempts = 5
```

`MaximumRetryAttempts` accepts values from 1 through 5. Download concurrency is fixed at three and is not configurable.

## Post-transfer administrative record

When temporary employee OneDrive administrative access is no longer needed, record externally:

- transfer run ID
- protected employee identity
- access grant time
- access removal time
- administrator performing removal
- verification result

The application remains read-only and does not perform this permission change.