# Microsoft Graph Endpoint and Permission Matrix

This matrix defines the approved Microsoft Graph surface for version 1. It applies least privilege to the exact endpoints required by `IMPLEMENTATION_CONTRACT.md`.

The implementation must not add an endpoint, Graph permission, write method, beta API, or directory-wide permission without updating this matrix and obtaining repository-owner approval.

## Approved delegated scopes

```text
openid
profile
offline_access
User.Read
Files.Read.All
Sites.Read.All
```

Notes:

- `openid`, `profile`, and `offline_access` are Microsoft identity platform scopes, not Microsoft Graph data permissions.
- `User.Read` is used for the signed-in IT operator only.
- `Files.Read.All` permits the application to read files the signed-in operator can access. It does not independently grant the operator access to every employee OneDrive.
- `Sites.Read.All` is required for approved OneDrive-root URL resolution through the employee personal-site path.
- No write permission is approved.
- No application permission is approved.

## Approved endpoint matrix

| ID | Operation | Method and endpoint | Required selected fields | Approved delegated permission | Purpose and constraints |
|---|---|---|---|---|---|
| `GRAPH-AUTH-001` | Read signed-in operator | `GET /me?$select=id,userPrincipalName,displayName` | `id`, `userPrincipalName`, `displayName` | `User.Read` | Validate and display the authenticated IT operator. The durable authorization identity is `id`, not display name or UPN alone. |
| `GRAPH-SRC-001` | Resolve employee default drive from UPN | `GET /users/{idOrUserPrincipalName}/drive?$select=id,driveType,webUrl,owner,quota` | `id`, `driveType`, `webUrl`, `owner`, optional quota fields needed for warnings | `Files.Read.All` | Resolve the employee's default business OneDrive. The signed-in operator must already be authorized to access it. Reject non-business drives. |
| `GRAPH-SRC-002` | Resolve personal site from approved OneDrive root URL | `GET /sites/{hostname}:/{relative-path}?$select=id,webUrl,isPersonalSite,siteCollection` | `id`, `webUrl`, `isPersonalSite`, `siteCollection` | `Sites.Read.All` | Resolve only the configured tenant OneDrive host and employee personal-site root. Reject SharePoint team, project, communication, external, file, subfolder, and shared-link inputs. |
| `GRAPH-SRC-003` | Resolve default drive from validated personal site | `GET /sites/{site-id}/drive?$select=id,driveType,webUrl,owner,quota` | `id`, `driveType`, `webUrl`, `owner`, optional quota fields needed for warnings | `Files.Read.All` or `Sites.Read.All` as supported by the current endpoint documentation | Confirm the same default business drive as UPN mode. The implementation must use the least approved permission already granted and prove behavior in the tenant. |
| `GRAPH-SCAN-001` | Initial inventory and reconciliation | `GET /drives/{drive-id}/root/delta?$select=...` followed by returned opaque links | Only contract-required identity, hierarchy, facet, size, time, tag, hash, and download metadata | `Files.Read.All` | Enumerate the complete drive using delta paging. Do not scan sharing permissions or request write scopes. |
| `GRAPH-ITEM-001` | Re-read item metadata | `GET /drives/{drive-id}/items/{item-id}?$select=...` | Item ID, parent ID, name, size, file/folder/package/deleted facets, eTag/cTag when available, timestamps, hashes | `Files.Read.All` | Revalidate source identity and metadata before accepting a completed file. |
| `GRAPH-DL-001` | Obtain file content redirect or preauthenticated URL | `GET /drives/{drive-id}/items/{item-id}/content` or metadata request selecting `@microsoft.graph.downloadUrl` | No unnecessary properties | `Files.Read.All` | Obtain short-lived file content access. Do not persist or log the redirect target. |
| `GRAPH-DELTA-001` | Continue delta paging | Exact returned `@odata.nextLink` | As encoded by Microsoft | Existing approved Graph permission | Treat the URL as opaque. Do not modify query parameters or token values. |
| `GRAPH-DELTA-002` | Continue later reconciliation | Exact returned `@odata.deltaLink` | As encoded by Microsoft | Existing approved Graph permission | Persist per source drive and use as opaque state. |
| `GRAPH-DELTA-003` | Reset invalid delta state | Follow the `Location` URL returned with supported `410 Gone` delta response | As encoded by Microsoft | Existing approved Graph permission | Perform a fresh enumeration and reconciliation. Do not reset SQLite or delete archived files. |

## Explicitly prohibited endpoints and methods

Version 1 must not call:

- Microsoft Graph `/beta`;
- `POST`, `PUT`, `PATCH`, or `DELETE` against Microsoft 365 content;
- invitation, sharing-link, permission-grant, or permission-removal endpoints;
- upload-session or content-upload endpoints;
- Recycle Bin or version-history endpoints;
- search endpoints as a replacement for complete delta inventory;
- directory enumeration endpoints for all users;
- audit, compliance, retention, activity, or sharing-permission APIs;
- consumer OneDrive endpoints;
- SharePoint or Teams library copy endpoints; or
- application-registration management endpoints.

## Additional permissions are not preapproved

The following are not approved for version 1 unless a separate reviewed contract change proves necessity:

```text
User.ReadBasic.All
User.Read.All
Directory.Read.All
Sites.Selected
Sites.FullControl.All
Sites.ReadWrite.All
Files.ReadWrite
Files.ReadWrite.All
Any application permission
```

Do not add `User.ReadBasic.All` merely to look up an employee profile. UPN mode should resolve the employee's default drive through the file endpoint and persist the durable employee identity exposed by the validated source. If implementation evidence proves that a separate user endpoint is unavoidable, update this matrix, identify the exact endpoint and fields, prove the least-privileged permission, and obtain approval before changing Entra consent.

## Request rules

Every Graph request must:

- use `v1.0`;
- use HTTPS;
- include a unique `client-request-id`;
- request only required fields through `$select` where supported;
- use the approved MSAL token provider;
- preserve returned paging links as opaque values;
- use the single retry owner defined in `docs/GRAPH_RESILIENCY_POLICY.md`;
- redact protected identifiers from normal UI errors; and
- avoid raw-response logging.

## Permission validation evidence

Before M3 is complete, evidence must record:

- exact Entra delegated permissions granted;
- admin-consent status;
- exact Graph SDK and MSAL versions;
- each endpoint actually called;
- successful UPN and URL resolution using a test employee;
- proof that UPN and URL modes resolve to the same tenant, employee object, and Drive ID;
- expected `403` behavior when the operator lacks employee OneDrive access;
- proof that no write scope or application permission exists; and
- a redacted captured request inventory containing methods and endpoint templates, not tokens or real protected identifiers.

## Current official references

- Graph permissions reference: https://learn.microsoft.com/graph/permissions-reference
- Get drive: https://learn.microsoft.com/graph/api/drive-get?view=graph-rest-1.0
- Get site by path: https://learn.microsoft.com/graph/api/site-getbypath?view=graph-rest-1.0
- Drive item delta: https://learn.microsoft.com/graph/api/driveitem-delta?view=graph-rest-1.0
- Get drive item: https://learn.microsoft.com/graph/api/driveitem-get?view=graph-rest-1.0
- Download drive item content: https://learn.microsoft.com/graph/api/driveitem-get-content?view=graph-rest-1.0
