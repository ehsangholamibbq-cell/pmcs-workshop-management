# ADR-0001: Modular Monolith for V1

- Status: Accepted
- Date: 2026-09-09

## Context

PMCS چند دامنه جدی دارد، اما تیم و الگوی بار V1 هنوز نیاز به هزینه عملیاتی Microservices را اثبات نکرده‌اند.

## Decision

یک Backend deployable با Moduleهای مستقل، schemaهای PostgreSQL جدا، قراردادهای Application/Event صریح و architecture tests ساخته می‌شود.

## Consequences

- Transaction و deployment ساده‌تر است.
- مرز ماژول باید در کد و تست enforce شود.
- جداسازی احتمالی سرویس‌ها در آینده از contractها شروع می‌شود، نه از database sharing.
