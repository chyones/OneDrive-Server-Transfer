# Environment and Required Inputs

Do not store passwords, tokens, secrets, employee backup content, or unredacted production reports in this document.

## Microsoft 365

| Value | Required | Status |
|---|---:|---|
| Tenant name | Yes | Not provided |
| Tenant primary domain | Yes | Not provided |
| Tenant ID | Yes | Not provided |
| Entra application Client ID | Yes | Not provided |
| Allowed OneDrive host | Yes | Not provided |
| Dedicated transfer administrator email | Yes | Not provided |
| Admin consent completed | Yes | Not confirmed |
| Public-client flow enabled | Yes | Not confirmed |
| Redirect URI `http://localhost` | Yes | Not confirmed |
| Delegated-permission threat assessment approved | Yes | Not confirmed |
| Narrower supported permission model evaluated | Yes | Not confirmed |
| Site Collection Administrator removal procedure assigned | Yes | Not confirmed |

## Test employee OneDrive

| Value | Required | Status |
|---|---:|---|
| Test employee email or protected identifier | Yes | Not provided |
| Employee OneDrive root URL | Yes | Not provided |
| Site Collection Administrator access granted | Yes | Not confirmed |
| Grant time and granting administrator recorded externally | Yes | Not confirmed |
| Browser access by administrator verified | Yes | Not confirmed |
| Nested folders prepared | Yes | Not confirmed |
| Empty folder prepared | Yes | Not confirmed |
| Arabic and Unicode names prepared | Yes | Not confirmed |
| Large file prepared | Yes | Not confirmed |
| File for modification/resume test prepared | Yes | Not confirmed |
| Invalid file/subfolder/shared/SharePoint URLs prepared | Yes | Not confirmed |
| Post-test SCA removal owner identified | Yes | Not confirmed |

## Windows Server 2019

| Value | Required | Status |
|---|---:|---|
| Server name | Yes | Not provided |
| Windows Server build | Yes | Not provided |
| Desktop Experience | Yes | Not confirmed |
| Dedicated Windows execution account | Yes | Not provided |
| RDP access | Yes | Not confirmed |
| Local destination root | Yes | Not provided |
| Free disk space | Yes | Not confirmed |
| Write permission | Yes | Not confirmed |
| Restricted NTFS ACL baseline | Yes | Not confirmed |
| Token-cache location and ACL reviewed | Yes | Not confirmed |
| BitLocker or approved equivalent enabled | Yes | Not confirmed |
| Approved encryption exception | When encryption is unavailable | Not provided |
| Long-path support | When needed | Not confirmed |
| Proxy present | Yes/No | Not provided |
| Outbound HTTPS permitted | Yes | Not confirmed |
| Antivirus/application-control constraints reviewed | Yes | Not confirmed |
| Authenticode certificate available | Yes/No | Not provided |

## Repository and CI

| Value | Required | Status |
|---|---:|---|
| Required GitHub Actions workflows created | Before source completion | Not started |
| Main-branch required checks configured | Before protected implementation merges | Not confirmed |
| Dependency restore strategy documented | Yes | Not started |
| Vulnerability scanning configured | Yes | Not started |
| Secret scanning configured | Yes | Not started |
| SBOM generation configured | Before production publish | Not started |
| Durable evidence summaries committed | Per completed milestone | M0 documentation only |

## Required delegated Microsoft Graph permissions

Current approved model:

- `User.Read`
- `Files.Read.All`
- `Sites.Read.All`
- `offline_access`
- `openid`
- `profile`

Do not create a client secret and do not add write permissions.

Before production acceptance, document the security impact of these delegated permissions and whether a narrower officially supported model can meet the validated workflow. Do not change the approved model silently.

## Production endpoints

The server must be able to reach:

```text
https://login.microsoftonline.com
https://graph.microsoft.com
https://YOURTENANT-my.sharepoint.com
```

Temporary Microsoft download hosts may vary. Requests to temporary download URLs must not include Graph bearer tokens, cookies, or Graph-specific authentication headers.

## Configuration handoff

The implementation must produce `appsettings.example.json` with placeholders. Real values are placed in local `appsettings.json` on the target server and must not be committed.

`MaximumRetryAttempts` accepts only values from `1` through `5`. Values outside this range must prevent startup.

## Post-transfer administrative record

After a completed, cancelled, or failed production test no longer requires access, record externally:

- transfer run ID
- target employee protected identifier
- SCA grant time
- SCA removal time
- administrator who performed removal
- verification result

The application remains read-only and must not perform this permission change itself.