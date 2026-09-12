# ADR 0012 — Production trust and operability boundary

- Status: Accepted
- Date: 2026-09-10

## Context

هسته V1 دارای Tenant/Permission، Audit، Offline Sync، Evidence و AI است، اما Header هویت توسعه، Health واحد و نبود Restore Drill برای استقرار واقعی کافی نیستند. از طرف دیگر انتخاب Identity Provider، زیرساخت TLS/WAF و سرویس Backup به محیط استقرار وابسته‌اند و نباید با secret یا فرض پنهان در کد ثابت شوند.

## Decision

- API در Production فقط OIDC/OAuth Access Token را با JWT Bearer و validation کامل signature/issuer/audience/lifetime می‌پذیرد. `sub` و `tenant_id` UUID مرز actor هستند؛ Role داخل Token جای Permission داخلی را نمی‌گیرد.
- Development header identity یک Authentication Scheme جداست، خارج Development هیچ هویتی تولید نمی‌کند و Bearer request را override نمی‌کند.
- Startup غیر Development در برابر dev flags/credentials، Authority یا Origin غیر HTTPS، wildcard host و auto-create bucket fail-fast است.
- OpenAPI فقط در Development منتشر می‌شود؛ API headerهای دفاعی، سقف body و rate limit سراسری per actor/IP دارد. تولید Insight علاوه بر آن policy محدودتر دارد.
- Liveness از Readiness جداست. PostgreSQL readiness dependency است؛ OpenAI چون optional/advisory است readiness را خراب نمی‌کند.
- telemetry درخواست فقط method، route template، status، duration و correlation id را ثبت می‌کند؛ raw path/query/body و payload هوش مصنوعی ثبت نمی‌شود. Metricها از Meter پایدار `Pmcs.Api` منتشر می‌شوند و exporter وظیفه محیط استقرار است.
- PostgreSQL backup checksumدار و restore drill غیر overwrite در Repository استاندارد می‌شود. Object Storage versioning/replication/retention و PITR در زیرساخت اجباری‌اند.

## Consequences

- API trust boundary از Provider مستقل می‌ماند، اما Production login تا انتخاب و اتصال IdP/BFF یک release gate باز است.
- rate limit داخل process دفاع پایه است و جای WAF، DDoS protection یا load test را نمی‌گیرد.
- `/health/ready` خرابی DB را نشان می‌دهد، ولی خرابی سرویس optional یا جزئی باید با telemetry/alert جدا دیده شود.
- داشتن Backup بدون Restore Drill و نسخه سازگار Object Storage معیار آمادگی محسوب نمی‌شود.
