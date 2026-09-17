# QA Foundation Slice 4 — Offline and Sync Verification

- Date: 2026-09-17
- Status: Implemented; connected verification pending
- Product state: `PMCS V1 — Feature Complete`
- Qualification state: In progress; not Qualified, Final or Locked
- Governing roadmap: [`../roadmaps/pmcs-v1-development-and-qualification.md`](../roadmaps/pmcs-v1-development-and-qualification.md)

## Review corrections

The independent Offline/Sync review found four integrity gaps in the existing Checkpoint 23 implementation:

1. the application idempotency key was scoped by Tenant/Device/Operation but omitted User, while the protocol identity is Tenant/User/Device/Operation;
2. reusing an accepted Operation ID with changed content could overwrite its durable Receipt status from `Applied` to `Rejected`;
3. the validated operation `correlationId` reached Change Feed, but applied/rejected Audit, Outbox and Receipt paths substituted the transient HTTP trace;
4. an invalid cross-project or overlong envelope was rejected logically but its raw scope/text was still used when persisting diagnostic Receipt/Audit evidence, allowing a database error or misleading project lineage.

The implementation now hashes Device within a bounded User-scoped idempotency identity, preserves an accepted Receipt on changed-payload reuse, carries the immutable operation correlation through every side effect, pins rejected diagnostics to the authorized Session project and bounds persisted diagnostic text. These are corrections inside the approved operation-based Sync architecture; no architectural boundary or business workflow changed.

## Independent qualification scope

- protocol compatibility failure and Observer handshake denial;
- first connection, reconnect and superseding Lease issuance;
- acceptance after reconnect of an offline operation captured under the previous valid Lease;
- Session actor isolation and mandatory Session header;
- exact retry replay without a second Fact, Audit, Outbox or Change Feed row;
- changed-payload Operation ID reuse rejection without downgrading the accepted Receipt;
- same Device ID and same Operation ID used independently by Site Supervisor and Technical Office;
- real two-user date conflict with owner visibility, non-manager isolation and Project Manager resolution;
- idempotent resolution replay and rejection of a contradictory second decision;
- project-wide Pull, apply-before-acknowledge contract, monotonic Checkpoint and acknowledgement replay;
- healthy server Diagnostics after Checkpoint/Watermark alignment;
- bounded rejection of overlong and cross-project envelopes without HTTP 500 or cross-project diagnostic pollution;
- device listing, self-revocation, Session/Lease closure, reconnect purge warning and `DeviceRevoked` diagnostics;
- direct PostgreSQL proof for actor/correlation lineage, payload-free receipts, independent idempotency identities, conflict resolution, checkpoint alignment and device recovery state.

## Deliberate boundary

This Slice independently qualifies the API/Application/Database Offline/Sync boundary and retains the existing deterministic Web recovery, queue, retry, attachment and Persian-date unit checks. Real browser network toggling, IndexedDB lifecycle across browser restarts, multi-tab behavior, authenticated cold start and responsive recovery UI remain in the approved UI/E2E Slice. Seven-day device endurance and broad offline mutation for every aggregate remain Hardening/Pilot gates.

UI/E2E, exploratory-agent and full-regression/reporting qualification remain open. PMCS V1 therefore remains Feature Complete and must not yet be described as Qualified, Final or Locked.

## Connected verification

Pending the connected CI run for this source revision.
