# QA Foundation Slice 3 — File and Attachment Verification

- Date: 2026-09-17
- Status: Implemented; connected verification pending
- Product state: `PMCS V1 — Feature Complete`
- Qualification state: In progress; not Qualified, Final or Locked
- Governing roadmap: [`../roadmaps/pmcs-v1-development-and-qualification.md`](../roadmaps/pmcs-v1-development-and-qualification.md)

## Review corrections

The File/Evidence review found three gaps in the existing whole-file V1 upload boundary:

1. an allowed declared MIME type was checked, but the binary signature was not matched to that type;
2. a successful private-object download did not create a dedicated Audit event;
3. the object was returned without rechecking its stored byte length, SHA-256 and content type against immutable metadata.

The endpoint now rejects disguised content before Object Storage, derives the private object extension from the verified content type, rejects missing storage receipts, verifies stored bytes before download and records the successful downloader, project, lineage, SHA-256 and Correlation ID.

## Independent qualification scope

- mandatory idempotency key and replay of upload-session and content commands;
- Observer upload denial and unrelated-project read denial;
- rejection of missing parent lineage, unsupported MIME, MIME mismatch, SHA-256 mismatch and disguised binary content;
- stable client-generated identity and conflict on reuse with a different upload intent;
- real PDF upload and private MinIO download with byte-for-byte/SHA-256 equality;
- verified `Content-Type`, attachment filename, `Cache-Control: no-store` and `X-Content-Type-Options: nosniff`;
- Observer metadata/list/download access and Finance Operator read denial under central permissions;
- direct PostgreSQL checks for immutable lineage, canonical object key, pending rejected content, Audit actor lineage, Outbox and Idempotency receipts;
- retained regression of the original PostgreSQL/API/MinIO object-storage roundtrip.

## Deliberate boundary

This Slice verifies and hardens the approved V1 whole-file attachment contract. Multipart/resumable transfer, malware scanner, quarantine, safe preview/thumbnail, retention/legal-hold policy and real production Object Storage lifecycle remain explicit Hardening/Pilot gates and are not represented as complete.

Independent Offline/Sync, UI/E2E, exploratory-agent and full-regression qualification remain open. PMCS V1 therefore remains Feature Complete and must not yet be described as Qualified, Final or Locked.

## Connected verification

Pending the connected CI run for this source revision.
