# Foundation checkpoint 22 — Daily Report revision, My Work and notifications

- Date: 2026-09-16
- Status: Implemented and verified by connected CI
- Verified remote source commit: `9e09f04f12b908fbfaf0e2f8718ade05f2be91ab`
- Verified source tree: `390e0ed60ebcc584261a1e2042230243524ac38f`
- GitHub Actions evidence: [`35146249608`](https://github.com/ehsangholamibbq-cell/pmcs-workshop-management/actions/runs/35146249608)
- Governing roadmap: [`../roadmaps/pmcs-v1-development-and-qualification.md`](../roadmaps/pmcs-v1-development-and-qualification.md)
- Product baseline: `PMCS_Blueprint_V1_21_FA.md` plus the approved additive V1 development and qualification roadmap

## Scope delivered

- immutable Daily Report correction versions with root/version/predecessor/successor lineage;
- copied Fact lineage through fresh identities and `copiedFromFactId` without changing the approved predecessor;
- atomic approval of the replacement and transition of the predecessor to `Superseded`;
- editable correction Draft/Returned states for details, Fact removal/addition and resubmission;
- consolidated, permission-aware My Work query across report review/correction and assigned Management Actions;
- durable recipient-scoped in-app notifications with deduplication, read and acknowledgement receipts;
- transactional notification creation with the source mutation;
- independent `IWorkManagementQueryService` Application boundary suitable for future QA Gateway and Agent Tools;
- Persian/Shamsi rendering for every new user-facing date/time while retaining ISO/UTC API/database contracts.

## Tests added

- Daily Report domain tests for correction creation, mandatory reason, revision conflict, copied Fact lineage, predecessor immutability and bidirectional supersession;
- Notification domain tests for exact-recipient enforcement, read/acknowledge separation and optimistic revision;
- role-level permission tests for reviewer, supervisor and observer boundaries;
- Web/API contract tests for My Work, notification receipt and correction commands;
- stable-selector contract tests for future UI/E2E automation;
- PostgreSQL integration scenario covering Submit → Notification → Acknowledge → Approve → Correction → My Work → replacement approval → Supersede;
- direct database verification of workflow lineage, copied Facts, notification receipts, Audit correlation, Outbox and Idempotency evidence.

The connected scenario exposed and fixed a cross-feature defect in the offline duplicate-date lookup: after a correction version existed, Sync used `SingleOrDefault` across every version for that project date. It now selects only the lineage root (`SupersedesReportId == null`), and the repository contract guard prevents regression.

## Testability Hooks created

| Hook | Evidence |
| --- | --- |
| Audit verification | lineage, notification count/recipients and correlation fields in Audit plus `tools/checkpoint22-db-verification.sh` |
| Permission testing | central `IProjectPermissionService`, role grant tests and integration Permission Preview probe |
| Test IDs | stable `data-testid` and `data-entity-id` selectors on My Work, Notification and correction surfaces |
| Seed/Test Data | deterministic Tenant/Project/Report/Fact/Correction identities in the isolated integration scenario |
| Workflow state verification | direct PostgreSQL assertions for Approved/Superseded, version and predecessor/successor links |
| Notification verification | recipient-scoped API assertions plus direct read/acknowledgement persistence checks |

## Connected verification result

- all six GitHub Actions jobs passed: Architecture, Backend, Integration, Web, Pilot Contract and Identity Container;
- .NET 10 Release build passed with zero warnings;
- 223 C# tests passed with zero failures;
- 109 Web/API contract and testability tests passed;
- ESLint, Persian UI audit, Persian calendar audit, TypeScript and Next.js production build passed;
- repository validation passed for 256 C# module files;
- system contract audit passed for 212 endpoints, 169 mutations and 5 documented protocol-managed mutations;
- all 8 Pilot/Release tests and shell validation passed;
- PostgreSQL 17 migration/API workflow, MinIO binary roundtrip and Checkpoint 22 database verification passed;
- isolated PostgreSQL restore drill passed and retained the drill database with 35 migrations.

## Deliberate boundary

Checkpoint 22 does not claim full offline mutation for Actions/Issues, authenticated cold-start bootstrap, complete Sync recovery diagnostics, QA impersonation or the final QA Gateway. Those belong to Checkpoint 23 and the later independent QA Foundation. Closing this checkpoint does not make PMCS V1 Feature Complete, Qualified, Final or Locked.

The next approved step is Checkpoint 23: Offline / Sync / Recovery / Conflict Handling, including disconnect/reconnect, retry, duplicate prevention, conflict simulation, recovery, simultaneous-user scenarios, local/server verification and readable Sync diagnostics.
