# Project setup, activation and Location API — version 1

## Project registry

| Method | Path | Permission | Result |
| --- | --- | --- | --- |
| `GET` | `/api/v1/projects` | `projects.read` scope | Projects visible to the current actor |
| `GET` | `/api/v1/projects/{projectId}` | `projects.read` | One tenant-scoped project |
| `GET` | `/api/v1/projects/{projectId}/readiness` | `projects.read` | Exact activation checklist and blocking reasons |
| `POST` | `/api/v1/projects` | `projects.create` | A `Draft` project and its active `ROOT` Location |
| `POST` | `/api/v1/projects/{projectId}/activate` | `projects.activate` | Revision-controlled `Draft → Active` transition |
| `PUT` | `/api/v1/projects/{projectId}/setup` | `projects.setup.configure` | Save/resume a versioned setup configuration |
| `PUT` | `/api/v1/projects/{projectId}/calendar` | `projects.calendar.configure` | Optional working-week configuration |
| `PUT` | `/api/v1/projects/{projectId}/planning-mode` | `projects.planning.configure` | Optional planning mode configuration |

All commands require `Idempotency-Key`. Project create, activation and configuration write Aggregate, Audit, Outbox and the idempotency receipt in one PostgreSQL transaction.

Setup stores project type, execution phase, country/region, ISO `DateOnly` start and planned-finish dates, short description, IANA time zone, base currency, unit system, reporting cut-off/frequency, Daily Report workflow, offline-policy acceptance, planning/capability modes and working week. Dates remain ISO at the API/database boundary and are entered and displayed only through the shared Persian-calendar control in Web.

Every setup/calendar/planning change increments `configurationVersion` independently of the optimistic aggregate `revision`. A successful activation pins `activatedConfigurationVersion`. A setup change after activation requires both `projects.setup.configure-sensitive` and a non-empty reason; the reason and version are audited.

Activation requires all server-calculated readiness items to be non-blocking, including:

- the current project revision;
- `Draft` status;
- a non-empty activation actor supplied by the authenticated session;
- a configured base contract model;
- an active `ROOT` Location;
- complete project identity and valid main dates;
- at least one active Tenant Administrator, Project Manager and operational project user;
- explicit working week, IANA time zone and unit system;
- Daily Report cut-off, reporting frequency and workflow;
- accepted offline policy;
- no optional module marked `Active` before its module-specific readiness evidence exists.

`SetupRequired` and `NotEnabled` optional modules do not block core activation. WBS and an initial official contract record are not activation prerequisites. Selecting advanced planning or marking an optional module `Active` is conservative and blocks activation until a module-specific readiness provider is implemented; this avoids treating configuration intent as evidence.

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

Ordinary project mutation endpoints return `409 project.not_operational` while the project is not active. Reads, setup, activation, calendar, planning and Location configuration remain available for setup and diagnosis. Sync handshake and offline Fact capture apply the same invariant through their own server-side checks.

New Daily Report facts require `locationId`. The API resolves the active Location, ignores free-text Location as authority, and stores both the stable identifier and the canonical name snapshot.
