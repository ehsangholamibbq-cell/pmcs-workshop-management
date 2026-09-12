# Contract and Procurement Control API — V1 Lite

تمام مسیرهای پروژه زیر پیشوند `/api/v1/projects/{projectId}/commercial` دارند. Commandها به `Idempotency-Key` و commandهای تغییر وضعیت/اصلاح به `baseRevision` صحیح نیاز دارند. همه شناسه‌ها در Tenant و Project جاری اعتبارسنجی می‌شوند.

## Party register

| Method | Path | Permission | کاربرد |
| --- | --- | --- | --- |
| `GET` | `/parties` | `commercial.parties.read` | فهرست طرف‌های پروژه |
| `POST` | `/parties` | `commercial.parties.manage` | ایجاد کارفرما، پیمانکار، فروشنده یا مشاور |
| `PUT` | `/parties/{partyId}` | `commercial.parties.manage` | اصلاح با Revision |
| `POST` | `/parties/{partyId}/status` | `commercial.parties.manage` | فعال/غیرفعال‌کردن کنترل‌شده |

Party در این نسخه project-scoped است. `code` در پروژه یکتا است. دفتر جامع فروشندگان، ارزیابی تأمین‌کننده و deduplication سازمانی خارج از این Slice است.

## Project contracts

| Method | Path | Permission | کاربرد |
| --- | --- | --- | --- |
| `GET` | `/contracts` | `contracts.read` | فهرست قراردادها |
| `POST` | `/contracts` | `contracts.capture` | ایجاد Draft |
| `PUT` | `/contracts/{contractId}` | `contracts.capture` | اصلاح Draft یا Returned |
| `POST` | `/contracts/{contractId}/submit` | `contracts.submit` | ارسال برای بررسی |
| `POST` | `/contracts/{contractId}/activate` | `contracts.review` | فعال‌سازی قرارداد Submitted |
| `POST` | `/contracts/{contractId}/return` | `contracts.review` | عودت برای اصلاح |
| `POST` | `/contracts/{contractId}/close` | `contracts.review` | خاتمه قرارداد Active |

نوع قرارداد یکی از `MainContract`، `Subcontract`، `Supply`، `ProfessionalService`، `Labor` یا `Other` است. `originalApprovedAmount`، تاریخ شروع/پایان و یادداشت اختیاری‌اند. مقدار `null` در مبلغ یعنی سقف اولیه هنوز معلوم/ثبت نشده است و در State به صفر تبدیل نمی‌شود.

## Contract amendments

| Method | Path | Permission | کاربرد |
| --- | --- | --- | --- |
| `GET` | `/contract-amendments` | `contracts.read` | فهرست الحاقیه‌ها؛ فیلتر اختیاری contractId |
| `POST` | `/contract-amendments` | `contracts.amendments.capture` | ایجاد Draft برای قرارداد Active |
| `PUT` | `/contract-amendments/{id}` | `contracts.amendments.capture` | اصلاح Draft یا Returned |
| `POST` | `/contract-amendments/{id}/submit` | `contracts.amendments.submit` | ارسال برای بررسی |
| `POST` | `/contract-amendments/{id}/approve` | `contracts.amendments.review` | تأیید و ورود به سقف محاسباتی |
| `POST` | `/contract-amendments/{id}/return` | `contracts.amendments.review` | عودت برای اصلاح |

نوع الحاقیه `ScopeChange`، `ValueChange`، `TimeExtension` یا `Mixed` است. تغییر ارزش به `amountDelta` غیرصفر، تمدید زمان به `extensionDays` مثبت و Mixed به هر دو نیاز دارد. فقط الحاقیه Approved در State اثر دارد.

## Purchase request and commitment

| Method | Path | Permission | کاربرد |
| --- | --- | --- | --- |
| `GET` | `/purchase-requests` | `procurement.requests.read` | فهرست درخواست‌ها |
| `POST` | `/purchase-requests` | `procurement.requests.capture` | ایجاد Draft |
| `PUT` | `/purchase-requests/{requestId}` | `procurement.requests.capture` | اصلاح Draft یا Returned |
| `POST` | `/purchase-requests/{requestId}/submit` | `procurement.requests.submit` | ارسال برای تأیید |
| `POST` | `/purchase-requests/{requestId}/approve` | `procurement.requests.review` | تأیید درخواست |
| `POST` | `/purchase-requests/{requestId}/return` | `procurement.requests.review` | عودت برای اصلاح |
| `GET` | `/purchase-orders` | `procurement.orders.read` | فهرست سفارش‌ها/تعهدها |
| `POST` | `/purchase-orders` | `procurement.orders.issue` | صدور سفارش از درخواست Approved |
| `POST` | `/purchase-orders/{orderId}/close` | `procurement.orders.manage` | بستن تعهد باز |
| `POST` | `/purchase-orders/{orderId}/cancel` | `procurement.orders.manage` | لغو دلیل‌دار تعهد باز |

`estimatedAmount` و `neededByDate` در درخواست خرید اختیاری‌اند. Approval درخواست به‌تنهایی تعهد مالی ایجاد نمی‌کند. تعهد فقط با صدور Purchase Order دارای Party، مبلغ مثبت، ارز پایه و Revision جاری درخواست ساخته می‌شود. اگر Contract انتخاب شود، Party سفارش باید همان Party قرارداد Active باشد. همان درخواست در V1 فقط یک سفارش دارد و به `Ordered` می‌رود.

## Commercial State

| Method | Path | Permission | کاربرد |
| --- | --- | --- | --- |
| `GET` | `/state` | `commercial-state.read` | آخرین Snapshot یا State قطعی بدون داده |
| `GET` | `/api/v1/portfolio/commercial-state` | `commercial-state.read` در scope پروژه | خلاصه portfolio-ready پروژه‌های مجاز |

Contract و Procurement هرکدام `contractState/procurementState` و `contractDataQualityStatus/procurementDataQualityStatus` مستقل دارند. وضعیت‌ها عبارت‌اند از `NotConfigured`، `SetupRequired`، `NoData`، `Available` و `Suspended`. کیفیت داده `NoData`، `Adequate` یا `NeedsAttention` است.

شاخص‌ها شامل قرارداد فعال، قرارداد بدون سقف، approval معطل، قرارداد Active منقضی، سقف مصوب معلوم، درخواست Approved بدون سفارش، تعهد باز و تعهد سررسیدگذشته است. `approvedContractCeilingAmount` تا زمانی که هیچ قرارداد Active/Closed با مبلغ اولیه معلوم وجود ندارد `null` می‌ماند.

Commercial State در Command Center بخش مستقل است و وارد رنگ Operational Health یا Financial State نمی‌شود.

## Finance linkage

در ایجاد یا اصلاح Finance Record دو شناسه اختیاری اضافه شده‌اند:

- `contractId`: قرارداد واقعی موجود در همان Tenant/Project؛
- `commitmentId`: Purchase Order واقعی موجود در همان Tenant/Project.

اگر هر دو ارسال شوند، commitment باید به همان Contract متصل باشد. `contractReference` متنی برای سازگاری و ثبت مرجع بیرونی حفظ شده است، اما lineage رسمی با شناسه انجام می‌شود. اتصال اختیاری است و نبود Contract یا Procurement مانع ثبت Finance پایه نمی‌شود.

## Scope exclusions

- RFQ، Tender، مقایسه پیشنهاد و گردش استعلام؛
- انبار، رسید/حواله کالا و تطبیق سه‌طرفه؛
- Invoice، صورت‌وضعیت، Retention و پرداخت قراردادی تفصیلی؛
- چندارزی، نرخ تبدیل و تسعیر؛
- تقسیم یک درخواست بین چند سفارش؛
- Vendor Master سراسری و ارزیابی تأمین‌کننده.
