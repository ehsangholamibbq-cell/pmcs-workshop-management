# Reporting Center API — V1.1 Phase 1

- Contract: `pmcs.reporting/v1`
- Checkpoint: `V1.1-RPT1`
- Base path: `/api/v1`
- Status: F01/F02 connected؛ F03 semantic contract candidate بدون Runtime؛ UI/Production disabled؛ RPT1 active

## ۱. قواعد عمومی

- تمام endpointها Authentication، Tenant scope و Project scope را از Context معتبر می‌گیرند؛
- Role، Tenant یا Permission از Body/Header سفارشی پذیرفته نمی‌شود؛
- تمام Mutationها `Idempotency-Key` و Correlation ID دارند؛
- `asOf` یک UTC instant صریح است؛ اگر خالی باشد، Server هنگام پذیرش Run آن را pin می‌کند؛
- Template version، locale، calendar و format در Run pin می‌شوند؛
- Errorها Problem Details با `code` ماشین‌خوان هستند؛
- هیچ endpoint عمومی Query/SQL/Template executable دریافت نمی‌کند.

### ۱.۱ سطح عملیاتی Readiness

`GET /health/ready` جزو API تجاری Reporting زیر `/api/v1` نیست. check با نام
`reporting-worker` فقط چهار مقدار عددی `activeRuns`، `heartbeatAgeSeconds`،
`oldestQueueAgeSeconds` و `queuedRuns` را منتشر می‌کند. دادهٔ سایر checkها، مقدارهای غیرعددی و
هر Tenant/Project/User/Run ID حذف می‌شوند. عبور queue age از budget یا heartbeat گمشده/کهنه
وضعیت `Degraded` می‌دهد؛ این payload جای exporter یا alerting را نمی‌گیرد. مسیر مستقل deployment
در MS03-C2 از OTLP به Collector، scrape پرومتئوس و Alertmanager متصل شده و endpoint تجاری تازه‌ای
زیر `/api/v1` اضافه نکرده است.

### ۱.۲ سطح عملیاتی remediation

MS04 هیچ endpoint تجاری یا عمومی تازه‌ای اضافه نمی‌کند. worker داخلی orphan remediation فقط با
`ReportingCenter:OrphanRemediationMode=InventoryOnly|ApplyEligible` فعال می‌شود و مقدار پیش‌فرض
`Disabled` است. قرارداد بین Reporting و Documents هیچ object key برنمی‌گرداند؛ APIهای
Create/Retry/Download/Verify و Permissionهای آن‌ها بدون تغییر مانده‌اند.

### ۱.۳ سطح Qualification معنایی و XLSX

MS05 endpoint تازه‌ای اضافه نمی‌کند. همان `asOfUtc` موجود با دو cutoff پیش/پس از correction، دو
twin در هر cutoff و parser مستقل ZIP/OpenXML qualify شده است. projection تاریخی state/revision و
metadata supersession آینده را نشت نمی‌دهد؛ v3 Draft نیز وارد Snapshot یا XLSX نمی‌شود. replay همان
Output باید bytes و SHA-256 یکسان برگرداند و workbook فقط metadata خروجی‌ویژه را از semantic digest
حذف می‌کند.

### ۱.۴ خانواده F02 روی API موجود

Micro-Step جاری endpoint تازه‌ای اضافه نمی‌کند، اما Definition دوم را روی همان routeهای موجود منتشر
می‌کند. قرارداد `PMCS-RPT1-F02-SEMANTIC-001 v1.3.1` شناسه Definition
`project-periodic-certified/1.0.0` و schemaهای
`pmcs.reporting.project-periodic.parameters/v1` و
`pmcs.reporting.project-periodic.snapshot/v1` را pin کرده است. پارامترهای Client فقط
`periodKind=Weekly|Monthly` و `periodStartLocalDate` خواهند بود؛ `projectId` از route و `asOfUtc` از
Run pin می‌شود و end date، Time Zone، Source ID، Query یا filter دلخواه پذیرفته نمی‌شود. Project
revision/configuration/time zone نیز هنگام پذیرش Run در evidence سروری جداگانه pin می‌شود و Client
اجازه ارسال یا override آن را ندارد.

### ۱.۵ Renderer و Worker خانواده F02

Safe Checkpoint `S07-MS04` قرارداد داخلی
`pmcs.reporting.project-periodic.renderer/v1`، layout
`pmcs.reporting.project-periodic.layout/v1` و PDF/XLSX deterministic را تثبیت کرد. Checkpoint
`S07-MS05` همان Rendererها را در registry اختصاصی ثبت و Worker را براساس
Definition dispatch می‌کند. Download/Verify از همان کنترل‌های permission، integrity و object ownership
F01 استفاده می‌کنند؛ route یا bypass جداگانه‌ای وجود ندارد. تنظیمات production همچنان خاموش‌اند.

Source `7fc55c167ad2159a31c895b32a52d78f47574df9` با tree
`d665fe4cdf29369f96ec0875bc6f1535db349d55` در Run 144 (`35498990050`) هر هشت Job را پاس کرد؛
هارنس متصل F02 هر `13/13` assertion و Restore Drill هر ۴۴ Migration را تأیید کردند.

### ۱.۶ قرارداد آینده F03 بدون تغییر API

Micro-Step `S07-MS06` route تازه‌ای اضافه نمی‌کند. قرارداد
`PMCS-RPT1-F03-SEMANTIC-001 v1.0.0` پارامتر Client را دقیقاً `{}` تعریف می‌کند؛ `projectId` از route
و `sourceCutoffUtc` از `asOfUtc` پین‌شدهٔ Run می‌آیند. Client اجازه ارسال `snapshotId`، تاریخ، status،
include flag یا انتخاب Source را ندارد.

Runtime آینده فقط از Application Contract خواندنی ProjectIntelligence، Snapshot رسمی و immutable
تا cutoff را انتخاب می‌کند و هرگز `project-state.recalculate` یا endpoint Command Center را
فراخوانی نمی‌کند. Project State عملیاتی، Coverage/Freshness/Confidence و partial scope بدون join
Finance/Commercial/Planning/Quality/HSE/Action حمل می‌شوند. Definition، Template، schema ID،
Catalog/API dispatch، Worker و Renderer F03 در این Micro-Step هنوز وجود ندارند.

## ۲. Catalog

```http
GET /api/v1/projects/{projectId}/reports/catalog
GET /api/v1/projects/{projectId}/reports/catalog/{definitionCode}
```

Permission: `reporting.catalog.read` و Permissionهای پایه‌ای که Definition برای visibility اعلام می‌کند.

نمونهٔ Definition نخست:

```json
{
  "code": "daily-report-certified",
  "title": "گزارش روزانه رسمی",
  "description": "نسخه رسمی گزارش روزانه و زنجیره اصلاحات آن",
  "scope": "Project",
  "classification": "Internal",
  "parameterSchemaVersion": "1.0",
  "templateVersion": "1.0.0",
  "supportedFormats": ["Pdf", "Xlsx"],
  "requiredSourcePermissions": ["field.daily-reports.read"],
  "dataStatuses": ["Available", "NoData", "InsufficientData"]
}
```

Catalog فقط Definitionهایی را برمی‌گرداند که Actor در همان Project اجازه دیدن آن‌ها را دارد.

Definition دوم `project-periodic-certified` با schema
`pmcs.reporting.project-periodic.parameters/v1`، Template `1.0.0`، فرمت‌های `Pdf/Xlsx` و چهار وضعیت
`Available/NoData/InsufficientData/NotConfigured` منتشر می‌شود.

## ۳. ایجاد Run

```http
POST /api/v1/projects/{projectId}/reports/runs
Idempotency-Key: <stable-key>
Content-Type: application/json
```

```json
{
  "clientGeneratedId": "11111111-1111-1111-8111-111111111111",
  "definitionCode": "daily-report-certified",
  "templateVersion": "1.0.0",
  "asOfUtc": "2026-09-18T12:00:00Z",
  "formats": ["Pdf", "Xlsx"],
  "parameters": {
    "dailyReportId": "22222222-2222-2222-8222-222222222222",
    "includeRevisionChain": true
  }
}
```

نمونهٔ F02 روی همان endpoint:

```json
{
  "clientGeneratedId": "11111111-1111-1111-8111-111111111112",
  "definitionCode": "project-periodic-certified",
  "templateVersion": "1.0.0",
  "asOfUtc": "2026-09-20T12:00:00Z",
  "formats": ["Pdf", "Xlsx"],
  "parameters": {
    "periodKind": "Weekly",
    "periodStartLocalDate": "2026-09-12"
  }
}
```

Permission:

- `reporting.run.create`؛
- تمام `requiredSourcePermissions` تعریف؛
- Project باید Active باشد.

پاسخ `202 Accepted`:

```json
{
  "id": "11111111-1111-1111-8111-111111111111",
  "projectId": "33333333-3333-3333-8333-333333333333",
  "definitionCode": "daily-report-certified",
  "templateVersion": "1.0.0",
  "status": "Queued",
  "pipelineStage": "Queued",
  "asOfUtc": "2026-09-18T12:00:00Z",
  "requestedFormats": ["Pdf", "Xlsx"],
  "attemptCount": 0,
  "createdAt": "2026-09-18T12:00:01Z",
  "links": {
    "self": "/api/v1/projects/.../reports/runs/..."
  }
}
```

همان Idempotency key و payload همان response را replay می‌کند. key یکسان با payload متفاوت
`409 idempotency.key.reused` است. `clientGeneratedId` متعلق به Tenant/Project دیگر قابل استفاده نیست.

## ۴. مشاهده Run و فهرست

```http
GET /api/v1/projects/{projectId}/reports/runs?status=Succeeded&definitionCode=daily-report-certified&limit=50
GET /api/v1/projects/{projectId}/reports/runs/{runId}
```

Permission: `reporting.catalog.read` و Source permission جاری.

پاسخ Run موفق شامل metadata است، نه Snapshot payload کامل:

```json
{
  "id": "...",
  "status": "Succeeded",
  "pipelineStage": "Complete",
  "dataStatus": "Available",
  "definitionCode": "daily-report-certified",
  "templateVersion": "1.0.0",
  "asOfUtc": "2026-09-18T12:00:00Z",
  "snapshotHash": "0123456789abcdef...",
  "attemptCount": 1,
  "startedAt": "2026-09-18T12:00:02Z",
  "completedAt": "2026-09-18T12:00:04Z",
  "diagnosticCode": null,
  "outputs": [
    {
      "id": "...",
      "format": "Pdf",
      "fileName": "daily-report-PRJ-1405-06-27-r2.pdf",
      "contentType": "application/pdf",
      "sizeBytes": 48231,
      "sha256": "...",
      "verificationCode": "RPT-...",
      "downloadUrl": "/api/v1/projects/.../reports/outputs/.../content"
    }
  ]
}
```

Failure detail فقط code و توضیح امن می‌دهد؛ stack trace، Source payload و object key برگردانده نمی‌شود.
`NoData` و `InsufficientData` failure نیستند؛ در `dataStatus` ثبت می‌شوند و خروجی صریح بدون عدد
ساختگی ساخته می‌شود.

## ۵. Retry و Cancel

```http
POST /api/v1/projects/{projectId}/reports/runs/{runId}/retry
POST /api/v1/projects/{projectId}/reports/runs/{runId}/cancel
Idempotency-Key: <stable-key>
```

- Retry فقط برای Failureهای retryable و در سقف policy مجاز است؛
- Permissionها دوباره بررسی می‌شوند؛
- Retry Output تکراری ایجاد نمی‌کند؛
- Cancel فقط پیش از ورود Run به `Rendering` و پیش از هر immutable output مجاز است؛
- Run موفق حذف یا overwrite نمی‌شود.

## ۶. Download

```http
GET /api/v1/projects/{projectId}/reports/outputs/{outputId}/content
```

Permission:

- `reporting.output.download`؛
- Permissionهای Source تعریف در زمان دانلود؛
- Tenant/Project/Classification جاری.

Server باید Document owner/type، released state، size، content type و SHA-256 را verify کند. هر mismatch
با `502 reporting.output.integrity_failed` متوقف و با event ممیزی
`CertifiedReportOutputIntegrityFailed` ثبت می‌شود. generic Documents endpoint برای owner type
`ReportOutput` پاسخ content نمی‌دهد. Download/Verify می‌تواند در rollback با
`ReportingCenter:Phase1Enabled=false` و `ReportingCenter:OutputAccessEnabled=true` برای خروجی‌های
موجود روشن بماند؛ Create/Catalog/Run mutation در این حالت بسته است.

Headerهای لازم:

```text
Content-Type: application/pdf | application/vnd.openxmlformats-officedocument.spreadsheetml.sheet
Content-Disposition: attachment; filename*=UTF-8''...
X-Content-Type-Options: nosniff
Cache-Control: private, no-store
ETag: "sha256-..."
```

## ۷. Verification

```http
GET /api/v1/projects/{projectId}/reports/outputs/{outputId}/verify
```

این endpoint عمومی و ناشناس نیست. پاسخ فقط metadata مجاز را پس از Permission برمی‌گرداند:

```json
{
  "verificationCode": "RPT-...",
  "status": "Valid",
  "definitionCode": "daily-report-certified",
  "templateVersion": "1.0.0",
  "asOfUtc": "2026-09-18T12:00:00Z",
  "sha256": "...",
  "archived": false
}
```

`Valid` به معنی تأیید صحت Artifact و lineage است، نه تأیید حقوقی محتوای خارج از Workflow PMCS.

## ۸. Status و Error codeها

| HTTP | Code | معنا |
| --- | --- | --- |
| 400 | `reporting.definition.invalid` | Definition ناشناخته یا retired |
| 400 | `reporting.parameters.invalid` | پارامتر ناسازگار با schema |
| 400 | `reporting.format.unsupported` | format خارج allowlist |
| 400 | `reporting.as_of.future` | cutoff در آینده است |
| 403 | `reporting.permission.denied` | Reporting permission ندارد |
| 403 | `reporting.source_permission.denied` | Source permission ندارد |
| 404 | `reporting.run.not_found` | Run در Scope Actor وجود ندارد |
| 409 | `reporting.run.not_retryable` | Retry مجاز نیست |
| 409 | `reporting.run.already_final` | Run immutable شده است |
| 502 | `reporting.output.integrity_failed` | Artifact با Manifest تطبیق ندارد |

Diagnosticهای Worker مانند `reporting.permission.revoked`، `reporting.template.retired`،
`reporting.renderer.license_unconfigured`، `reporting.renderer.license_unapproved`،
`reporting.renderer.font_missing`، `reporting.renderer.font_integrity_failed`،
`reporting.renderer.configuration_unpinned` و
`documents.generated.storage_unavailable` در `diagnosticCode` Run دیده می‌شوند؛ آن‌ها پاسخ HTTP
مستقیم Create نیستند. ADR 0030 انتخاب `Community` را ثبت کرده، اما
`PdfLicense=Unconfigured` حالت fail-closed پیش‌فرض deployment باقی می‌ماند؛ QA فقط برای Golden
آن را صریحاً `Community` می‌کند و tier دیگر بدون ADR جدید پذیرفته نمی‌شود.

## ۹. Versioning

- Contract path در این فاز `/api/v1` است؛
- Definition code پایدار و Template version semantic است؛
- parameter schema و snapshot schema جدا version می‌شوند؛
- معنی فیلد موجود تغییر نمی‌کند؛ نسخه جدید افزوده می‌شود؛
- Output قدیمی با Template pinned خود قابل Verification و بازتولید باقی می‌ماند.
