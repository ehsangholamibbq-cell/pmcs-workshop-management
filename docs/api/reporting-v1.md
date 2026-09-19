# Reporting Center API — V1.1 Phase 1

- Contract: `pmcs.reporting/v1`
- Checkpoint: `V1.1-RPT1`
- Base path: `/api/v1`
- Status: Runtime implemented؛ latest qualification candidate `4ff44c96104ee1df87d267ca9a530d19b9248ba3` passed Run 123؛ business API unchanged and extended RPT1 gates open

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
`reporting.renderer.license_unconfigured`، `reporting.renderer.font_missing` و
`documents.generated.storage_unavailable` در `diagnosticCode` Run دیده می‌شوند؛ آن‌ها پاسخ HTTP
مستقیم Create نیستند. `PdfLicense=Unconfigured` حالت fail-closed پیش‌فرض است و تا انتخاب حقوقی
صریح Community/Professional/Enterprise تغییر نمی‌کند.

## ۹. Versioning

- Contract path در این فاز `/api/v1` است؛
- Definition code پایدار و Template version semantic است؛
- parameter schema و snapshot schema جدا version می‌شوند؛
- معنی فیلد موجود تغییر نمی‌کند؛ نسخه جدید افزوده می‌شود؛
- Output قدیمی با Template pinned خود قابل Verification و بازتولید باقی می‌ماند.
