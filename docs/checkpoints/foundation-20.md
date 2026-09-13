# Foundation checkpoint 20 — System integrity and blueprint traceability

- Date: 2026-09-13
- Status: Repository hardening implemented; connected CI and remaining MVP/Pilot gates determine release readiness
- Audited remote baseline: `4715fc7fa44986daacee7839cb4b60a2a5f82445`
- Audited baseline tree: `bed37627148704caf3f1a2dbd37b4caf1cf567de`
- Product baseline: `PMCS_Blueprint_V1_21_FA.md`

## Scope delivered

- replaced the hard-coded demo landing with the actor-scoped project registry, project creation and controlled activation;
- added an audited, revision-controlled `Draft → Active` transition and blocked operational mutations outside active projects;
- validated real time-zone identifiers instead of accepting arbitrary text;
- added the module-owned hierarchical project Location/LBS registry with automatic `ROOT`, retirement rules and scoped database constraints;
- required an active Location for all new direct and offline daily facts and propagated its stable identifier through Project State and intelligence context;
- fixed the Field Operations EF mapping so `location_id` belongs to the Fact rather than the report;
- hardened idempotency expiry, concurrent storage, key bounds and retention cleanup;
- made core Sync handshake/checkpoint/conflict/device Audit writes atomic with their state changes;
- fixed client conflict resolution to use the server conflict revision and to recover idempotently after interruption;
- isolated in-flight sync/handshake promises by Tenant, User and Project;
- removed mutating advisory permissions from `PortfolioViewer` and split broad project configuration permissions;
- cleared stale local identity scope after authorization/session invalidation;
- strengthened production host, PostgreSQL certificate and object-storage transport validation;
- corrected the service-acceptance civil date to use the Tehran project day;
- added a repository-wide endpoint/mutation contract audit to CI;
- enabled explicit manual CI dispatch so the complete verification suite can be rerun independently of a push event.

## Verification available in this workspace

- 102 Web/API contract tests passed;
- ESLint, TypeScript and Next.js production build passed;
- Persian UI and Persian calendar audits passed;
- system contract audit passed for 202 endpoints and 163 mutation endpoints, with 5 documented protocol-managed Sync mutations;
- repository architecture validation passed for 241 C# module files;
- all 8 Pilot policy/release tests and shell syntax checks passed;
- `.NET`, Docker and PostgreSQL clients are not installed in this workspace, so backend compilation and PostgreSQL/MinIO runtime results are not claimed locally.

## Release interpretation

This checkpoint closes high-risk integrity defects; it does not redefine unfinished product scope as complete. The authoritative status of each approved MVP/V1 capability and every remaining blocker is recorded in `docs/audits/system-integrity-traceability-2026-09-13.md`.
