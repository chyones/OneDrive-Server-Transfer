# OneDrive Server Transfer

A native Windows desktop application specification for creating a complete local backup of one employee's Microsoft 365 OneDrive to storage attached to the same Windows Server 2019 machine.

## Repository status

**Documentation Ready — Implementation Not Started**

This repository currently contains the approved implementation contract, execution plan, AI working memory, acceptance controls, and project-governance files. It intentionally contains no application code yet.

## Product summary

The IT administrator manually grants the designated Microsoft 365 administrator account Site Collection Administrator access to an employee's personal OneDrive. The application then:

1. Signs in through Microsoft.
2. Accepts the employee's OneDrive root URL.
3. Validates that the URL resolves to the employee's OneDrive for Business root.
4. Copies all in-scope active files and folders to local storage attached to the Windows Server.
5. Preserves resumable state and generates operational reports.

The application is read-only against Microsoft 365. It does not grant permissions, modify OneDrive data, support network destinations, or operate as a synchronization service.

## Source of truth

The binding requirements are in:

- [`IMPLEMENTATION_CONTRACT.md`](IMPLEMENTATION_CONTRACT.md)

When another document conflicts with the implementation contract, the implementation contract wins.

## Start here for AI implementation

An implementation agent must read these files in order:

1. [`AGENTS.md`](AGENTS.md)
2. [`IMPLEMENTATION_CONTRACT.md`](IMPLEMENTATION_CONTRACT.md)
3. [`.ai/START_HERE.md`](.ai/START_HERE.md)
4. [`.ai/PROJECT_MEMORY.md`](.ai/PROJECT_MEMORY.md)
5. [`.ai/PHASE_STATUS.md`](.ai/PHASE_STATUS.md)
6. [`docs/IMPLEMENTATION_PLAN.md`](docs/IMPLEMENTATION_PLAN.md)
7. [`docs/ACCEPTANCE_MATRIX.md`](docs/ACCEPTANCE_MATRIX.md)

A ready-to-use instruction is available at [`.ai/AI_START_PROMPT.md`](.ai/AI_START_PROMPT.md).

## Planned repository structure

```text
OneDrive-Server-Transfer/
├── .ai/                         AI context, memory, phase tracking, handoff
├── .github/                     Review controls
├── docs/                        Project documentation and execution plan
├── artifacts/                   Generated validation and publish evidence
├── src/                         Application source; created during implementation
├── tests/                       Automated tests; created during implementation
├── scripts/                     Build and publish scripts; created during implementation
├── AGENTS.md                    Mandatory rules for implementation agents
├── IMPLEMENTATION_CONTRACT.md   Binding implementation contract
├── README.md                    Repository entry point
└── SECURITY.md                  Security and secret-handling policy
```

## Implementation milestones

The implementation is organized into evidence-based phases:

- M0 — Repository and contract readiness
- M1 — Solution foundation
- M2 — Authentication and configuration
- M3 — OneDrive root resolution and validation
- M4 — Local destination, locking, and path mapping
- M5 — Enumeration, manifest, and reporting
- M6 — Transfer, resume, and integrity
- M7 — Reconciliation, cancellation, errors, and UI completion
- M8 — Automated tests and production-pipeline benchmark
- M9 — Windows build and self-contained publish
- M10 — Windows Server 2019 production acceptance

The agent may complete multiple phases in one execution, but every phase must have its own evidence and status update.

## Required values before production validation

The real tenant and server values are intentionally not stored in this repository. Complete the checklist in [`docs/ENVIRONMENT_AND_INPUTS.md`](docs/ENVIRONMENT_AND_INPUTS.md) before production validation.

Never commit:

- Microsoft administrator passwords
- Access or refresh tokens
- Client secrets
- Private certificates
- Employee backup data
- Temporary Microsoft download URLs
- Production logs containing sensitive paths or identities

## Completion labels

- **Documentation Ready:** Contract and project-control files are prepared.
- **Source Implementation Complete:** Source and supported non-Windows validation are complete.
- **Production Ready:** All mandatory Windows and real-tenant acceptance steps have passed.
- **Not Complete:** Required implementation or validation remains unfinished.

The repository is currently **Documentation Ready** only.
