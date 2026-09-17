# QA Foundation Slice 3 — File and Attachment Verification

- Date: 2026-09-17
- Status: Implemented and verified by connected CI
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

Verified source revision: [`9e305b9`](https://github.com/ehsangholamibbq-cell/pmcs-workshop-management/commit/9e305b901dcd3d1546ddc472bb2b686accaec250)

GitHub Actions evidence: [CI Run 44](https://github.com/ehsangholamibbq-cell/pmcs-workshop-management/actions/runs/35222371975), with all six jobs successful.

- Backend Release build and all `252/252` domain tests passed.
- Web validation passed with `124/124` tests, lint, Persian audits, TypeScript and production build.
- Repository validation passed across `276` C# module files; the contract audit confirmed `239` endpoints, `188` mutations and `5` documented protocol-managed mutations.
- QA permission/workflow verification passed `32/32` assertions.
- File/attachment verification passed `21/21` assertions against the real API, PostgreSQL and isolated MinIO, including disguised-content rejection, private-download byte/SHA integrity and download Audit evidence.
- Direct PostgreSQL file, attachment, object-storage and Audit verification passed.
- The original PostgreSQL/API/MinIO roundtrip, Checkpoints 22–24 evidence, OIDC container check, `8/8` pilot-contract tests and backup/restore drill with `38` migrations all remained green.

QA Foundation Slice 3 is closed. The next independent qualification slice is Offline/Sync; UI/E2E, exploratory-agent and full-regression/reporting remain open. PMCS V1 remains Feature Complete and is not yet Qualified, Final or Locked.
