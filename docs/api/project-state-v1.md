# Project State and Command Center API — V1 Lite

## Endpointها

| Method | Path | Permission | کاربرد |
| --- | --- | --- | --- |
| `GET` | `/api/v1/projects/{projectId}/command-center` | `project-state.read` | آخرین Snapshot، Capabilityها، Attention Itemها و ۱۴ نقطه روند |
| `POST` | `/api/v1/projects/{projectId}/project-state/recalculate` | `project-state.recalculate` | محاسبه و ذخیره Snapshot رسمی جدید |

POST به `Idempotency-Key` نیاز دارد. Body می‌تواند `{ "asOfDate": null }` باشد تا تاریخ محلی پروژه استفاده شود، یا یک تاریخ غیرآینده برای بازسازی تاریخی دریافت کند.

## منبع حقیقت

فقط Daily Reportهای `Approved` خوانده می‌شوند. Draft، Returned، Submitted، Operation آفلاینِ Pending و داده Rejected در Projection وارد نمی‌شوند.

## قواعد `project-state-v1`

Snapshotهای موجود با `calculationVersion = project-state-v1` immutable باقی می‌مانند. از Checkpoint 05، محاسبه جدید با `project-state-v2` انجام می‌شود؛ قرارداد پاسخ همچنان مقدار قدیمی `SevenCalendarDays` را برای خواندن تاریخچه می‌پذیرد.

## قواعد `project-state-v2`

| وضعیت تقویم پروژه | `coverageBasis` | مخرج Coverage در پنجره هفت‌روزه |
| --- | --- | --- |
| تقویم تنظیم نشده | `FallbackSevenCalendarDays` | هر ۷ روز تقویمی؛ fallback صریح و قابل مشاهده |
| هفته کاری تنظیم شده | `ConfiguredWorkingDays` | فقط روزهای کاری انتخاب‌شده در همان پنجره |

تغییر تقویم Revision پیکربندی پروژه را افزایش می‌دهد. اگر Revision آخرین Snapshot با پروژه برابر نباشد، Command Center مقدار `isOutdated = true` برمی‌گرداند. تقویم اختیاری است و نبود آن مانع محاسبه نمی‌شود.

## قواعد تاریخی `project-state-v1`

| محور | قاعده |
| --- | --- |
| Assessment Scope | `ApprovedDailyOperations` و `isPartial = true` |
| Coverage Basis | `SevenCalendarDays` |
| Coverage کافی | حداقل ۶۰٪ روزها؛ در پنجره ۷روزه یعنی حداقل ۵ روز دارای گزارش Approved |
| Freshness Current | آخرین گزارش Approved مربوط به امروز یا دیروز |
| Freshness Aging | ۲ تا ۳ روز از آخرین گزارش Approved |
| Freshness Stale | بیش از ۳ روز |
| Attention Window | Issue و Stoppageهای Approved در ۳۰ روز اخیر |
| Unassessed Impact | مقدار مستقل `Unassessed`؛ هرگز Low فرض نمی‌شود |

ترتیب Operational Status:

1. نبود سابقه Approved → `NoData`
2. Coverage ناکافی یا داده Stale → `InsufficientData`
3. مشاهده Critical → `Critical`
4. مشاهده High یا هر Stoppage → `AtRisk`
5. Issue یا Freshness Aging → `Watch`
6. در غیر این صورت → `Stable`

`Stable` در این قرارداد فقط به معنی «در داده عملیاتی Approved و در Scope فعلی استثنای شناخته‌شده وجود ندارد» است؛ به معنی سلامت مالی، برنامه‌ای، قراردادی یا HSE نیست.

## Capability isolation

Command Center برای هر قابلیت دو محور برمی‌گرداند:

- `configurationState`: `NotConfigured`، `NotEnabled`، `SetupRequired`، `Active` یا `Suspended`
- `metricState`: `NotApplicable`، `NoData` یا `Available`

Planning/WBS، Budget، Procurement و HSE غیرفعال یا بدون داده از Operational Assessment حذف می‌شوند. Financial State و Commercial State نیز به‌صورت بخش‌های مستقل و فقط برای دارنده Permission مربوط بازگردانده می‌شوند و در رنگ Operational Status ترکیب نمی‌شوند. بنابراین پروژه بدون WBS، بدون بودجه اولیه، بدون تقویم اختصاصی، بدون HSE یا بدون ماژول خرید همچنان Project State عملیاتی معتبر اما Partial دارد.

## Snapshot و Lineage

هر Snapshot شامل شناسه مستقل، `calculationVersion`، `asOfDate`، زمان محاسبه، `sourceMaxChangedAt`، شمارش Factها و Attention Itemهای دارای `sourceReportId/sourceFactId` است. اگر گزارش Approved جدیدتری ثبت شود، GET مقدار `isOutdated = true` می‌دهد تا مدیر بداند محاسبه مجدد لازم است.

Attention Item محاسباتی داخل Snapshot همچنان immutable و `NeedsTriage` است. Command Center تصمیم جاری را به‌صورت overlay مستقل با `NeedsTriage`، `ConvertedToAction` یا `Dismissed` برمی‌گرداند؛ بنابراین Action یا Dismissal تاریخچه Snapshot را بازنویسی نمی‌کند. Approval جدید نیز از Outbox به‌صورت خودکار Snapshot تازه می‌سازد و Recalculate دستی برای بازسازی یا کنترل مدیر باقی مانده است.
