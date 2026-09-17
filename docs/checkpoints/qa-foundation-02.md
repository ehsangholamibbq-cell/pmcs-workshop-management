# QA Foundation Slice 2 — Permission, Workflow, Database and Audit Verification

- Date: 2026-09-17
- Status: Implemented; connected verification pending
- Product state: `PMCS V1 — Feature Complete`
- Qualification state: In progress; not Qualified, Final or Locked
- Governing roadmap: [`../roadmaps/pmcs-v1-development-and-qualification.md`](../roadmaps/pmcs-v1-development-and-qualification.md)

## Repository-wide review outcome

The review rechecked module dependencies, authentication and actor validation, endpoint contracts, idempotency, tenant/project scoping, workflow transitions, migration registration, reset isolation, Audit/Outbox lineage, Sync boundaries, Persian calendar/UI gates and the existing Checkpoint 22–24 evidence. Two concrete defects were corrected:

1. the historical measurement-link migration used `field_operations` while every other Field Operations migration used the canonical `field-operations` ledger identity; a forward-only normalization migration now upgrades existing databases without losing migration history;
2. QA Permission Preview Audit events were project-scoped in behavior but persisted a null `project_id`; the Audit entry now carries the verified project identity.

The repository validator now rejects inconsistent migration identities and any drift between application schemas and the external QA reset inventory. The contract audit also rejects duplicate HTTP method/route registrations.

## Qualification scope implemented

- 24 explicit allow/deny Permission assertions across all 12 deterministic QA actors;
- central `pmcs-rbac-v1` preview contract verification with real active accounts and memberships;
- negative API enforcement for Observer capture and Site Supervisor approval;
- a real Daily Report workflow executed as Site Supervisor → Technical Office → Observer read;
- optimistic revisions, stable project Location lineage and deterministic test identities;
- direct PostgreSQL verification of 38 migrations, canonical migration identity, seed cardinality, workflow state and actor lineage;
- direct Audit verification for Permission Preview project/correlation scope and all workflow transitions;
- direct Outbox, Idempotency and Notification evidence checks;
- machine-readable TestHarness result with per-assertion pass/fail details.

## Deliberate boundary

This Slice closes only Permission / Workflow / Database / Audit Verification. Independent File/Attachment, Offline/Sync, UI/E2E, exploratory-agent and full-regression qualification remain open. PMCS V1 therefore remains Feature Complete and must not yet be described as Qualified, Final or Locked.

## Connected verification

Pending the connected CI run for this source revision.
