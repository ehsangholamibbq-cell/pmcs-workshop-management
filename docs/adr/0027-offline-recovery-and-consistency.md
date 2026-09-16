# ADR 0027 — Offline recovery orchestration and local/server consistency

- Status: Accepted
- Date: 2026-09-16

## Context

The operation-based Sync protocol already provided scoped sessions, offline leases, idempotent operation identities, pull/checkpoint progression and explicit conflict resolution. It did not yet expose one durable client recovery lifecycle across operations, Quality/HSE intake and attachments, nor enough payload-free server evidence to distinguish a healthy reconnect from a retry loop, unresolved rejection or checkpoint divergence.

Checkpoint 23 must make disconnect, reconnect, retry, crash recovery, duplicate prevention, simultaneous-user conflict and local/server verification deterministic and directly testable without treating locally persisted data as official server state.

## Decision

### Durable recovery orchestration

- A project-scoped recovery coordinator owns the ordered lifecycle `recovering → pushing → uploading → verifying` and ends in `succeeded`, `attention`, `retry-scheduled` or `blocked`. The explicit `offline` state is persisted when no connection is available.
- Only one active recovery cycle may run for the same Tenant/User/Project. Reconnect, manual sync and scheduled retry converge on the same coordinator rather than starting competing queue drains.
- Interrupted operation and attachment states are returned to a retryable queue before sending. Operation and attachment queues drain in bounded batches and preserve their immutable operation/attachment identities across retries.
- Automatic retry is limited to transport failures, timeout/rate-limit responses, server failures and recoverable Sync-session/checkpoint errors. Validation, permission, lease and conflict outcomes require attention and are not hidden behind infinite retry.
- Backoff is deterministic and bounded at 5, 15, 45, 120 and 300 seconds. A valid `Retry-After` is honored between one second and fifteen minutes.

### Verification and diagnostics

- A cycle is successful only after queue processing and an explicit comparison of local checkpoint sequence, server device checkpoint, server watermark, pending operations/files/intakes, conflicts, rejections, device status and lease expiry.
- The local recovery record contains operational metadata and counters only. It is stored in the Tenant/User-isolated IndexedDB database and is readable by the UI and future QA tooling.
- The server persists one payload-free operation receipt per Tenant/User/Device/Operation. Its attempt count, replay count, terminal status, revision/conflict reference, correlation ID and timestamps support duplicate/retry diagnosis without copying business payload into the diagnostics store.
- Server diagnostics expose server time, checkpoint lag, recent rejection/replay counts, last operation time and a domain-calculated recovery state: `NeverSynchronized`, `Healthy`, `PendingPull`, `AttentionRequired`, `LeaseExpired` or `DeviceRevoked`.
- User-facing dates and retry times use the shared Persian calendar formatter. Protocol/database timestamps remain ISO 8601 UTC.

### Conflict and replay safety

- Operations are processed deterministically by local sequence and operation identity. A dependency may be satisfied by an applied operation in the current batch or by its durable prior receipt.
- Concurrent insertion of the same operation result or change-feed identity is handled through database uniqueness, then resolved as a replay; it must not create a second business Fact, Audit event or feed record.
- Simultaneous edits are never general last-write-wins. The losing intent becomes an explicit conflict containing both actor/device lineage and is resolved by a permitted human using the existing versioned conflict command.
- Attachment upload-session idempotency is bound to the stable attachment identity, not the retry attempt number.

## Consequences

Reconnect and crash recovery now have a durable, observable state machine suitable for direct UI/E2E and future QA Gateway verification. Support diagnostics can prove replay, rejection, checkpoint alignment and recovery health without exposing business content. PostgreSQL execution, two-user conflict, audit correlation and replay uniqueness remain mandatory connected-CI gates; browser/device endurance, authenticated cold-start and broader multi-entity offline commands remain later qualification work and are not claimed by this checkpoint.
