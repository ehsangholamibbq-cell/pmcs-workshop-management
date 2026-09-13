# ADR 0024 — Project lifecycle, stable Location identity and integrity gates

- Status: Accepted
- Date: 2026-09-13

## Context

The approved product blueprint requires a real project selector, a controlled setup/activation boundary, a minimal Location Breakdown Structure (LBS), operation-based offline sync and traceable drill-down. The restored implementation still opened a hard-coded development project from the product landing page, did not expose a controlled activation command and accepted free-text Location values for new daily facts.

Those conditions made a technically valid module capable of operating outside an explicitly activated project and made Location-based reporting depend on mutable text.

## Decision

### Project lifecycle

- A newly created project is persisted as `Draft`.
- Project creation also creates exactly one active `ROOT` Location in the same aggregate transaction.
- Activation is a revision-controlled command requiring `projects.activate`, an idempotency key, a configured base contract model and an active `ROOT` Location.
- The activation actor and instant are persisted and included in Audit/Outbox evidence.
- Operational mutations under `/api/v1/projects/{projectId}/...` require an `Active` project. Setup commands for activation, calendar, planning mode and Location remain available while the project is being configured.
- Sync handshake and the module-owned offline command handler enforce the active-project invariant independently because their project identifier is not always a route value.

The current executable lifecycle is `Draft → Active`. `Configuring`, `Ready for Activation`, hold, closing, closed and archived transitions from the blueprint remain explicit future slices and are not inferred from enum values.

### Location ownership and lineage

- The Projects module owns `ProjectLocation` and its hierarchy.
- Only `ROOT` may have no parent; `ROOT` cannot have a parent or be retired.
- Every non-root Location requires an active parent in the same Tenant and Project.
- A Location with an active child cannot be retired.
- New direct and offline daily facts require an active Location identifier from the same Tenant/Project. Client-supplied Location text is not authoritative; the server snapshots the current canonical name.
- The stable Location identifier is propagated through approved-fact contracts, Project State attention snapshots, Command Center responses and permission-filtered intelligence context.
- Historical facts and snapshots created before this decision may keep `location_id = null`. No fabricated backfill is performed.

Cross-schema foreign keys are intentionally not introduced. Module boundaries are enforced through public directory contracts and server-side validation; each module stores the stable foreign identifier and a readable snapshot where needed.

### Integrity enforcement

- Idempotency keys have one bounded validation contract, seven-day expiry, expiry-indexed retention and atomic replacement only after expiry.
- Core Sync control mutations write their Audit event in the same PostgreSQL transaction as the state change.
- Production startup requires explicit hosts, PostgreSQL `SSL Mode=VerifyFull` and HTTPS object storage.
- Repository validation inventories endpoints and rejects unclassified mutations that lack idempotency or an explicit protocol-level replay contract.

## Consequences

Project selection no longer relies on demo identity, a draft project cannot accept ordinary business postings, and Location drill-down can use a stable identifier. Existing records remain honest instead of being assigned an invented Location. The repository is more fail-closed, but full Pilot readiness still depends on the remaining product slices and real environment/device evidence listed in the system integrity audit.

