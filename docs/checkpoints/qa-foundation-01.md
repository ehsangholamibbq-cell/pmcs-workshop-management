# QA Foundation Slice 1 — Seed, Diagnostics and fail-closed QA Gateway

- Date: 2026-09-17
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

## Candidate verification

- repository validation passed for 273 C# module files;
- system contract audit passed for 239 endpoints, 188 mutations and 5 documented protocol-managed mutations;
- 124 Web tests, ESLint, Persian UI audit, Persian calendar audit, TypeScript and Next.js production build passed;
- shell syntax and whitespace validation passed;
- connected .NET 10 build, C# tests and isolated PostgreSQL QA Harness run are required before this Slice is closed.
