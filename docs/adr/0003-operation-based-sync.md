# ADR-0003: Operation-based offline sync

- Status: Accepted
- Date: 2026-09-09

## Decision

کلاینت Command/Operationهای idempotent با Base Revision می‌فرستد. Replication مستقیم جدول‌ها و general last-write-wins استفاده نمی‌شود.

## Consequences

- سرور Permission، Workflow و Domain rule را در زمان Sync دوباره بررسی می‌کند.
- Conflictهای حساس به Review می‌روند.
- Approval، Decision، Financial Post و Access Management در V1 آنلاین می‌مانند.
