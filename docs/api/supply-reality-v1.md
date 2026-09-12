# Supply Reality API V1

Base path:

```text
/api/v1/projects/{projectId}/commercial/supply
```

همه Commandها به `Idempotency-Key`، Actor معتبر و Permission همان عملیات نیاز دارند. همه شناسه‌ها با Tenant و Project دوباره اعتبارسنجی می‌شوند. شماره رسمی Receipt، Issue، Adjustment و Service Acceptance فقط در سرور ساخته می‌شود.

## State

`GET /state` با Permission `supply.read` این مجموعه را برمی‌گرداند:

- قلم‌ها و نسخه‌های تبدیل واحد؛
- محل‌های موجودی؛
- Receipt، نتیجه Inspection و زمان PostStock؛
- Material Issue، مانده Custody و Reconciliationهای افزایشی؛
- Adjustmentها و Service Acceptanceها؛
- آخرین ۲۰۰۰ رخداد Ledger برای Drill-down؛
- Stock Position محاسبه‌شده از کل Ledger، بدون محدودیت ۲۰۰۰ رخداد؛
- شمارنده‌های Inspection، Quarantine/Reject، Custody و Adjustment؛
- عملکرد تحویل تأمین‌کننده بر اساس Receipt و Due Date سفارش.

`availableBaseQuantity` فعلاً برابر On-hand Ledger است و Reservation هنوز پیاده‌سازی نشده؛ این مقدار نباید به‌عنوان Available-to-promise قراردادی تفسیر شود.

## Catalog

| Method | Path | Permission | قاعده |
| --- | --- | --- | --- |
| POST | `/items` | `supply.items.manage` | قلم Material/Service/EquipmentRental و واحد پایه |
| POST | `/items/{id}/unit-conversions` | `supply.items.manage` | افزودن نسخه Conversion بدون بازنویسی نسخه قبلی |
| POST | `/items/{id}/status` | `supply.items.manage` | تغییر وضعیت Revision-controlled |
| POST | `/locations` | `supply.locations.manage` | Quarantine/Rejected نمی‌تواند Available باشد |
| POST | `/locations/{id}/deactivate` | `supply.locations.manage` | فقط وقتی مانده همه قلم‌ها صفر است |

## Receipt and inspection

| Method | Path | Permission | اثر رسمی |
| --- | --- | --- | --- |
| POST | `/receipts` | `supply.receipts.capture` | دریافت فیزیکی با Evidence؛ بدون تغییر موجودی |
| POST | `/receipts/{id}/submit-inspection` | `supply.receipts.capture` | انتقال به صف بازرسی |
| POST | `/receipts/{id}/inspect` | `supply.inspections.review` | ثبت Accepted/Rejected/Quarantined با جمع دقیق |
| POST | `/receipts/{id}/post-stock` | `supply.inventory.post` | فقط Accepted Quantity را به Ledger اضافه می‌کند |

Receipt فقط برای سفارش `Issued` دارای Supply Item، Ordered Quantity و Unit پذیرفته می‌شود. مجموع Receiptهای قبلی و جدید از مقدار سفارش عبور نمی‌کند، مگر Actor دارای `supply.receipts.override-excess` باشد و دلیل اضافه‌تحویل ثبت کند.

## Inventory and custody

| Method | Path | Permission | قاعده |
| --- | --- | --- | --- |
| POST | `/material-issues` | `supply.inventory.issue` | موجودی کافی را کم می‌کند و Custody می‌سازد؛ مصرف نیست |
| POST | `/material-issues/{id}/acknowledge` | `supply.inventory.acknowledge` | دریافت امانت را با Reference تأیید می‌کند |
| POST | `/material-issues/{id}/reconcile` | `supply.inventory.reconcile` | Consumption/Return/Waste افزایشی؛ Return به Ledger برمی‌گردد |
| POST | `/transfers` | `supply.inventory.transfer` | Entry خروج و ورود متوازن در تراکنش واحد |
| POST | `/adjustments` | `supply.inventory.count` | مقایسه Physical Count با Balance سرور در Cutoff |
| POST | `/adjustments/{id}/review` | `supply.inventory.adjust` | تأیید یا رد مستقل؛ رد نیازمند دلیل |
| POST | `/adjustments/{id}/post` | `supply.inventory.adjust` | رخداد Adjustment؛ بدون Update موجودی قبلی |

موجودی منفی آنلاین برای Issue، Transfer و Adjustment رد می‌شود. در Reconciliation، جمع مصرف، برگشت و پرت نمی‌تواند از مانده Custody بیشتر شود و پرت به علت و Evidence نیاز دارد.

## Service acceptance

`POST /service-acceptances` با Permission `supply.services.accept` فقط برای سفارش فعالِ Service یا EquipmentRental دارای قلم و مقدار معتبر است. Accepted + Rejected باید دقیقاً با Delivered برابر باشد و Reject نیازمند Comment است. این Endpoint هیچ Stock Position یا Ledger Entry نمی‌سازد.

## Semantics and current limits

- Submittal Approval ورودی Receipt یا Service Acceptance نیست.
- WBS و Budget Line اختیاری‌اند و `NotConfigured` با صفر یا سلامت اشتباه نمی‌شود.
- Client حق ارسال شماره رسمی یا System Balance را ندارد.
- Offline cache فقط read-only است؛ Command آفلاین رسمی یا Draft provisional در V1 این Slice وجود ندارد.
- RFQ/Quote/Evaluation، PO Revision/Expediting، Reservation/Hold، Return-to-vendor و Invoice Matching هنوز در این API نیستند.
