# QA Foundation Slice 1 — Seed, Diagnostics and fail-closed QA Gateway

- Date: 2026-09-17
- Status: Implemented and verified by connected CI
- Verified remote source commit: `49f2d18d40b5e6d4a670f36613099f03b0bf677e`
- GitHub Actions evidence: [`35216586670`](https://github.com/ehsangholamibbq-cell/pmcs-workshop-management/actions/runs/35216586670)
- Product state: `PMCS V1 — Feature Complete`
- Qualification state: In progress; not Qualified, Final or Locked
- Governing roadmap: [`../roadmaps/pmcs-v1-development-and-qualification.md`](../roadmaps/pmcs-v1-development-and-qualification.md)

## Scope implemented

- environment-gated QA Gateway under `/api/qa/v1`;
- mandatory `pmcs_qa_` database-name isolation;
- independent 32+ byte QA authentication key with fixed-time comparison;
- real Tenant/User actor validation through the existing Identity boundary;
- no Role claims or QA permission grants supplied by the authentication adapter;
- central `IProjectPermissionService` authorization and permission preview;
- Audit Trail and Correlation ID for every QA Gateway read;
- deterministic multi-role Test Data Set with stable identities;
- external, explicitly confirmed and schema-bounded reset script;
- TestHarness commands for database guard, seed manifest and diagnostics probe;
- health/diagnostics response without direct module-table access;
- no destructive or seed endpoint exposed through HTTP.

## Deliberate boundary

This Slice does not claim V1 Qualification. The independent permission/workflow/database/audit, attachment, offline/sync, UI/E2E, exploratory-agent and full-regression suites remain in the approved QA sequence.

## Connected verification

- repository validation passed for 273 C# module files;
- system contract audit passed for 239 endpoints, 188 mutations and 5 documented protocol-managed mutations;
- .NET 10 Release build passed with zero warnings and zero errors;
- 249 C# tests passed with zero failures;
- 124 Web tests, ESLint, Persian UI audit, Persian calendar audit, TypeScript and Next.js production build passed;
- shell syntax and whitespace validation passed;
- the isolated `pmcs_qa_integration` gate passed database guard, explicit reset, 37 migrations, multi-role seed, Test Authentication and Diagnostics;
- the original integration smoke, Checkpoint 22/23/24 direct database gates and isolated PostgreSQL restore drill all passed;
- Identity Container and Pilot Contract gates passed.

The first connected candidate correctly exposed an invalid `DbConnectionStringBuilder` enumeration assumption in the QA database guard. The parser was corrected without weakening the prefix rule or suppressing tests; Run 40 then rebuilt and reran every job from zero.

QA Foundation Slice 1 is closed. Full PMCS V1 Qualification remains active and the product must not yet be described as Qualified, Final or Locked.
