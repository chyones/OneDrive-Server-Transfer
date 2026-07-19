# Implementation Contract Amendments — Superseded

## Status

`SUPERSEDED`

All approved corrections and product-scope decisions have been incorporated directly into `IMPLEMENTATION_CONTRACT.md`.

This file is retained only to preserve repository history. It is no longer a separate authority layer and must not be used to override the current binding contract.

## Current source of truth

Use this order:

1. Explicit current instruction from the repository owner.
2. `IMPLEMENTATION_CONTRACT.md`.
3. Approved entries in `.ai/DECISION_LOG.md` that do not conflict with the contract.
4. `AGENTS.md` and implementation-control documents.

## Incorporated corrections

The current contract now directly includes:

- repository root as project root
- simple one-window IT workflow
- read-only delegated Microsoft access
- employee OneDrive root validation
- local destination selected by the IT administrator
- destination binding to one tenant, employee, and drive
- Microsoft Graph v1.0 delta inventory and reconciliation
- streaming copy, partial files, resume, retry, and fixed concurrency of three
- local SHA-256 and supported source-hash separation
- local SQLite transfer state instead of a custom five-million-item JSONL index
- clear reporting and user-facing errors
- local-path containment and security requirements
- evidence-based completion states
- Windows Server 2019 and real-tenant production acceptance

## Explicitly removed as release blockers

The following former requirements are no longer binding for the first version:

- custom five-million-item benchmark
- custom disk-index engine
- absolute prohibition on embedded SQLite
- mandatory million-record report segmentation design before implementation
- ten-stage implementation structure
- supply-chain ceremony that blocks internal source development before functional validation

Security checks, dependency scanning, SBOM generation, and signing remain appropriate release controls where available, but they must not distort the simple product workflow.