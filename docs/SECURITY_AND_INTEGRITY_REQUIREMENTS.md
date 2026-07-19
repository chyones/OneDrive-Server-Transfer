# Security and Integrity Requirements

This document operationalizes the binding requirements in `IMPLEMENTATION_CONTRACT_AMENDMENTS.md`. It does not replace the contract.

## Local-data integrity

- Calculate and persist a streaming local SHA-256 for every completed file.
- Revalidate the current file against the recorded local SHA-256 before trusting a persisted `Completed` state.
- Never describe size and metadata validation as source cryptographic verification.
- Keep all hash operations bounded in memory.

## Destination containment

- Treat destination containment as a continuous invariant, not a startup-only check.
- Revalidate path ancestors and final resolved targets before file creation, replacement, and rename.
- Reject symbolic links, junctions, mount points, or reparse-point changes that redirect an operation outside its approved root.
- Add adversarial tests that replace a destination directory with a junction after initial validation.

## NTFS and storage baseline

Production validation must record that:

- the application and destination use an authorized Windows account
- `OneDriveData` and `_TransferReport` are not broadly readable through inherited ACLs
- application-owned token-cache files are not readable by normal unrelated users
- the backup volume uses BitLocker or an approved documented equivalent, unless an organizational exception is recorded
- no antivirus, EDR, firewall, or application-control protection was broadly disabled

## Microsoft access lifecycle

- Use a dedicated transfer administrator account where possible.
- Document the threat impact of delegated tenant-wide read permissions and persistent session access.
- Grant Site Collection Administrator access outside the application.
- Remove and verify removal of that access when the transfer no longer requires it.
- Keep the access grant and removal record outside the application as an administrative audit record.

## Protected audit metadata

Record sufficient identity and environment information to prove who ran the transfer, against which tenant and OneDrive, on which server, into which destination, using which application build.

Normal UI messages must remain simple. Internal identifiers belong in protected reports and logs.

## Software supply chain

Before production release:

- use deterministic dependency restore
- generate an SBOM
- run dependency vulnerability and secret scans
- associate the build with an exact source commit
- use Authenticode signing when an approved organizational certificate is available
- document verification and any approved unsigned-release exception
