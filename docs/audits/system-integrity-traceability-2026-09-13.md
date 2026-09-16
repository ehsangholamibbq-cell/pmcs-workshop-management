# PMCS system integrity and blueprint traceability audit

- Audit date: 2026-09-13
- Product authority: `PMCS_Blueprint_V1_21_FA.md`
- Audited GitHub baseline: `4715fc7fa44986daacee7839cb4b60a2a5f82445`
- Baseline tree: `bed37627148704caf3f1a2dbd37b4caf1cf567de`
- Audit checkpoint: Foundation 20

## Executive decision

The codebase has a coherent modular-monolith foundation and several substantial end-to-end vertical slices. It is not yet accurate to call the complete approved MVP Pilot scope finished or the deployment Pilot-ready.

Checkpoint 20 closes defects that could break a real project lifecycle, Location lineage, offline conflict resolution, idempotent retry or production transport policy. Remaining gaps are explicitly separated into product work, technical hardening and evidence that can only be produced in the target environment.

Status vocabulary:

- **Implemented** — the approved behavior exists end to end in repository code and has an automated gate;
- **Partial** — a usable slice exists, but at least one approved behavior is absent;
- **Open** — the approved capability is not implemented as a product slice;
- **External gate** — repository work alone cannot produce valid evidence.

## Audit method and coverage

The review covered:

- the approved MVP/V1 boundaries and acceptance criteria in Blueprint 1.21;
- all 14 backend modules, API host and shared building blocks;
- 205 registered API endpoints and all 164 non-GET mutations;
- module dependencies and cross-module persistence imports;
- aggregate transitions, revision control, permissions, audit/outbox/idempotency and tenant/project scoping;
- PostgreSQL migrations and important uniqueness/reference constraints;
- BFF/session isolation, IndexedDB identity scope, PWA/sync/conflict paths and evidence upload flow;
- all Web user-boundary date inputs/formatters and Persian UI gates;
- CI, release provenance, backup/restore and Pilot evidence policy.

Static inspection cannot replace compilation, database migration execution, browser/device UAT, load tests or security testing. Those limitations remain visible below.

## Blueprint-to-code matrix — MVP Pilot

| Approved capability | Status | Evidence in repository | Remaining work |
| --- | --- | --- | --- |
| Identity, organization and membership | Implemented core / Partial scope model | OIDC/BFF, tenant/user activation, invite lifecycle, membership commands, device/session control, explainable Effective Permission Preview | full Location/Contract/Party/Own/Assigned scope, explicit deny, Delegation and custom grants |
| Project Setup Quick Start | Implemented core / Partial module readiness | save/resume, complete core metadata, Shamsi Web dates, versioned setup, leadership/access checks, exact readiness preview and server-enforced activation | dedicated module readiness providers and initial party/organization assignment |
| Location/LBS base | Implemented for core Fact path | hierarchical module-owned Location, automatic ROOT, active-parent/child rules, stable ID in Fact and Project State | apply LBS identifiers to remaining modules that still use free-text Location; permission-scoped Location bootstrap |
| Module activation/status | Partial | independent capability modes and state semantics | versioned module readiness prerequisites and explicit Ready/Activate/Suspend workflow |
| Daily Site Operations | Partial | structured WorkProgress/Labor/Equipment/Material/Issue/Stoppage/SiteCondition/Note facts | richer project-day/weather/crew semantics and remaining blueprint reference dimensions |
| Daily Report workflow | Implemented core | Draft/Submit/Return/Approve, immutable correction Draft, copied Fact lineage, atomic replacement approval/Supersede, bidirectional version history, audit/outbox/idempotency | richer project-day semantics and domain UAT remain; Rejected is reserved and not presented as an active workflow |
| Progress without mandatory WBS | Implemented for current scope | measurement items, optional target, approved/provisional separation, versioned modes/baselines | broader UAT and import/integration remain outside MVP core |
| Issue and Action core | Partial | triage, assignment, transitions, complete/verify/close separation, governance escalation and consolidated assigned work | offline Issue/Action operations |
| Attachment/Evidence | Partial | private S3-compatible content, SHA-256/size checks, metadata lineage, local attachment queue | resumable multipart upload, malware scan/quarantine/safe preview and real object-store lifecycle |
| Audit and versioning | Implemented core / Partial platform | aggregate revisions, audit/outbox transactions for formal commands, release provenance | immutable retention policy, migration checksums and completion of atomic audit on every Sync rejection/conflict path |
| Offline PWA and Sync | Partial | operation log, lease/session, push/pull/checkpoint, Daily Fact handler, conflict center, crash retry, scoped local stores | authenticated cold-start app shell; full project/location/task bootstrap; offline Issue/Action; quality/HSE through the common operation protocol; seven-day device endurance |
| Permission Project/Module/Operation/Own/Assigned | Partial | operation permissions, project roles, own conflict filtering, assigned Action rules, explainable effective permission endpoint/UI | explicit deny, Delegation and full scoped grants |
| Finance Control Lite | Partial | receipt/payment/petty-cash funding/expense, submit/post/return, optional budget baseline, financial state | petty-cash request/advance/reconciliation as distinct workflows, basic payable/receivable aging and optional management-fee rule |
| Project State Lite | Implemented core | approved-fact-only deterministic calculation, coverage/freshness/confidence, no fake green/zero | broader dimensions and change summary against an official selected prior snapshot |
| Command Center Lite | Implemented core / Partial offline | source drill-down, top exceptions, actions, financial/commercial optional states, trend | cached authenticated Project Pulse/My Work for true cold start |
| AI Daily/Weekly Summary with citation | Partial | permission-aware structured context, provenance, citation validation, human review, safe failure | scheduled daily/weekly product experience and approved-model production evidence |
| Notification / in-app My Work | Implemented core | recipient-owned notification record, transactional dedup, read/acknowledge receipts and one permission-scoped My Work inbox across reports/actions | external channels, broader source adapters and target-environment UAT remain |

## Blueprint-to-code matrix — additional V1 scope

| V1 capability | Status | Main gap or qualification |
| --- | --- | --- |
| Risk, Decision and SLA/Escalation | Implemented substantial slice | external notification delivery, broader role/UAT and portfolio feed remain |
| Workflow template versioning | Open | workflows are code-defined rather than tenant/project versioned templates |
| Contracts and Technical Office | Implemented substantial slice | general attachment pipeline, change-impact path and environment concurrency/UAT remain |
| Procurement and Inventory Lite | Partial | request/order/receipt/inspection/ledger/custody/count/adjustment exist; RFQ/Quote/Evaluation/Award, order revision/expediting and serial traceability do not |
| Payment Certificates | Open | no dedicated measurement-to-certificate workflow |
| Quality Lite and independent HSE Lite | Implemented substantial slice / Partial offline | several records still use free-text Location; offline intake does not use the common Sync operation handler |
| Budget only when configured | Implemented | no budget is never converted to zero or false health |
| Planning modes None/Simple/Milestone/WBS/External | Implemented core | target-environment migration/concurrency and domain UAT remain |
| Official Project State snapshots | Implemented | retention/selection policy and broader dimension snapshots remain |
| Portfolio | Implemented core | richer feeds and permission scopes remain; PortfolioViewer is now read-only |
| Forecast/scenario | Open by design | no unsupported forecast is fabricated |
| Access Review, Retention and Security Center | Open | startup security controls exist, but governance product surfaces and legal hold/disposal do not |
| AI executive/anomaly/risk suggestion | Partial | advisory engine exists; richer executive/scenario features remain |
| Conflict Center and reconciliation | Partial | Daily Fact conflict resolution exists; multi-entity merge/remap/reconciliation remains |

## High-impact defects closed in Checkpoint 20

| Severity | Finding | Resolution |
| --- | --- | --- |
| Critical | Product root opened a hard-coded demo project | replaced with authenticated project registry and explicit selection |
| Critical | No controlled project activation or operational lifecycle gate | added audited/revision-controlled activation and active-project mutation guard |
| Critical | Daily Fact Location trusted free text and had no stable project relation | new facts require a same-project active Location ID; server snapshots canonical name |
| Critical | EF mapped `LocationId` to the wrong aggregate | corrected mapping to `DailyReportFact.location_id` and added repository guard |
| High | Conflict client resolved every server conflict with revision zero | fetches current conflict revision and supports idempotent crash recovery |
| High | In-flight sync/handshake promise could be shared across identity scopes | keys now include Tenant, User and Project |
| High | Expired idempotency rows blocked legitimate key reuse and had unbounded retention | expiry-aware lookup, atomic expired-row replacement, length validation, index and cleanup worker |
| High | Core Sync control state and Audit could commit independently | handshake, checkpoint, conflict resolution and device revocation use one transaction |
| High | Production allowed insufficient PostgreSQL/S3 transport validation | requires VerifyFull database TLS, HTTPS object storage and explicit hosts |
| High | PortfolioViewer could generate/review advisory records despite read-only role semantics | mutating insight permissions removed |
| Medium | Broad `projects.configure` permission mixed unrelated authority | split activation, calendar, planning, Location and conflict-management permissions |
| Medium | Session rejection could leave an in-memory pointer to prior local identity scope | pointer is cleared on 401/403 before redirect/error |
| Medium | Service acceptance used the UTC day instead of Tehran civil day | uses the shared project-time-zone date boundary; calendar gate rejects the old pattern |
| Medium | Invalid database references/duplicates surfaced as generic server failure | common API handler maps protected uniqueness and reference violations to 409/422 |

## Remaining engineering risks

These are not all release blockers, but must stay tracked:

1. Idempotency request hashes currently bind primarily to the serialized body and operation name, not uniformly to actor and canonical route. Random client keys make collision unlikely, but the next compatible idempotency schema should bind all three without invalidating live seven-day receipts.
2. PostgreSQL serializable/deadlock failures do not yet have a uniform bounded retry policy for every command.
3. Sync change-feed repair after a business commit relies on idempotent replay; the remaining rejection/conflict Audit paths are not all in the business aggregate transaction.
4. `GET /sync/pull` creates a short-lived checkpoint offer. Offer/session/feed retention and abuse limits need an explicit cleanup policy and load evidence.
5. Older schemas rely mainly on application-level Tenant/Project validation; composite scoped constraints are not yet uniform across every historical table.
6. Migration history records module/version/description but no SQL checksum. Applied migration drift must be prevented in a later foundation hardening step.
7. Effective Permission Preview reflects the current static role policy; explicit deny, Delegation and custom Location/Contract/Party/Own/Assigned grants are not yet available.
8. Legacy active projects correctly keep unknown activation and Location lineage as `null`; any backfill requires evidence and an approved migration process.

## Persian/Shamsi boundary result

The current Web code passes the Persian UI and calendar gates:

- all user date inputs use the shared Persian date picker;
- visible dates/times use the Persian calendar and Persian digits;
- Tehran civil-day conversion is used where a date is derived from an instant;
- API/database values remain `DateOnly`/ISO 8601/UTC and PostgreSQL `date`/`timestamptz`;
- official server number dates use the Persian calendar without renumbering historical records.

This result applies to implemented Web screens. PDF/Excel exports and future Notification channels must use the same formatter and need their own release tests when implemented. Cross-browser/device visual UAT remains an external gate.

## Automated evidence at audit close

Connected verification: GitHub Actions run [`34761372257`](https://github.com/ehsangholamibbq-cell/pmcs-workshop-management/actions/runs/34761372257) on commit `5dad38666befa71a2dd3cdc9ead60799c871c195` completed successfully across all six jobs.

| Gate | Local result |
| --- | --- |
| Web unit/API contract | 102 passed |
| ESLint | passed |
| Persian UI audit | passed |
| Persian calendar audit | passed |
| TypeScript and Next.js production build | passed |
| Endpoint/mutation contract audit | 202 endpoints, 163 mutations, 5 documented protocol exceptions; passed |
| Architecture validation | 241 C# module files; passed |
| Pilot policy tests | 8 passed |
| Shell syntax | passed |
| .NET build/domain tests | Release build passed; 211 passed, 0 failed |
| PostgreSQL 17 + MinIO + restore drill | API/object-storage roundtrip passed; isolated restore retained with 32 migrations |

## Release blockers and ownership

### Repository product work

1. Complete MVP offline bootstrap/cold start and Issue/Action operations.
2. Close the agreed Finance Lite gaps.
3. Add explicit deny, Delegation, custom scoped grants and dedicated module readiness providers.

### Repository hardening work

1. Route/actor-bound idempotency contract with backward-compatible versioning.
2. Uniform transient PostgreSQL retry and Sync retention/cleanup policy.
3. Multipart/resumable Evidence, malware scan/quarantine and safe preview integration.
4. Load, accessibility, offline chaos and tenant-isolation suites at target capacity.

### External Pilot evidence

1. Real domain, secrets, TLS/WAF, observability and approved identity configuration.
2. Real object storage versioning/encryption/replication and paired backup/restore evidence.
3. UAT with at least two users and two target devices, including account switch and device revocation.
4. Seven-day normal offline endurance on target workshop devices.
5. Product, Security and Operations sign-offs generated after all other evidence.

## Recommended implementation order

1. Complete offline bootstrap and Issue/Action/attachment recovery.
2. Finance Lite completion and cross-module Location adoption.
3. Explicit deny/scoped grants, module readiness providers and workflow templates.
4. CI hardening, target-environment UAT and Pilot evidence closure.

No item in the external-gate list may be replaced by a template, a local assertion or an unsigned report.

## Checkpoint 21 verification extension

Project Setup Readiness and Effective Permission Preview were implemented after the original Checkpoint 20 audit close. Connected GitHub Actions run [`34763386154`](https://github.com/ehsangholamibbq-cell/pmcs-workshop-management/actions/runs/34763386154) on commit `2cb5c44b6b66a48353eaed66047446bdb96d856f` completed successfully across all six jobs:

- .NET 10 Release build: passed with zero warnings;
- C# tests: 215 passed, 0 failed;
- Web/API contract tests: 104 passed;
- ESLint, Persian UI audit, Persian calendar audit, TypeScript and Next.js production build: passed;
- repository validation: 242 C# module files;
- endpoint/mutation audit: 205 endpoints, 164 mutations and 5 documented protocol-managed mutations;
- PostgreSQL 17/API and MinIO object roundtrip: passed;
- isolated restore drill: passed with 33 migrations retained for inspection;
- identity container and all 8 Pilot policy tests: passed.

## Checkpoint 22 verification extension

Daily Report revision lineage, permission-scoped My Work and durable in-app Notifications were implemented after Checkpoint 21. Connected GitHub Actions run [`35146249608`](https://github.com/ehsangholamibbq-cell/pmcs-workshop-management/actions/runs/35146249608) on source commit `9e09f04f12b908fbfaf0e2f8718ade05f2be91ab` completed successfully across all six jobs:

- .NET 10 Release build: passed with zero warnings;
- C# tests: 223 passed, 0 failed;
- Web/API/testability tests: 109 passed;
- ESLint, Persian UI audit, Persian calendar audit, TypeScript and Next.js production build: passed;
- repository validation: 256 C# module files;
- endpoint/mutation audit: 212 endpoints, 169 mutations and 5 documented protocol-managed mutations;
- PostgreSQL 17 correction workflow, My Work, Notification acknowledgement and direct Audit/Outbox/Idempotency database verification: passed;
- MinIO object roundtrip and isolated restore drill: passed with 35 migrations retained for inspection;
- Identity Container and all 8 Pilot/Release policy tests: passed.

The connected scenario also found and closed a lineage interaction defect in offline duplicate-date detection. Sync now resolves the root report for a project date instead of assuming the correction chain contains only one row, and a repository guard pins this rule.

Per the approved additive roadmap, the repository now advances to Checkpoint 23. PMCS V1 is not yet Feature Complete, Qualified, Final or Locked.
