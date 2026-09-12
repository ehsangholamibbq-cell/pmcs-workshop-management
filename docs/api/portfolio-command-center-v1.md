# Portfolio Command Center API — V1

## Endpoint

| Method | Path | Permission | کاربرد |
| --- | --- | --- | --- |
| `GET` | `/api/v1/portfolio/command-center` | `portfolio.read` + scopeهای هر بعد | نمای مدیریتی تمام پروژه‌های مجاز |

در محیط Development، headerهای `X-Tenant-Id` و `X-User-Id` الزامی‌اند. پاسخ فقط پروژه‌هایی را دارد که کاربر در `project-state.read` می‌بیند. فیلدهای مالی، تجاری و اقدام برای هر پروژه براساس `financial-state.read`، `commercial-state.read` و `actions.read` مستقل محدود می‌شوند.

## Response contract

ریشه پاسخ:

- `contractVersion = portfolio-command-center-v1`
- `generatedAt`: زمان ساخت Read Model
- `header`: شمارش‌های شفاف و Exposureهای ارزی
- `projects`: وضعیت مستقل هر پروژه
- `actionExceptions`: حداکثر ۵۰ اقدام باز، با پروژه، مسئول، سررسید، اولویت و Revision

هر پروژه شامل Registry، مدیر پروژه، lifecycle، مدل قرارداد و حالت Planning است. بخش Operational وضعیت Snapshot، freshness، coverage، confidence، stale/outdated و سه دلیل اصلی را دارد. Financial و Commercial از Snapshotهای مستقل خود می‌آیند. Capabilityها وضعیت پیکربندی و وضعیت داده را جدا نگه می‌دارند.

## Empty, missing and optional semantics

| وضعیت | معنی |
| --- | --- |
| `NoData` | منبع رسمی کافی برای محاسبه وجود ندارد؛ وضعیت خوب نیست |
| `InsufficientData` | داده وجود دارد اما پوشش/تازگی برای ارزیابی کافی نیست |
| `NotConfigured` | قابلیت برای پروژه پیکربندی نشده است |
| `NotEnabled` | قابلیت عمداً فعال نیست |
| `SetupRequired` | قابلیت انتخاب شده ولی راه‌اندازی کامل نشده است |
| `Suspended` | قابلیت موقتاً تعلیق شده است |

WBS، بودجه اولیه و HSE می‌توانند وجود نداشته باشند. نبود هرکدام مانع نمایش پروژه یا وضعیت Operational نیست و به مقدار صفر/سبز تبدیل نمی‌شود.

## Currency rule

`header.currencyExposures` براساس currency code گروه‌بندی می‌شود. `recognizedSpend` و `externalNetCash` فقط از Financial State قابل دسترس و `Available`، و `totalCommittedAmount/openCommitmentAmount` فقط از Procurement State قابل دسترس و `Available` جمع می‌شوند. هیچ FX conversion یا جمع چندارزی وجود ندارد.

## Freshness and lineage

- `isOutdated` زمانی true است که Revision پیکربندی یا واقعیت Approved از Snapshot عملیاتی جدیدتر باشد.
- `needsCalculation` زمانی true است که واقعیت Approved وجود دارد ولی Snapshot عملیاتی هنوز ساخته نشده باشد.
- وضعیت‌های واقعی شناسه Snapshot، calculation version، as-of date و calculated-at خود را حمل می‌کنند.
- پاسخ Portfolio on-demand است؛ V1 تاریخچه مستقل Portfolio را ذخیره نمی‌کند.

## Scope limits

- Endpoint فقط Read است و command مدیریتی جدیدی ایجاد نمی‌کند.
- Filtering و sorting در Client روی مجموعه مجاز برگشتی انجام می‌شود.
- Portfolio V1 آنلاین است؛ پاسخ آن وارد Operation Queue کارگاه نمی‌شود.
- Approval count فقط Contract/Amendment/Purchase Requestهایی را می‌شمارد که در Commercial State نسخه فعلی تعریف شده‌اند؛ تعمیم به سایر Workflowها ادعا نمی‌شود.
