# My Work and in-app Notification API — V1

## Endpoints

| Method | Path | Scope | Result |
| --- | --- | --- | --- |
| `GET` | `/api/v1/projects/{projectId}/my-work` | active actor + `projects.read`, then each source permission | consolidated personal work |
| `GET` | `/api/v1/projects/{projectId}/notifications?unreadOnly=true` | exact Tenant/Project/recipient | up to 100 personal notifications |
| `POST` | `/api/v1/projects/{projectId}/notifications/{notificationId}/read` | exact recipient | records read receipt |
| `POST` | `/api/v1/projects/{projectId}/notifications/{notificationId}/acknowledge` | exact recipient | records read and explicit acknowledgement |

Both POST endpoints require `Idempotency-Key` and `baseRevision`. They commit the Notification revision, Audit event, Outbox event and Idempotency receipt in one PostgreSQL transaction.

## My Work composition

The response includes only work the current actor can see and own:

- non-terminal Management Actions assigned to the actor when `actions.read` is effective;
- Submitted Daily Reports when `field.daily-reports.read` and `field.daily-reports.review` are effective;
- Returned Daily Reports created by the actor when report read access is effective;
- active correction Drafts created for the original report owner.

Each item includes stable kind/target identity, status, priority, revision, change time and either due date or report date. `isOverdue` is calculated using the project's configured time zone; it is not calculated by the browser.

## Notification guarantees

- source commands write notifications through their existing database connection and transaction;
- uniqueness is `(tenantId, recipientUserId, deduplicationKey)`;
- a notification target is explicit (`DailyReport` or `ManagementAction`);
- list and receipt queries always filter Tenant, Project and recipient user;
- `readAt` and `acknowledgedAt` have different meanings and neither changes the source business record;
- notification timestamps are UTC at the API boundary and formatted as Persian/Shamsi dates and Tehran-local time in the Persian Web interface.

The current channel is in-app only. Email, SMS and push delivery are not claimed by this contract.

## Application and verification boundary

`IWorkManagementQueryService` is the permission-aware Application boundary for My Work and notification queries. HTTP endpoints do not own aggregation, priority, overdue or source-visibility rules. Future QA/Agent adapters must call this service instead of reading module tables directly.

The Persian UI exposes stable `data-testid` selectors and entity identities for E2E automation. The isolated integration scenario uses deterministic report/correction identities; `tools/checkpoint22-db-verification.sh` verifies persisted lineage, receipt, Audit, Outbox and Idempotency evidence directly in the test database. This verification path is CI/Staging-only and creates no production reset or bypass endpoint.
