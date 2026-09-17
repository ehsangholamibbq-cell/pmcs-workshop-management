# QA Foundation Slice 2 — Permission, Workflow, Database and Audit Verification

- Date: 2026-09-17
- Status: Implemented and verified by connected CI
- Verified remote source commit: `ccd7905ebbae7862e798ba8734eb826309c1eb49`
- GitHub Actions evidence: [`35219479805`](https://github.com/ehsangholamibbq-cell/pmcs-workshop-management/actions/runs/35219479805)
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

- repository validation passed for 274 C# module files, including canonical migration identities and exact reset-schema coverage;
- system contract audit passed for 239 endpoints, 188 mutations and 5 documented protocol-managed mutations, with duplicate route detection active;
- .NET 10 Release build passed with zero warnings and zero errors;
- 249 C# tests passed with zero failures;
- 124 Web tests, ESLint, Persian UI audit, Persian calendar audit, TypeScript and Next.js production build passed;
- all 32 API assertions passed: 24 explicit Permission decisions plus 8 role-separated Daily Report workflow checks;
- the direct database gate passed for 38 migrations, 12 active seeded actors, canonical migration ledger identity, project-scoped Permission Audit, workflow state and actor lineage, Outbox, Idempotency and Notification evidence;
- the original integration smoke and Checkpoint 22/23/24 direct database gates remained green;
- the isolated PostgreSQL backup/restore drill passed with all 38 migrations;
- Identity Container and Pilot Contract gates passed.

The connected candidate passed on its first run without suppressing or weakening any gate.

QA Foundation Slice 2 is closed. Independent File/Attachment, Offline/Sync, UI/E2E, exploratory-agent and full-regression qualification remain active. PMCS V1 therefore remains Feature Complete and must not yet be described as Qualified, Final or Locked.
