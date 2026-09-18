# PMCS Shared Documents API V1

- Stage: `V1.1-DOC1 — Shared Document and Attachment Foundation`
- Base paths: `/api/v1/documents` and `/api/v1/upload-sessions`
- Storage: private S3-compatible object storage
- State: implementation candidate; qualification evidence pending

## Boundary

The Documents bounded context owns general product files used by project collaboration, reports,
technical workflows, member profiles and the configurable login experience. It does not replace or
mutate the specialized Evidence aggregate used by Daily Reports. A later module may reference only
a released document through `ISharedDocumentDirectory`; it may not read the Documents schema or
object-storage key directly.

Supported owner types:

| Owner type | Scope |
| --- | --- |
| `ProjectGeneral` | Project |
| `ProjectChat` | Project |
| `ReportOutput` | Project |
| `TechnicalDocument` | Project |
| `MemberProfile` | Tenant |
| `LoginExperience` | Tenant |

## State machine

`PendingUpload → Quarantined → Released`

`PendingUpload → Rejected`

- A client-generated asset id is stable across retry and cannot be reused with another upload intent.
- Version number is assigned by the server per tenant/owner pair.
- Binary content is never readable before `Released`.
- A clean scan does not auto-release the asset.
- `documents.release_quarantine` is a separate Tenant/Critical permission.
- Restricted assets require a tenant-wide read grant even when they are project-owned.

## Endpoints

| Method | Route | Permission | Result |
| --- | --- | --- | --- |
| GET | `/api/v1/documents` | `documents.read` | Permission-filtered metadata list |
| GET | `/api/v1/documents/{id}` | `documents.read` | One tenant-isolated asset |
| GET | `/api/v1/documents/{id}/content` | `documents.read` | Integrity-verified released bytes |
| POST | `/api/v1/upload-sessions` | `documents.upload` | Stable resumable session |
| PUT | `/api/v1/documents/{id}/content` | `documents.upload` | Hash/signature scan and quarantine |
| POST | `/api/v1/documents/{id}/release` | `documents.release_quarantine` | Critical manual release |
| PUT | `/api/v1/documents/{id}/classification` | `documents.classify` | Revision-controlled governance |

Every mutation requires `Idempotency-Key`. Release and classification also require the current
`baseRevision`. Tenant and actor identities come only from the authenticated server context.

## Content and storage controls

- Maximum size: 25 MiB.
- Allowed types: JPEG, PNG, WebP, HEIC/HEIF, PDF, UTF-8 text/CSV and Open XML DOCX/XLSX/PPTX.
- File extension and declared media type must agree.
- SHA-256, content signature and stored object size are verified.
- Open XML packages must contain the expected package roots.
- HTML, SVG, executable and arbitrary archive uploads are not accepted.
- Object keys are server-generated and never returned to clients.
- Downloads re-check size, hash, type and signature.

The current `IContentScanner` implementation is a deterministic fail-closed policy adapter used to
prove the scanner boundary and quarantine flow. It detects the standard test-malware marker and
signature/package mismatch. It is deliberately replaceable; production qualification for an
internet-facing deployment must bind this adapter to the approved malware engine without changing
the domain or API contract.

## Classification and retention

- Classification: `Internal`, `Confidential`, `Restricted`.
- Retention: `Standard` (minimum three years), `LongTerm` (minimum ten years), `Permanent`.
- Legal hold and active retention prevent deletion at the domain boundary.
- Classification, retention and legal-hold changes are revision controlled and audited.

## Offline retry

`document-upload-queue.ts` stores the binary and immutable upload intent in the actor-scoped
IndexedDB database. Interrupted `uploading` entries return to `queued` and reuse
`{assetId}:session` and `{assetId}:content`; attempt count never changes idempotency identity.
The server always re-authorizes the actor when the queue reconnects.

## Event and audit

The public integration contract is `documents.asset.released.v1`. Its outbox payload contains
identifiers, owner/version, state, classification and occurrence time only; it excludes binary,
filename, hash, storage key, token and secret. Upload, quarantine, rejection, governance, release
and download actions retain actor, project, correlation and revision evidence in the audit trail.
