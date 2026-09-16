# Foundation checkpoint 23 — Offline, Sync, Recovery and Conflict Handling

- Date: 2026-09-16
- Status: Implemented and verified by connected CI
- Verified remote source commit: `fdcee701238f3cdefb3b0d44821b05f144f52db4`
- Verified source tree: `7aac5749f85951c5ce53491ba960dd8a93b034eb`
- GitHub Actions evidence: [`35150589920`](https://github.com/ehsangholamibbq-cell/pmcs-workshop-management/actions/runs/35150589920)
- Governing roadmap: [`../roadmaps/pmcs-v1-development-and-qualification.md`](../roadmaps/pmcs-v1-development-and-qualification.md)
- Product baseline: `PMCS_Blueprint_V1_21_FA.md` plus the approved additive V1 development and qualification roadmap

## Scope implemented

- durable project-scoped recovery state machine for offline, startup, reconnect, manual and scheduled-retry paths;
- single-flight recovery per Tenant/User/Project and bounded deterministic retry with `Retry-After` support;
- crash recovery for interrupted operation and attachment uploads before bounded queue draining;
- stable operation and attachment idempotency identities across retry;
- durable, payload-free server operation receipts with attempt/replay metadata and correlation identity;
- prior-batch dependency resolution from accepted receipts;
- concurrency-safe duplicate receipt and change-feed handling;
- explicit local/server verification across checkpoint, watermark, pending queues, conflicts, rejections, lease and device status;
- readable domain recovery state and UI diagnostics with stable E2E selectors;
- deterministic second user/device test identity and real simultaneous-edit conflict scenario;
- Persian/Shamsi display of all new user-facing times while retaining ISO/UTC protocol and persistence values.

## Tests added or extended

- domain tests for healthy, pending-pull, attention, expired-lease and revoked-device recovery states;
- Web unit tests for consistency classification, bounded retry, `Retry-After`, transient/non-transient failures and scheduled retry due time;
- API/testability guards for disconnect/reconnect wiring, durable recovery storage, payload-free receipts, stable selectors and CI verification hooks;
- Persian calendar roundtrip across Esfand/Farvardin for an offline Sync payload;
- attachment replay test that pins the idempotency key to the attachment instead of an attempt number;
- PostgreSQL integration replay of one identical operation and direct proof of one Fact, one Audit event and one change-feed entry;
- real two-user conflict detection/resolution with actor lineage and Audit correlation;
- direct database verification of receipt counts, replay counts, checkpoint alignment and absence of business payload in diagnostics.

## Testability hooks created

| Hook | Evidence |
| --- | --- |
| Disconnect/reconnect | explicit `offline`, `reconnect` and `retry` triggers plus stable recovery phases |
| Retry simulation | pure failure classifier, bounded delay function and persisted `nextRetryAt` |
| Crash recovery | explicit recovery of interrupted operation/attachment states before queue drain |
| Duplicate prevention | immutable IDs, durable receipt counters and direct one-row database assertions |
| Conflict simulation | deterministic second actor/device and version-mismatch integration scenario |
| Local/server comparison | pure consistency evaluator plus local checkpoint/server checkpoint/watermark diagnostics |
| Sync diagnostics | payload-free server receipt model, domain recovery health and readable UI panel |
| Audit verification | conflict detection/resolution and applied-operation correlation assertions |
| E2E readiness | stable `data-testid` and `data-sync-phase` selectors without bypassing authorization |

## Connected verification result

- all six GitHub Actions jobs passed: Architecture, Backend, Integration, Web, Pilot Contract and Identity Container;
- .NET 10 Release build passed with zero warnings;
- 231 C# tests passed with zero failures;
- 120 Web/API contract and testability tests passed;
- ESLint, Persian UI audit, Persian calendar audit, TypeScript and Next.js production build passed;
- repository validation passed for 257 C# module files;
- system contract audit passed for 212 endpoints, 169 mutations and 5 documented protocol-managed mutations;
- all 8 Pilot/Release policy tests and shell validation passed;
- PostgreSQL 17 migration/API flow and MinIO binary roundtrip passed;
- identical-operation Replay produced one Fact, one applied Audit and one Change Feed while the payload-free Receipt recorded two attempts and one replay;
- real second-user concurrent edit produced and resolved an explicit Conflict with both actors and correlated detection/resolution Audit events;
- device Checkpoint aligned with the server Watermark and readable recovery diagnostics returned `Healthy`;
- isolated PostgreSQL restore drill passed and retained the drill database with 36 migrations.

The first connected attempt correctly failed on four .NET performance-analyzer findings in the new structured logging/SQL wrapper. The implementation was corrected without suppressing analyzers; the verified run above then rebuilt and reran every job from zero.

## Deliberate boundary

Checkpoint 23 does not add general offline mutation for every PMCS aggregate, resumable multipart transfer, authenticated cold-start bootstrap, seven-day device endurance, QA impersonation or the independent QA Gateway. Those remain explicit qualification work. Closing this checkpoint also does not make PMCS V1 Feature Complete, Qualified, Final or Locked.

The next approved product step is Checkpoint 24: Finance Lite + Financial Integration + Location.
