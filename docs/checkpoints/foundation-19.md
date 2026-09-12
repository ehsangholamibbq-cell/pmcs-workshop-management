# Foundation checkpoint 19 — Pilot Release Gate and Artifact Provenance

- Date: 2026-09-12
- Status: Repository implementation complete; real environment and device sign-off remain blocked gates
- Baseline: Checkpoint 18 (`6dea26e`)

## Scope delivered

- Release identity مشترک شامل Commit کامل، نسخه و زمان Build در خود API و Web؛
- endpoint عمومی و بدون داده حساس `/api/v1/release` و فایل ثابت `/release.json`؛
- fail-fast Production برای Artifact فاقد provenance معتبر؛
- OCI label و Docker build arguments یکسان برای API/Web؛
- سیاست ماشین‌قابل‌بررسی هشت Gate با عمر مجاز مستقل؛
- Candidate schema بسته و رد فیلد ناشناخته؛
- تطبیق Commit تمام Evidenceها و Approvalها با Candidate؛
- الزام سه تأیید مستقل Product، Security و Operations پس از آخرین Evidence؛
- Hash اجباری برای API image، Web image، Source bundle و گزارش هر Gate؛
- Smoke پس از Deploy برای جلوگیری از ترکیب Backend جدید و Frontend قدیمی؛
- Integration CI شامل PostgreSQL 17، Roundtrip واقعی Evidence روی MinIO و Backup/Restore ایزوله؛
- ADR 0022 و Runbook انتشار Pilot.

## Verification completed in this workspace

- ۸ تست Pilot policy و deployed-artifact matching موفق؛
- ۹۰ تست Web/API contract موفق؛
- ESLint، ممیزی رابط فارسی، TypeScript و Next.js production build موفق؛
- Repository architecture validation روی ۲۳۰ فایل ماژول C# موفق؛
- syntax همه اسکریپت‌های Release/Integration/Backup موفق؛
- `dotnet`، Docker و `psql` در این نشست موجود نبودند؛ بنابراین نتیجه C#، PostgreSQL، MinIO یا Restore واقعی برای این محیط ادعا نمی‌شود و CI آن‌ها را اجباری اجرا می‌کند.

## Gates that require the target environment

- Domain نهایی، TLS/WAF، Secret Store و Observability واقعی؛
- Migration و constraint/restart recovery روی clone مجاز PostgreSQL Production؛
- Backup/Restore هم‌نسخه PostgreSQL و Object Storage واقعی؛
- Malware Scan/Quarantine و Multipart Resume؛
- UAT دو User/دو Device، revoke، shared-device account switch و endurance هفت‌روزه؛
- سه Sign-off واقعی Product/Security/Operations و Smoke روی Deployment مبتنی بر Digest.

هیچ‌یک از موارد بالا با تست محلی یا Template امضاشده تلقی نمی‌شود.
