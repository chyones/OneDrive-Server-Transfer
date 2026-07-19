# OneDrive Server Transfer

A native Windows desktop application specification for creating a complete local backup of one employee's Microsoft 365 OneDrive to storage attached to the same Windows Server 2019 machine.

## Repository status

**Documentation Ready — Implementation Not Started**

This repository contains the approved implementation contract, binding corrections, execution plan, AI working memory, acceptance controls, and project-governance files. It intentionally contains no application code yet.

## Product summary

The IT administrator manually grants the designated Microsoft 365 administrator account Site Collection Administrator access to an employee's personal OneDrive. The application then:

1. Signs in through Microsoft.
2. Accepts the employee's OneDrive root URL.
3. Validates that the URL resolves to the employee's OneDrive for Business root.
4. Copies all in-scope active files and folders to local storage attached to the Windows Server.
5. Preserves resumable state and generates operational reports.

The application is read-only against Microsoft 365. It does not grant permissions, modify OneDrive data, support network destinations, or operate as a synchronization service.

## Binding source of truth

The binding requirements are, in precedence order:

1. [`IMPLEMENTATION_CONTRACT_AMENDMENTS.md`](IMPLEMENTATION_CONTRACT_AMENDMENTS.md)
2. [`IMPLEMENTATION_CONTRACT.md`](IMPLEMENTATION_CONTRACT.md)

The amendments correct repository structure, evidence handling, completion states, integrity verification, destination containment, access controls, audit requirements, manifest indexing, and milestone governance. Requirements not changed by the amendments remain fully binding.

## Start here for AI implementation

An implementation agent must read these files in order:

1. [`AGENTS.md`](AGENTS.md)
2. [`IMPLEMENTATION_CONTRACT_AMENDMENTS.md`](IMPLEMENTATION_CONTRACT_AMENDMENTS.md)
3. [`IMPLEMENTATION_CONTRACT.md`](IMPLEMENTATION_CONTRACT.md)
4. [`.ai/START_HERE.md`](.ai/START_HERE.md)
5. [`.ai/PROJECT_MEMORY.md`](.ai/PROJECT_MEMORY.md)
6. [`.ai/PHASE_STATUS.md`](.ai/PHASE_STATUS.md)
7. [`.ai/HANDOFF.md`](.ai/HANDOFF.md)
8. [`docs/IMPLEMENTATION_PLAN.md`](docs/IMPLEMENTATION_PLAN.md)
9. [`docs/ACCEPTANCE_MATRIX.md`](docs/ACCEPTANCE_MATRIX.md)
10. [`docs/EVIDENCE_POLICY.md`](docs/EVIDENCE_POLICY.md)
11. [`docs/SECURITY_AND_INTEGRITY_REQUIREMENTS.md`](docs/SECURITY_AND_INTEGRITY_REQUIREMENTS.md)

A ready-to-use instruction is available at [`.ai/AI_START_PROMPT.md`](.ai/AI_START_PROMPT.md).

## Required repository structure

The repository root is the project root. Do not create a nested project container.

```text
OneDrive-Server-Transfer/
├── .ai/                         AI context, memory, phase tracking, handoff
├── .github/                     Review and workflow controls
├── artifacts/
│   ├── evidence/                Small redacted evidence summaries committed to Git
│   ├── source/                  Generated source-validation output
│   └── win-x64/                 Generated Windows publish output
├── docs/                        Project documentation and execution controls
├── scripts/                     Build, validation, publish, and packaging scripts
├── src/                         Application source
├── tests/                       Automated tests and synthetic benchmark
├── AGENTS.md
├── IMPLEMENTATION_CONTRACT.md
├── IMPLEMENTATION_CONTRACT_AMENDMENTS.md
├── OneDriveServerTransfer.sln
├── appsettings.example.json
├── README.md
└── SECURITY.md
```

## Implementation milestones

- M0 — Repository and contract readiness
- M1 — Solution foundation and enforceable CI foundation
- M2 — Authentication and configuration
- M3 — OneDrive root resolution and validation
- M4 — Local destination, locking, containment, ACL, and path mapping
- M5 — Enumeration, disk-based manifest indexing, and reporting
- M6 — Transfer, resume, local SHA-256, and source integrity
- M7 — Reconciliation, cancellation, errors, and UI completion
- M8 — Automated tests, adversarial security tests, and production-pipeline benchmark
- M9 — Windows build, SBOM, signing decision, and self-contained publish
- M10 — Windows Server 2019 and real-tenant production acceptance

Every milestone must have its own committed redacted evidence summary. A checked PR-template box or an uncommitted local log is not evidence.

## Required values before production validation

Real tenant and server values are intentionally not stored in this repository. Complete [`docs/ENVIRONMENT_AND_INPUTS.md`](docs/ENVIRONMENT_AND_INPUTS.md) before production validation.

Never commit:

- Microsoft administrator passwords
- Access or refresh tokens
- Client secrets
- Private certificates or private keys
- Employee backup data
- Temporary Microsoft download URLs
- Production logs containing sensitive paths or identities

## Completion labels

- **Documentation Ready:** Contract and project-control files are prepared; application implementation has not started.
- **Source Implementation Complete:** Source and supported validation are complete, with committed evidence summaries.
- **Production Ready:** All mandatory Windows, security, supply-chain, and real-tenant acceptance steps have passed.
- **Not Complete:** Required implementation or validation remains unfinished.

The repository is currently **Documentation Ready** only.