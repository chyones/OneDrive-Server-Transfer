# Environment and Required Inputs

Do not store passwords, tokens, client secrets, employee content, temporary download URLs, production databases, or unredacted reports here.

Employee UPN and OneDrive root URL are identifiers only. The application never uses an employee password.

## Microsoft platform

| Input or validation | Required by | Status |
|---|---|---|
| Official Microsoft baseline review date | M2, M3, M5, M7, M8 | Not provided |
| .NET 10 SDK version | M1 | Not provided |
| MSAL.NET version | M2 | Not provided |
| Microsoft Graph SDK version | M3 | Not provided |
| Graph `v1.0` and beta dependency absence | M3 | Not confirmed |
| Endpoint-permission matrix reviewed | M3 | Not confirmed |
| Single retry owner selected | M5 | Not confirmed |
| Temporary-download client isolation validated | M5 | Not confirmed |
| Supported .NET servicing patch selected | M8 | Not provided |

Use current official references listed in `docs/MICROSOFT_PLATFORM_BASELINE.md`; model memory or old examples are not platform validation.

## Microsoft 365 tenant

| Input or validation | Required | Status |
|---|---:|---|
| Tenant name and primary domain | Yes | Not provided |
| Tenant ID | Yes | Not provided |
| Entra public-client application Client ID | Yes | Not provided |
| Allowed OneDrive host | Yes | Not provided |
| Authorized transfer-account object ID list | Yes | Not provided |
| Dedicated transfer administrator | Recommended | Not provided |
| Admin consent | Yes | Not confirmed |
| Public-client flow | Yes | Not confirmed |
| System-browser redirect URI `http://localhost` | Yes | Not confirmed |
| WAM behavior on target server | Yes | Not confirmed |
| System-browser fallback | Yes | Not confirmed |
| MFA and Conditional Access | Yes | Not confirmed |
| Temporary OneDrive access grant/removal procedure | Yes | Not confirmed |

Approved delegated scopes only:

```text
User.Read
Files.Read.All
Sites.Read.All
offline_access
openid
profile
```

Do not add another permission, application permission, write permission, secret, certificate, ROPC, device-code flow, or employee authentication without an approved contract and permission-matrix change.

## Test employee and source data

Provide a controlled employee OneDrive containing:

- employee object ID, UPN, and root URL;
- nested and empty folders;
- Arabic, Unicode, invalid Windows characters, reserved names, case collisions, file/folder collisions, long components, and long paths;
- a large file and interruption/resume case;
- source change, rename, move, deletion, and unstable-source cases;
- package content such as OneNote;
- timestamp cases;
- supported and missing source-hash cases;
- delta paging, duplicate occurrence, and controlled `410 Gone` reset cases;
- throttling and transient-failure cases;
- temporary URL expiration and valid/invalid Range behavior;
- invalid user, unprovisioned OneDrive, file, subfolder, consumer, shared, SharePoint, Teams, and external-tenant inputs.

Both UPN and URL modes must resolve to the same tenant ID, employee object ID, and source drive ID.

## Windows Server

| Input or validation | Required | Status |
|---|---:|---|
| Server name and exact Windows build | Yes | Not provided |
| Desktop Experience | Yes | Not confirmed |
| Windows execution account | Yes | Not provided |
| Local destination root | Yes | Not provided |
| Destination write permission | Yes | Not confirmed |
| Restricted NTFS ACL baseline | Yes | Not confirmed |
| BitLocker, approved equivalent, or exception | Yes | Not confirmed |
| Long-path support | Yes | Not confirmed |
| Free space plus fixed 5 GiB reserve | Yes | Not confirmed |
| Controlled disk-full test method | Yes | Not confirmed |
| Token-cache location and ACL | Yes | Not confirmed |
| Proxy and TLS inspection status | Yes/No | Not provided |
| Outbound HTTPS and dynamic download hosts | Yes | Not confirmed |
| Antivirus and application-control constraints | Yes | Not confirmed |
| Authenticode certificate availability | Yes/No | Not provided |
| Secondary Server 2022/2025 compatibility environment | Recommended | Not provided |

Required fixed endpoints include Microsoft identity, Microsoft Graph, and the tenant OneDrive host. Dynamic temporary download hosts must not receive Graph credentials and must not require weakened TLS validation.

## Repository and CI

| Requirement | Status |
|---|---|
| Documentation baseline | Complete |
| M1 start authorization | Granted; mark `IN_PROGRESS` before source changes |
| Windows CI | Not started |
| Required main-branch checks | Not confirmed |
| Deterministic restore | Not started |
| Static analysis, vulnerability, and secret scans | Not started |
| Graph beta and prohibited-auth detection | Not started |
| Permission and endpoint inventory evidence | Not started |
| SBOM, runtime version, and artifact hash | Not started |

Current repository phase and evidence are defined only in `.ai/PHASE_STATUS.md`.

## Application configuration

Implementation must create `appsettings.example.json` with placeholders. Real values belong in local `appsettings.json` and must not be committed.

Required keys:

```text
MicrosoftIdentity.TenantId
MicrosoftIdentity.ClientId
MicrosoftIdentity.RedirectUri = http://localhost
MicrosoftIdentity.AllowedOneDriveHost
MicrosoftIdentity.AuthorizedAccountObjectIds
Transfer.MaximumRetryAttempts = 5
```

Do not make Graph version, WAM preference, download concurrency, retry ownership, source-hash selection, destination reserve, or security controls user-configurable.

## External access-removal record

When temporary OneDrive administrative access is no longer needed, record externally the run ID, protected employee reference, operator, grant time, removal time, administrator, and verification result. The application remains read-only and does not make this permission change.
