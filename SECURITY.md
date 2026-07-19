# Security Policy

## Security posture

OneDrive Server Transfer is designed as a read-only Microsoft 365 backup-copy utility. The current version must never request Microsoft 365 write permissions or modify source OneDrive content.

## Never commit secrets

Do not commit:

- Microsoft 365 passwords
- Access or refresh tokens
- Client secrets
- Private certificates or private keys
- Authentication headers or cookies
- Temporary Microsoft download URLs
- Production `appsettings.json`
- Employee OneDrive files
- Production manifests, logs, or reports containing employee information

Only `appsettings.example.json` with placeholders may be committed during implementation.

## Approved Microsoft access model

- Interactive delegated sign-in through MSAL
- Public-client application registration
- MFA and Conditional Access support
- Required delegated permissions only:
  - `User.Read`
  - `Files.Read.All`
  - `Sites.Read.All`
  - `offline_access`
  - `openid`
  - `profile`
- No client secret
- No write permission
- Microsoft Graph `v1.0` only

## Source access

The IT administrator grants Site Collection Administrator access to the employee OneDrive outside the application. The application validates existing access but does not grant or alter permissions.

## Destination

Only local storage attached to the same Windows Server is permitted. UNC, mapped network drives, NAS, SMB, and remote destinations are outside scope.

## Reporting a security issue

Do not place sensitive details in a public issue. Use the repository owner's private communication channel and provide:

- A concise description
- Affected component
- Reproduction conditions
- Potential impact
- Redacted evidence

Do not include active credentials, employee data, or tokens.
