# Foundation checkpoint 03 — Deterministic Project State and Command Center

- Date: 2026-09-09
- Status: Implemented and locally validated
- Product source: PMCS Product & System Blueprint V1.4

## Outcome

مسیر `Approved Fact → Deterministic Metric → Versioned Project State → Command Center` پیاده شد. سیستم اکنون می‌تواند بدون WBS، بدون Budget Baseline و بدون HSE یک ارزیابی عملیاتی رسمی اما صریحاً Partial بسازد؛ نبود داده نیز `NoData/InsufficientData` است و هیچ وضعیت سبز مصنوعی تولید نمی‌شود.

## Implemented

- ماژول مستقل `ProjectIntelligence` در Modular Monolith؛
- قرارداد خواندن بین‌ماژولی که فقط Daily Reportهای Approved را ارائه می‌کند؛
- Calculator خالص `project-state-v1` بدون I/O، randomness یا AI؛
- Coverage، Freshness و Confidence مستقل؛
- Coverage هفت‌روزه با basis ذخیره‌شده و Threshold قطعی؛
- Operational Statusهای NoData، InsufficientData، Stable، Watch، AtRisk و Critical؛
- پنجره ۳۰روزه Issue/Stoppage و Attention Item با `NeedsTriage`؛
- Impact ناموجود با وضعیت مستقل Unassessed؛
- Snapshot immutable، lineage تا Report/Fact و ۱۴ نقطه Trend؛
- تشخیص Snapshot قدیمی پس از Approved Fact جدید؛
- API خواندن Command Center و Recalculate دارای Permission/Idempotency؛
- transaction واحد Snapshot + Audit + Outbox + Idempotency؛
- نمایش Command Center واقعی، Capability isolation، Attention list و Cache آخرین Snapshot در PWA؛
- دسترسی خواندن Project State برای PortfolioViewer بدون مجوز Recalculate.

## Verification evidence

- .NET Release build: ۷ پروژه محصول + پروژه تست، ۰ warning و ۰ error؛
- Domain tests: ۱۹/۱۹ passed؛
- Frontend unit/API contract tests: ۱۳/۱۳ passed؛
- ESLint: passed؛
- TypeScript و Next.js production build: passed؛
- Production standalone smoke: صفحه اصلی، Manifest و Service Worker همگی HTTP 200؛
- Repository architecture validation: ۶۶ فایل C# ماژول بررسی و passed؛
- `git diff --check`: passed.

## Constraints still open

- اجرای Migration و Integration Test واقعی PostgreSQL در محیط فعلی ممکن نیست، چون Docker/PostgreSQL runtime نصب نیست.
- Recalculation فعلاً عملیات صریح مدیر است؛ Outbox consumer خودکار هنوز پیاده نشده است.
- Coverage V1 از هفت روز تقویمی استفاده می‌کند؛ Project Calendar نسخه بعد basis دقیق روزهای کاری را فراهم می‌کند.
- Attention Item فعلاً NeedsTriage است و Assignment/Action/Resolution workflow ندارد.
- Photo metadata، S3 upload session و attachment sync هنوز پیاده نشده‌اند.
- Project State فعلی فقط Approved Daily Operations را پوشش می‌دهد؛ Finance، Planning، Contract، Quality و HSE Metricها هنوز وارد Composite State نشده‌اند.
- Portfolio Dashboard چندپروژه‌ای و AI Summary هنوز پیاده نشده‌اند.

## Next checkpoint

Vertical Slice «Evidence & Action Control»:

1. Photo/File metadata و upload session برای S3-compatible storage؛
2. صف آفلاین attachment با upload retry مستقل؛
3. Action aggregate با Assignee، Due Date، Priority و Status؛
4. تبدیل NeedsTriage به Action یا Dismissal با Audit؛
5. Outbox consumer برای Refresh خودکار Project State؛
6. Project Calendar اختیاری و Coverage basis نسخه دوم.
