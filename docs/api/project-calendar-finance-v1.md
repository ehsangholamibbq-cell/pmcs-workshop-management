# Project Calendar and Finance Lite API — V1

## Project Calendar

| Method | Path | Permission | کاربرد |
| --- | --- | --- | --- |
| `PUT` | `/api/v1/projects/{projectId}/calendar` | `projects.configure` | تنظیم هفته کاری یا بازگرداندن تقویم به NotConfigured |

نمونه هفته کاری:

```json
{
  "baseRevision": 2,
  "mode": "WorkingWeek",
  "workingDays": ["Saturday", "Sunday", "Monday", "Tuesday", "Wednesday"]
}
```

برای نبود تقویم، `mode = NotConfigured` و `workingDays = null` ارسال می‌شود. Mutation به `Idempotency-Key` و Revision صحیح نیاز دارد.

## Financial records

| Method | Path | Permission | کاربرد |
| --- | --- | --- | --- |
| `GET` | `/api/v1/projects/{projectId}/finance/records` | `finance.records.read` | فهرست اسناد؛ فیلتر اختیاری status |
| `POST` | `/api/v1/projects/{projectId}/finance/records` | `finance.records.capture` | ایجاد Draft |
| `PUT` | `.../records/{recordId}` | `finance.records.capture` | اصلاح Draft یا Returned با Revision |
| `POST` | `.../records/{recordId}/submit` | `finance.records.submit` | ارسال برای کنترل |
| `POST` | `.../records/{recordId}/post` | `finance.records.review` | قطعی‌کردن و ساخت Snapshot مالی |
| `POST` | `.../records/{recordId}/return` | `finance.records.review` | عودت با دلیل الزامی |
| `GET` | `/api/v1/projects/{projectId}/finance/state` | `financial-state.read` | آخرین Financial State یا وضعیت NoData/NotConfigured محاسباتی |

نوع سند یکی از `Receipt`، `Payment`، `PettyCashFunding` یا `PettyCashExpense` است. `currencyCode` می‌تواند خالی باشد تا ارز پایه پروژه استفاده شود؛ ارز دیگر در Finance Lite رد می‌شود. `contractReference` و `costCenterCode` اختیاری‌اند. از Checkpoint 06، `contractId` و `commitmentId` نیز اختیاری‌اند و در صورت ارسال باید به Contract/Purchase Order واقعی در همان Tenant/Project اشاره کنند؛ نبود این شناسه‌ها مانع ثبت Finance پایه نیست.

## Optional Budget Baseline

| Method | Path | Permission | کاربرد |
| --- | --- | --- | --- |
| `GET` | `/api/v1/projects/{projectId}/finance/budget-baselines` | `budget.baselines.read` | تاریخچه Baselineها |
| `POST` | `/api/v1/projects/{projectId}/finance/budget-baselines` | `budget.baselines.capture` | ایجاد Draft اختیاری |
| `PUT` | `.../budget-baselines/{id}` | `budget.baselines.capture` | اصلاح Draft یا Returned با Revision |
| `POST` | `.../budget-baselines/{id}/submit` | `budget.baselines.submit` | ارسال برای کنترل |
| `POST` | `.../budget-baselines/{id}/approve` | `budget.baselines.review` | تأیید و Supersede نسخه Approved قبلی |
| `POST` | `.../budget-baselines/{id}/return` | `budget.baselines.review` | عودت با دلیل |

نبود Baseline جلوی ثبت و Post سند مالی را نمی‌گیرد. در این وضعیت `approvedBudgetAmount`، `budgetRemainingAmount` و `budgetConsumedPercent` مقدار `null` دارند؛ صفر مصنوعی تولید نمی‌شود.

## Financial State

- `status`: `NotConfigured`، `NoData` یا `Available`
- `dataQualityStatus`: `NoData`، `Adequate` یا `NeedsAttention`
- `budgetComparisonState`: `NotConfigured`، `SetupRequired`، `NoData`، `Available` یا `Suspended`
- `totalReceipts`: دریافت قطعی
- `directPayments`: پرداخت مستقیم قطعی
- `pettyCashFunding`: وجه تحویل‌شده به تنخواه
- `pettyCashExpenses`: هزینه مصرف‌شده از تنخواه
- `externalNetCash`: دریافت منهای پرداخت مستقیم و تأمین تنخواه
- `recognizedSpend`: پرداخت مستقیم به‌علاوه هزینه تنخواه، بدون دوباره‌شماری Funding
- `pettyCashBalance`: تأمین منهای هزینه تنخواه

Command Center فقط برای دارنده `financial-state.read` خلاصه Financial State را برمی‌گرداند و آن را در Operational Status ترکیب نمی‌کند.
