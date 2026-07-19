# AI Handoff

## Current position

- Documentation baseline: complete.
- Application source: not started.
- Current phase: `M1 — Solution and CI foundation`.
- Status: `NOT_STARTED`.
- Start authorization: granted.

The exact evidence pointer is maintained only in `.ai/PHASE_STATUS.md`.

## M1 task

Before changing source files, mark M1 `IN_PROGRESS`.

Implement M1 only:

- create `OneDriveServerTransfer.sln` at repository root;
- create the WPF application project and automated-test project;
- target .NET 10 for Windows;
- establish MVVM, dependency injection, structured logging, and configuration;
- add SQLite dependency and schema foundation;
- establish interfaces for authentication, Graph metadata requests, temporary-host downloads, retry ownership, hashing, local storage, state, and reports;
- add deterministic dependency restore;
- add Windows CI for restore, Release build, tests, static analysis, vulnerability review, prohibited API/auth checks, and secret detection.

## M1 boundaries

Do not implement authentication, employee resolution, Graph inventory, Scan, file transfer, resume, reports, or production behavior during M1. Do not add fake successful services or future-feature placeholders.

Do not add Graph beta, application permissions, write permissions, ROPC, device-code flow, client secrets, certificates, scheduling, batch processing, service mode, remote destinations, dashboards, or email notifications.

## Completion

M1 is complete only when its exit criteria in `docs/IMPLEMENTATION_PLAN.md` pass, Windows CI validates the real solution, and committed evidence references the exact validated source commit.

Real tenant values and acceptance inputs remain external; see `docs/ENVIRONMENT_AND_INPUTS.md`.
