# ADR 0025 — Project readiness gate and effective permission preview

- Status: Accepted
- Date: 2026-09-13

## Context

The project lifecycle could move from `Draft` to `Active` after checking only a contract-model choice and the automatic root Location. That did not satisfy the approved Quick Start checklist and could allow a project to enter operations without dates, project leadership, a working calendar, Daily Report rules or an accepted offline policy. Access administration also exposed assigned roles but not the resulting operation-level decisions.

## Decision

### Versioned setup and activation readiness

- The Project aggregate owns the setup facts and a monotonic `configurationVersion` distinct from its optimistic `revision`.
- Creation and `PUT /setup` accept incomplete draft data so an administrator can save and resume without fabricated defaults.
- Web captures user-facing dates through the shared Persian date picker. API and PostgreSQL persist `DateOnly`/ISO dates, `TimeOnly` cut-off and IANA time-zone identity.
- The server is the sole readiness authority. `GET /readiness` returns every item with `Passed`, `Warning` or `Blocked`, a stable code and an exact reason.
- Activation recalculates the same checklist in the command path and pins `activatedConfigurationVersion`; the browser cannot override readiness.
- WBS and an official contract record remain optional. A high-level contract model is required.
- Optional modules in `SetupRequired` or `NotEnabled` do not block the core start. An `Active` selection blocks until module-specific readiness evidence exists.
- Post-activation setup changes require the sensitive permission and a reason, and are audited with the new version.

### Effective permission preview

- A Tenant Administrator may calculate a diagnostic permission preview for one user and project, including a proposed supported role before saving it.
- Decisions use the same active Tenant, active Account, date-bounded Membership, Tenant Role and Project Role rules as enforcement.
- Every decision exposes operation, allow/deny, current or proposed-role source, scope, condition, deny reason, membership expiry, Policy version and delegation effect.
- Default deny is explicit. Preview does not grant access and is not accepted as authorization evidence by command endpoints.
- The current Policy has static role templates and one project membership role. Explicit deny, custom scoped grants and delegation are reported as not configured rather than inferred.

## Consequences

Draft completion is explainable, resumable and server-enforced; operational activation can no longer bypass missing leadership or reporting prerequisites. Administrators can see why access exists before relying on a role assignment. Advanced module readiness and full scoped Grant/Deny management remain visible future V1 work instead of being represented as complete.
