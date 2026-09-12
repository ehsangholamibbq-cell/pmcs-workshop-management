# ADR-0002: Optional planning, budget, quality and HSE capabilities

- Status: Accepted
- Date: 2026-09-09

## Decision

WBS، Budget، Contract، Quality و HSE prerequisite هسته نیستند. هر Capability وضعیت مستقل `NotEnabled / SetupRequired / Active / Suspended` دارد.

## Consequences

- نبود WBS باعث تولید Planned Variance جعلی نمی‌شود.
- نبود Budget به معنی Over-budget نیست.
- HSE غیرفعال KPI یا امتیاز سبز ایجاد نمی‌کند.
- داده‌های قبلی هنگام فعال‌سازی بعدی فقط با Mapping نسخه‌دار متصل می‌شوند.
