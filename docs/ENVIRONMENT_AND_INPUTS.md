# Environment and Required Inputs

Do not store passwords, tokens, secrets, or employee backup content in this document.

## Microsoft 365

| Value | Required | Status |
|---|---:|---|
| Tenant name | Yes | Not provided |
| Tenant primary domain | Yes | Not provided |
| Tenant ID | Yes | Not provided |
| Entra application Client ID | Yes | Not provided |
| Allowed OneDrive host | Yes | Not provided |
| Administrator email | Yes | Not provided |
| Admin consent completed | Yes | Not confirmed |
| Public-client flow enabled | Yes | Not confirmed |
| Redirect URI `http://localhost` | Yes | Not confirmed |

## Test employee OneDrive

| Value | Required | Status |
|---|---:|---|
| Test employee email | Yes | Not provided |
| Employee OneDrive root URL | Yes | Not provided |
| Site Collection Administrator access granted | Yes | Not confirmed |
| Browser access by administrator verified | Yes | Not confirmed |
| Nested folders prepared | Yes | Not confirmed |
| Empty folder prepared | Yes | Not confirmed |
| Arabic and Unicode names prepared | Yes | Not confirmed |
| Large file prepared | Yes | Not confirmed |
| File for modification/resume test prepared | Yes | Not confirmed |
| Invalid file/subfolder/shared/SharePoint URLs prepared | Yes | Not confirmed |

## Windows Server 2019

| Value | Required | Status |
|---|---:|---|
| Server name | Yes | Not provided |
| Windows Server build | Yes | Not provided |
| Desktop Experience | Yes | Not confirmed |
| Windows execution account | Yes | Not provided |
| RDP access | Yes | Not confirmed |
| Local destination root | Yes | Not provided |
| Free disk space | Yes | Not confirmed |
| Write permission | Yes | Not confirmed |
| Long-path support | When needed | Not confirmed |
| Proxy present | Yes/No | Not provided |
| Outbound HTTPS permitted | Yes | Not confirmed |
| Antivirus/application-control constraints reviewed | Yes | Not confirmed |

## Required delegated Microsoft Graph permissions

- `User.Read`
- `Files.Read.All`
- `Sites.Read.All`
- `offline_access`
- `openid`
- `profile`

Do not create a client secret and do not add write permissions.

## Production endpoints

The server must be able to reach:

```text
https://login.microsoftonline.com
https://graph.microsoft.com
https://YOURTENANT-my.sharepoint.com
```

## Configuration handoff

The implementation must produce `appsettings.example.json` with placeholders. Real values are placed in local `appsettings.json` on the target server and must not be committed.
