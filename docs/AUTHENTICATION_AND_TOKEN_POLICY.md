# Authentication and Token Policy

This document operationalizes the authentication requirements in `IMPLEMENTATION_CONTRACT.md`. It does not permit employee impersonation, unattended authentication, or Microsoft 365 write access.

## Version 1 authentication model

- Application type: single-tenant public-client desktop application.
- Actor: authorized IT transfer operator.
- Access type: delegated Microsoft Graph access.
- Primary interactive broker: Windows Web Account Manager (WAM).
- Fallback: MSAL system browser when WAM is unavailable or unsupported.
- MFA and Conditional Access: supported through the official Microsoft sign-in experience.
- Client secret: prohibited.
- Certificate credential: prohibited.
- Application permissions: prohibited.
- Employee password or employee authentication: prohibited.

## Required MSAL behavior

1. Build the MSAL public-client application using the configured Tenant ID and Client ID.
2. Enable broker support for WAM on supported Windows environments.
3. Allow the MSAL-supported system-browser fallback path.
4. Acquire the signed-in account and tokens using MSAL APIs only.
5. Attempt silent acquisition for the selected cached account before requesting interactive authentication.
6. When interaction is required, show the official Microsoft sign-in flow.
7. Validate token claims and Microsoft Graph `/me` results against the configured tenant and authorized-account object-ID allowlist.
8. Reject every non-allowed operator when an allowlist is configured.
9. Never authorize by display name or mutable UPN alone.

## Prohibited flows

The implementation must not include:

- ROPC or `AcquireTokenByUsernamePassword`;
- employee username/password fields;
- device-code flow;
- client credentials;
- managed identity;
- workload identity;
- app-only Graph access;
- embedded web views, unless a separately approved compatibility exception exists;
- token acquisition through custom HTTP OAuth calls;
- browser automation;
- MFA automation or bypass; or
- storage of Microsoft credentials in configuration or SQLite.

Automated tests and static checks must fail when prohibited APIs, models, configuration keys, or UI controls are introduced.

## WAM and browser fallback

- WAM is the preferred authentication experience on Windows Server 2019 and later.
- The application must not assume WAM is always available.
- When MSAL falls back to the system browser, the configured public-client redirect URI must match the Entra application registration.
- `http://localhost` is the approved system-browser redirect URI unless current MSAL documentation and the Entra registration require an approved change.
- The application must clearly report that interactive authentication is required without exposing protocol or token details.
- WAM and browser fallback must both be exercised during production acceptance where the environment permits controlled fallback testing.

## Token cache

- Use MSAL's account and token APIs; do not parse or manage refresh tokens directly.
- Protect persistent application-owned token-cache bytes with Windows DPAPI for the current Windows user.
- Restrict token-cache file permissions to the execution account and authorized administrators.
- Do not store tokens in SQLite, logs, reports, evidence, configuration, crash diagnostics, or Windows Credential Manager unless a separately approved design replaces this policy.
- Clear application-owned persistent cache during application sign-out.
- Sign-out wording must not claim that Windows, WAM, browsers, or every Microsoft session has been signed out.
- A cache-corruption error must fail safely and require reauthentication; never log cache contents.

## Account and tenant validation

After authentication, validate at minimum:

- token tenant claim matches `MicrosoftIdentity.TenantId`;
- Microsoft Graph `/me` object ID matches the token subject identity expected by the implementation;
- account object ID is present in `AuthorizedAccountObjectIds` when the allowlist is configured;
- the account is a work or school account in the configured tenant;
- the granted scopes include the approved delegated permissions required for the attempted operation; and
- the account can access the resolved employee OneDrive before scan succeeds.

A mutable UPN may be displayed and recorded for audit but is not the durable authorization identity.

## Consent and permission failure handling

- Missing admin consent must produce a stable authorization error and corrective action.
- Revoked consent, revoked operator access, expired sessions, and Conditional Access challenges must not be disguised as file failures.
- `401 Unauthorized` should trigger one controlled token-refresh or interactive-authentication path when appropriate.
- `403 Forbidden` must be classified as authorization or policy failure unless the exact Graph response proves another supported category.
- Do not retry authorization failures as transient download errors.

## Sign-in state

Approved application authentication states:

```text
SignedOut
InteractiveSignInRequired
SigningIn
SignedInValidated
SignedInUnauthorized
ReauthenticationRequired
SigningOut
AuthenticationFailed
```

Only `SignedInValidated` may proceed to employee source resolution or scan.

## Logging and correlation

Protected authentication logs may record:

- UTC time;
- authentication state transition;
- sanitized MSAL error code;
- tenant-validation result;
- allowlist-validation result;
- account-cache operation result;
- correlation ID; and
- stable application error reference.

They must not record:

- access or refresh tokens;
- authorization headers;
- cookies;
- passwords;
- full token claims;
- raw MSAL cache bytes;
- temporary download URLs; or
- raw identity-provider responses.

## Acceptance requirements

Automated tests must cover:

- WAM configuration boundary;
- system-browser fallback boundary;
- tenant mismatch;
- authorized-account allowlist success and rejection;
- no-allowlist deployment behavior;
- silent acquisition success and interaction-required behavior;
- application cache sign-out semantics;
- cache corruption;
- missing consent;
- revoked consent;
- `401` reauthentication handling;
- `403` authorization handling;
- no employee-password models, controls, keys, fixtures, or logs;
- no client secret or app-only flow; and
- redaction of MSAL and Graph errors.

Production acceptance must include actual Microsoft interactive sign-in on Windows Server 2019 using the approved transfer account, MFA or Conditional Access when enforced, and evidence that the operator was validated by tenant and object ID.

## Official references

- https://learn.microsoft.com/entra/msal/dotnet/acquiring-tokens/desktop-mobile/wam
- https://learn.microsoft.com/entra/msal/dotnet/acquiring-tokens/using-web-browsers
- https://learn.microsoft.com/entra/msal/dotnet/acquiring-tokens/desktop-mobile/username-password-authentication
- https://learn.microsoft.com/graph/auth/auth-concepts
