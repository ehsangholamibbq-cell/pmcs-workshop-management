# PMCS V1.1 — Test Strategy و Qualification Contract

- شناسه: `PMCS-QA-V1.1-001`
- نسخه: `1.0.0`
- وضعیت: Governance Contract
- Parent Qualification: Run 69 / source `26bf222d44634562ca7f3fc0931f3f8b79ca04a1`

## ۱. اصل Evidence

سبز بودن Build به‌تنهایی Qualification نیست. تمام Suiteها باید روی یک Commit کامل و ثابت اجرا شوند، Artifact وابسته به همان SHA بسازند و گزارش تجمیعی Fail-closed تولید کنند. Missing، duplicate، mixed-commit، cancelled، skipped یا failed evidence نسخه را Qualified نمی‌کند.

## ۲. لایه‌های اجباری

| لایه | پوشش |
| --- | --- |
| Governance/Architecture | Baseline identity، required docs، manifest، dependency و no-cross-persistence |
| Domain/Unit | Business rules و state transitions هر Context |
| API/Contract | schema، status/error، idempotency، version compatibility |
| Database/Migration | upgrade از V1، constraints، concurrency و ledger |
| Permission/Security | Allow/Deny، tenant/project/self، suspended/revoked و abuse cases |
| Integration | PostgreSQL، MinIO، Keycloak، Outbox، background jobs و reconnect |
| Web | unit، lint، TypeScript، Persian/RTL/Jalali و production build |
| UI/E2E | مرورگر واقعی، responsive، keyboard، offline و failure states |
| Visual/Print | screenshot regression، reduced-motion، A4/A3/PDF golden |
| Recovery | backup/restore، retry، partial failure و rollback rehearsal |
| Full Regression | تمام Contractهای PMCS V1 بدون کاهش پوشش |

## ۳. Suiteهای اختصاصی V1.1

### Extensibility

- manifest schema و duplicate/dependency cycle؛
- navigation visibility در برابر API permission؛
- event/tool compatibility و unknown version fail-closed؛
- Reference Module end-to-end.

### Login و Profile

- Preview/Publish/Rollback و cache invalidation؛
- Asset invalid/missing/quarantined و Fallback قابل استفاده؛
- CSP/XSS/remote-script negative tests؛
- image type/size/signature، crop/thumbnail و delete/replace؛
- self/admin field permission و cross-tenant privacy؛
- reduced-motion و فرم قابل تعامل مستقل از Asset.

### Project Bootstrap

- Preview/Execute digest parity؛
- allowlist و assertion عدم کپی تمام داده‌های عملیاتی؛
- member active/suspended/revoked و privilege escalation؛
- cross-tenant/cross-project denial؛
- retry/idempotency و partial failure؛
- destination Draft و Activation مستقل؛
- Audit و نتیجه Added/Skipped/Conflict/Blocked.

### Documents/Collaboration/Reporting

- upload/download/retry/hash/signature/quarantine/retention؛
- realtime ordering، reconnect، duplicate، moderation و conversion lineage؛
- report determinism، as-of، template version، hash، RTL/Jalali و PDF/Excel integrity.

### Intelligence Stage 1

- provider swap contract و structured output؛
- Tool permission در هر call؛
- no SQL/DB، no privilege escalation و cross-project denial؛
- timeout/cancel/safe failure و audit lineage؛
- عدم وجود write-domain tool.

## ۴. Migration و Rollback matrix

حداقل سناریوها:

1. دیتابیس خالی → تمام Migrationهای V1 + V1.1؛
2. Backup نماینده Commit `26bf222d...` → Upgrade V1.1؛
3. Upgrade → اجرای Smoke و Permission matrix؛
4. Runtime rollback با Schema expanded؛
5. Backup/Restore مستقل روی Candidate؛
6. تکرار migration runner بدون تغییر دوباره؛
7. قطع عملیات در میانه و recovery کنترل‌شده.

## ۵. Visual و Performance

- Login form نباید منتظر بارگیری تصویر یا Animation بماند؛
- animation یک‌باره/کوتاه و reduced-motion خاموش باشد؛
- Asset budget و Core Web Vital threshold در `VX-G3` عددگذاری و سپس قفل می‌شود؛
- تمام مسیرهای اصلی در Desktop/Tablet/Mobile و RTL تست می‌شوند؛
- حالت‌های Empty/Loading/Error/Offline/NoPermission/NotConfigured visual baseline دارند؛
- چاپ continuation Design System است و screenshot صفحه پذیرفته نیست.

## ۶. Gateهای نسخه

| Gate | Evidence حداقل |
| --- | --- |
| `G0 Governance Approved` | contract test + CI سبز + exact baseline manifest |
| `VX-G3 System Ready` | token/component/prototype/a11y contract |
| Feature Checkpoint | suite مالک + architecture + affected regression |
| `Feature Complete` | Scope کامل و deferred صریح؛ بدون known blocker |
| `Release Candidate` | freeze و migration rehearsal |
| `Qualified` | تمام Suiteها و Full Regression یک SHA |
| `Final/Locked` | report، artifact digest، migration ledger و evidence commit |

## ۷. ممنوعیت کاهش پوشش

تست V1 فقط به‌دلیل زمان اجرای زیاد حذف یا Skip نمی‌شود. تغییر count باید با توضیح semantic coverage ثبت شود. Retry CI برای flaky test جای اصلاح علت را نمی‌گیرد و flaky suite مانع Qualification است.

