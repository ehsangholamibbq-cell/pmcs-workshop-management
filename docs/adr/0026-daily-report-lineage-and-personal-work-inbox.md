# ADR 0026 — Daily Report correction lineage and personal work inbox

- Status: Accepted
- Date: 2026-09-13

## Context

An approved Daily Report was immutable but could not be corrected without either rewriting history or leaving an acknowledged error in the official fact source. Review work, assigned actions and escalation records were also displayed in separate product panels. No durable in-app notification record existed, so a user could not distinguish delivery, reading and explicit acknowledgement.

## Decision

### Daily Report corrections

- An approved report is never edited in place. Starting a correction creates a new Draft with the same `rootReportId`, the next `versionNumber` and an explicit `supersedesReportId`.
- A mandatory correction reason and initiating actor are recorded. Every copied Fact receives a new identity and `copiedFromFactId`; removing a copied Fact changes only the correction Draft.
- The prior Approved version remains the official source while the correction is Draft, Returned or Submitted. It changes to `Superseded` only inside the same database transaction that approves the replacement.
- The predecessor then records `supersededByReportId` and `supersededAt`. The replacement and predecessor therefore form a bidirectional, non-destructive chain.
- Only the current Approved version can start one active correction. Optimistic revision, idempotency, Audit and Outbox remain mandatory for all commands.
- Project State continues to consume only `Approved` reports. Approval of a correction emits the existing approval event and recalculates from the newly official facts without a period in which no official report exists.

### My Work and notifications

- `My Work` is a server-calculated, permission-scoped read model. It combines assigned non-terminal Management Actions, submitted reports visible to a reviewer, returned reports owned by the actor and active correction Drafts owned by the actor.
- Composition, permission evaluation, overdue calculation and ordering live in the independent `IWorkManagementQueryService` Application boundary rather than UI or HTTP endpoints. The same boundary can later be invoked by QA Gateway and permission-aware Agent Tools.
- Source permissions are evaluated independently. Access to the project does not fabricate access to Action or Daily Report work.
- In-app notifications are durable recipient-owned records with a unique deduplication key, target lineage, occurrence time, read time, acknowledgement time and optimistic revision.
- Notification creation for Daily Report submission/return/approval/correction and Action assignment occurs in the same PostgreSQL transaction as the source mutation. Retry cannot create duplicate notifications and a committed source command cannot silently lose its notification.
- Listing and receipt changes are always restricted by Tenant, Project and exact recipient. Reading and acknowledgement are separate idempotent audited commands.
- User-visible dates and times in My Work, notification and lineage surfaces use the shared Persian calendar formatter; API and database contracts remain ISO `date` and UTC `timestamptz`.
- Stable UI test selectors and deterministic integration identities are contractually maintained for E2E and database verification; they do not weaken production authorization.

## Consequences

Official Daily Report facts can be corrected without overwriting evidence or prematurely invalidating the last approved version. Users receive one operational inbox with explicit ownership and overdue state, while notifications provide durable delivery and acknowledgement evidence. Offline mutation of Issue/Action and full authenticated cold-start bootstrap remain a separate checkpoint; cached display does not turn offline data into an official receipt.
