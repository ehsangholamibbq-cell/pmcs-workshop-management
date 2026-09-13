# Project setup, activation and Location API — version 1

## Project registry

| Method | Path | Permission | Result |
| --- | --- | --- | --- |
| `GET` | `/api/v1/projects` | `projects.read` scope | Projects visible to the current actor |
| `GET` | `/api/v1/projects/{projectId}` | `projects.read` | One tenant-scoped project |
| `POST` | `/api/v1/projects` | `projects.create` | A `Draft` project and its active `ROOT` Location |
| `POST` | `/api/v1/projects/{projectId}/activate` | `projects.activate` | Revision-controlled `Draft → Active` transition |
| `PUT` | `/api/v1/projects/{projectId}/calendar` | `projects.calendar.configure` | Optional working-week configuration |
| `PUT` | `/api/v1/projects/{projectId}/planning-mode` | `projects.planning.configure` | Optional planning mode configuration |

All commands require `Idempotency-Key`. Project create, activation and configuration write Aggregate, Audit, Outbox and the idempotency receipt in one PostgreSQL transaction.

Activation requires:

- the current project revision;
- `Draft` status;
- a non-empty activation actor supplied by the authenticated session;
- a configured base contract model;
- an active `ROOT` Location.

`activatedBy` and `activatedAt` are returned for newly activated projects. Legacy active projects retain `null` when the historical actor or instant is unknowable.

## Location registry

| Method | Path | Permission | Result |
| --- | --- | --- | --- |
| `GET` | `/api/v1/projects/{projectId}/locations` | `projects.read` | Active and retired project Locations |
| `POST` | `/api/v1/projects/{projectId}/locations` | `projects.locations.manage` | A new child of an active Location |
| `POST` | `/api/v1/projects/{projectId}/locations/{locationId}/retire` | `projects.locations.manage` | Revision-controlled retirement |

Example child creation:

```json
{
  "code": "FLOOR-03",
  "name": "طبقه سوم",
  "parentLocationId": "00000000-0000-0000-0000-000000000001"
}
```

Rules:

- `ROOT` is created by the server and is the only node without a parent.
- Codes are unique inside a Tenant/Project and normalized to uppercase.
- Parent scope and active state are checked by the server and protected again by composite PostgreSQL constraints.
- `ROOT` and a node with active children cannot be retired.
- Retirement never removes history.

## Operational boundary

Ordinary project mutation endpoints return `409 project.not_operational` while the project is not active. Reads remain available for setup and diagnosis. Sync handshake and offline Fact capture apply the same invariant through their own server-side checks.

New Daily Report facts require `locationId`. The API resolves the active Location, ignores free-text Location as authority, and stores both the stable identifier and the canonical name snapshot.

