# Foundation checkpoint 21 — Project setup readiness and effective permission preview

- Date: 2026-09-13
- Status: Implemented in repository; connected CI evidence pending
- Product baseline: `PMCS_Blueprint_V1_21_FA.md`

## Scope delivered

- expanded Project Setup with project type, execution phase, region, main dates, short description, units, report cut-off/frequency/workflow and offline-policy acceptance;
- kept all Web date input/output on the shared Persian/Shamsi boundary while preserving ISO `DateOnly` and UTC/backend contracts;
- added save/resume for Draft setup with optimistic revision, idempotency, audit/outbox and monotonic configuration version;
- pinned the exact configuration version used at activation;
- added a server-calculated readiness endpoint with stable item codes, completion percentage and exact blocking/warning reasons;
- made activation recalculate the full checklist and reject incomplete projects with `422 project.activate.readiness_failed`;
- required active organization administration, project leadership and operational membership without requiring WBS or an official initial contract record;
- treated `SetupRequired` optional modules as non-blocking and unverified `Active` modules as blocking;
- added a diagnostic Effective Permission Preview using the same active-account, Tenant Role, Project Membership and role-template rules as enforcement;
- exposed allow/deny, grant source, scope, condition, reason, expiry, Policy version and Delegation status in the Persian administration UI;
- added migration `projects:20260913-006`, ADR 0025 and integration-smoke coverage for leadership, readiness, preview and activation.

## Verification in this workspace

- 104 Web/API contract tests passed;
- ESLint, Persian UI audit and Persian calendar audit passed;
- clean Next.js production build passed after discarding a corrupt generated Turbopack cache;
- repository validation passed for 242 C# module files;
- system contract audit passed for 205 endpoints and 164 mutations, with 5 documented protocol-managed mutations;
- all 8 Pilot policy/release tests and shell syntax checks passed;
- `.NET`, Docker and PostgreSQL clients are not installed locally; backend compilation,  PostgreSQL migration, MinIO roundtrip and restore results are delegated to connected CI and are not claimed yet.

## Deliberate boundary

The effective preview explains the current fixed Tenant/Project role Policy. Explicit deny, Delegation and custom Location/Contract/Party/Own/Assigned grants remain V1 work and are never simulated. Project module modes do not yet own independent readiness-provider records; selecting an optional module as `Active` therefore blocks activation instead of producing false readiness.
