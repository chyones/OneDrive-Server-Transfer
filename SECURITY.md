# Security Policy

## Security posture

OneDrive Server Transfer is designed as a read-only Microsoft 365 backup-copy utility. The current version must never request Microsoft 365 write permissions or modify source OneDrive content.

The binding security requirements are defined by:

1. `IMPLEMENTATION_CONTRACT_AMENDMENTS.md`
2. `IMPLEMENTATION_CONTRACT.md`
3. `docs/SECURITY_AND_INTEGRITY_REQUIREMENTS.md`

## Never commit secrets or employee data

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
- Unredacted ACL, tenant, account, destination, or audit details

Only `appsettings.example.json` with placeholders may be committed during implementation.

Small validation summaries committed under `artifacts/evidence` must be redacted and must explicitly confirm that they contain no secrets or employee data.

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

Before production acceptance, document the threat impact of the delegated permissions and persistent session access. Evaluate a narrower officially supported model without silently changing the approved authentication design.

Use a dedicated transfer administrator account where operationally possible.

## Microsoft access lifecycle

The IT administrator grants Site Collection Administrator access to the employee OneDrive outside the application. The application validates existing access but does not grant, alter, or remove permissions.

When a completed, failed, or cancelled transfer no longer requires that access:

- remove the temporary Site Collection Administrator assignment
- verify removal through Microsoft 365 administration tools
- record the run ID, grant time, removal time, responsible administrator, and verification result outside the application

## Local destination security

Only local storage attached to the same Windows Server is permitted. UNC, mapped network drives, NAS, SMB, and remote destinations are outside scope.

Production acceptance requires:

- restricted NTFS ACLs for `OneDriveData`, `_TransferReport`, application files, and token-cache files
- no broad inherited read access for unrelated users
- BitLocker or an approved documented equivalent or exception for the backup volume
- no broad disabling of antivirus, EDR, firewall, or application-control protections

## Integrity and destination containment

- Calculate and persist local SHA-256 for every completed file.
- Revalidate local SHA-256 before trusting recovered completed state.
- Never call size and metadata validation source cryptographic verification.
- Revalidate destination containment before create, open, replace, and rename operations.
- Prevent symbolic-link, junction, mount-point, and reparse-point time-of-check/time-of-use redirection.

## Supply-chain security

Before Production Ready status:

- use deterministic dependency restore
- run dependency-vulnerability and secret scans
- generate an SBOM
- tie the publish output to an exact source commit
- use Authenticode signing when an approved organizational certificate is available
- document any approved unsigned-release limitation

## Reporting a security issue

Do not place sensitive details in a public issue. Use the repository owner's private communication channel and provide:

- a concise description
- affected component
- reproduction conditions
- potential impact
- redacted evidence

Do not include active credentials, employee data, tokens, private keys, or temporary download URLs.