# Patching and Release Lifecycle

This document defines how the self-contained Windows application remains supported and secure after implementation.

Validated against official Microsoft lifecycle information on **2026-07-19**.

## Supported platform baseline

- Application framework: .NET 10 LTS.
- Primary operating system acceptance target: Windows Server 2019 x64 with Desktop Experience.
- Publish mode: self-contained `win-x64`.
- Microsoft Graph API: `v1.0`.
- Authentication: supported MSAL.NET release with WAM and system-browser fallback.

Current lifecycle reference points at the validation date:

- .NET 10 LTS support ends on 2028-11-14.
- Windows Server 2019 extended support ends on 2029-01-09.

Do not hard-code a specific .NET patch as permanently approved. Every release must use a currently supported .NET 10 servicing patch and record the exact version in evidence.

## Self-contained deployment rule

A self-contained deployment contains the .NET runtime used during publish. Installing a newer runtime on the server does not update the bundled runtime inside an already published application.

Therefore:

- security and reliability patches require a new restore, build, test, publish, and deployment cycle;
- release evidence must record the bundled runtime patch;
- old published folders must not be relabeled as current after a newer runtime patch is released; and
- production operators must be able to identify the application version, source commit, SDK, and runtime patch from protected version information.

## Release cadence

At minimum:

- review Microsoft .NET servicing releases monthly;
- review MSAL.NET and Microsoft Graph SDK releases monthly;
- review Windows Server security updates through the organization's normal server-patching process;
- review Microsoft Graph breaking-change and deprecation notices before each internal release;
- run an immediate review for a relevant critical or high-severity security advisory; and
- perform a formal platform-baseline review before M2, M3, M5, M7, and M8 completion.

A review does not require an upgrade when the new version is irrelevant or introduces unacceptable risk. The decision and evidence must be recorded.

## Dependency update process

For each direct dependency update:

1. Record current and proposed versions.
2. Review official release notes and security advisories.
3. Identify authentication, retry, serialization, HTTP, SQLite, logging, and WPF behavior changes.
4. Update deterministic restore files.
5. Run restore on compatible Windows.
6. Run formatting and static analysis.
7. Run the full automated test suite.
8. Run Windows Release build.
9. Run vulnerability and secret scans.
10. Run targeted real-environment checks when authentication or Graph behavior changed.
11. Update SBOM and release evidence.
12. Tie the release to an exact source commit.

Do not approve floating package versions or unreviewed transitive dependency changes.

## .NET SDK and runtime evidence

Each release evidence summary must record:

```text
DotnetSdkVersion
TargetFramework
RuntimeIdentifier
PublishSelfContained
BundledRuntimeVersion
WindowsBuildEnvironment
RestoreLockStatus
BuildConfiguration
SourceCommit
ArtifactHash
```

The publish process must use the latest approved supported .NET 10 patch available in the controlled build environment at release time.

## Microsoft Graph SDK and MSAL upgrades

An upgrade requires specific verification of:

- WAM behavior;
- system-browser fallback;
- token cache serialization and DPAPI protection;
- tenant and authorized-account validation;
- Graph base URL and `v1.0` use;
- middleware ordering;
- retry ownership and actual attempt count;
- `Retry-After` handling;
- request and response correlation IDs;
- unknown enum and JSON-property behavior;
- redirect handling;
- temporary-host credential isolation; and
- redaction of tokens and temporary URLs.

Do not upgrade the Graph SDK or MSAL solely because a newer version exists. Upgrade when supported, reviewed, and validated.

## Windows lifecycle

- Windows Server 2019 is the required version 1 production acceptance target.
- Keep the server current with organizationally approved Windows security updates.
- Do not disable antivirus, EDR, firewall, TLS validation, or application control to make the application work.
- Document proxy and TLS inspection requirements.
- Validate long-path support and required desktop components after major server changes.
- Add a secondary compatibility test on Windows Server 2022 or Windows Server 2025 when an environment is available.
- Secondary testing does not replace the required Server 2019 production acceptance evidence for version 1.

## End-of-support rules

The application must not be marked or remain approved as Production Ready when:

- the bundled .NET runtime is out of support;
- the primary deployed Windows version is out of support;
- a critical relevant vulnerability remains unremediated without a documented approved exception;
- required Graph or identity functionality depends on a deprecated or removed API; or
- the release cannot be tied to an exact source commit and artifact hash.

Before .NET 10 or Windows Server 2019 end of support, create an approved migration plan and complete compatibility testing on the next supported platform.

## Release package

The internal release package must include:

- self-contained `win-x64` application;
- configuration example without secrets;
- version and source-commit information;
- dependency lock metadata;
- SBOM where supported;
- artifact hashes;
- build, test, vulnerability, and secret-scan evidence;
- signing result or approved unsigned limitation;
- operating instructions;
- upgrade and rollback instructions; and
- known limitations and unsupported content.

## Rollback

- Preserve the previous approved release until the new release passes deployment validation.
- Application rollback must not silently downgrade or alter an existing destination SQLite schema.
- A release that introduces a SQLite migration must include compatibility and rollback rules.
- Do not open a database created by an unsupported future schema version.
- Preserve state-database backups required by the contract before migration.

## Acceptance checks

Before M8 and each later internal release:

- confirm the .NET 10 release is supported and current on servicing patches;
- confirm the target Windows release remains supported;
- verify exact SDK and runtime versions;
- perform deterministic restore;
- run Windows Release build and tests;
- publish self-contained `win-x64`;
- inspect publish output for the bundled runtime;
- run dependency-vulnerability and secret scans;
- generate or refresh the SBOM;
- calculate artifact hashes;
- verify WPF startup on the target server;
- verify authentication and Graph smoke tests when affected; and
- record all unexecuted checks and limitations honestly.

## Official references

- .NET support policy: https://dotnet.microsoft.com/platform/support/policy/dotnet-core
- Self-contained runtime patch selection: https://learn.microsoft.com/dotnet/core/deploying/runtime-patch-selection
- Windows Server 2019 lifecycle: https://learn.microsoft.com/lifecycle/products/windows-server-2019
