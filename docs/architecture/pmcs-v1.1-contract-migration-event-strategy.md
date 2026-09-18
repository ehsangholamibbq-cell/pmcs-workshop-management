# PMCS V1.1 — API، Event، Migration و Compatibility Strategy

- شناسه: `PMCS-ARCH-V1.1-CONTRACT-001`
- نسخه: `1.0.0`
- وضعیت: Governance Contract
- Parent Runtime: `26bf222d44634562ca7f3fc0931f3f8b79ca04a1`

## ۱. اصول معماری

- ساختار اصلی Modular Monolith باقی می‌ماند؛
- هر ماژول مالک Domain، Application و Persistence خود است؛
- دسترسی مستقیم به جدول، DbContext یا Repository ماژول دیگر ممنوع است؛
- Query بین‌ماژولی از Application Contract/Projection و Mutation از Command مالک انجام می‌شود؛
- Eventها از Outbox نسخه‌دار عبور می‌کنند و Consumer باید Idempotent باشد؛
- Product version با API version یکسان فرض نمی‌شود؛ endpointهای سازگار جدید زیر قرارداد موجود `/api/v1` افزوده می‌شوند؛
- Breaking contract فقط با API/Event version جدید و compatibility window مجاز است.

## ۲. Extensibility manifests

### `ModuleDescriptor`

فیلدهای اجباری: `moduleId`، `version`، `capabilities`، `dependencies`، `migrationsOwner`، `permissionsManifest`، `navigationManifest`، `toolsManifest` و `minimumPlatformVersion`.

### `PermissionManifest`

فقط Permission key، Scope و Risk را اعلام می‌کند. تصمیم Role/Actor در Engine مرکزی باقی می‌ماند.

### `NavigationManifest`

Route و Label و Feature flag را اعلام می‌کند؛ نمایش Navigation به‌معنی Permission Allow نیست.

### `ToolManifest`

برای هر Agent Tool شامل Input/Output schema، Permission، Risk class، timeout، data classification و citation policy است. Tool حق SQL/DB ندارد.

## ۳. قراردادهای جدید

| دامنه | Aggregate/Contract | مسیر منطقی API |
| --- | --- | --- |
| Identity Access | `LoginExperienceDescriptor` | خواندن عمومی `/api/v1/public/login-experience` و مدیریت `/api/v1/identity/login-experiences` |
| Identity Access | `MemberProfile` | Self-service روی `/api/v1/member-profile` و Directory روی `/api/v1/member-profiles/{userId}` |
| Projects | `ProjectBootstrapPlan/Run` | `/api/v1/project-bootstraps` |
| Documents | `Document/Asset/UploadSession` | `/api/v1/documents` و `/api/v1/upload-sessions` |
| Reporting | `ReportDefinition/Run/Output` | `/api/v1/reports` |
| Collaboration | `ProjectRoom/Message` | `/api/v1/projects/{projectId}/collaboration` |
| Intelligence | `IntelligenceSession/ToolInvocation` | `/api/v1/intelligence` |

تمام Mutationها از Actor/Tenant context معتبر، Permission مرکزی، Correlation ID و `Idempotency-Key` در عملیات retryable استفاده می‌کنند. Client اجازه ارسال Role یا Tenant authoritative در Body ندارد.

## ۴. Event catalog اولیه

| Event | Producer | Consumer مجاز |
| --- | --- | --- |
| `identity.login-experience.published.v1` | Identity Access | Web cache invalidation/Audit projection |
| `identity.member-profile.updated.v1` | Identity Access | Directory/Collaboration projection |
| `projects.bootstrap.completed.v1` | Projects | Notification/Audit/Project setup orchestration |
| `documents.asset.released.v1` | Documents | Owner Context با reference معتبر |
| `collaboration.message.created.v1` | Collaboration | Notification/Search projection |
| `reporting.report.completed.v1` | Reporting | Notification/Archive |
| `intelligence.tool-invocation.recorded.v1` | Intelligence | Audit/Telemetry |

Event payload فقط شناسه‌های لازم، schema version، occurredAt UTC، tenantId، correlationId و دادهٔ classification-safe را دارد. Secret، token، فایل خام و متن حساس غیرضروری در Event ممنوع است.

## ۵. Migration ownership

- هر Migration در Assembly/Module مالک ثبت می‌شود؛
- نام/شماره Migration در کل Repository یکتا است؛
- Schema جدید با Expand → Migrate → Verify → Contract پیش می‌رود؛
- Column/Table موجود V1 در V1.1 بدون compatibility window حذف یا بازتعبیر نمی‌شود؛
- Profile، Branding، Bootstrap، Documents، Reporting، Collaboration و Intelligence storage مالک مستقل دارند؛
- Project Bootstrap هیچ Table عمومی را با `INSERT ... SELECT *` کپی نمی‌کند؛
- Seed و QA fixture از Production migration جدا می‌ماند؛
- Upgrade از Snapshot نماینده V1 و Restore Drill پیش از Candidate اجباری است.

## ۶. Offline، Retry و Conflict

- Read modelهای Cache شده دارای version/etag و stale state صریح هستند؛
- Profile update با optimistic version و conflict response انجام می‌شود؛
- Asset upload دارای stable upload/session identity و resumable policy است؛
- Chat دارای stable client message ID، ordering و duplicate prevention است؛
- Project Bootstrap در Offline قابل اجرا نیست؛ فقط Draft محلی انتخاب‌ها ممکن است و Execute نیازمند اتصال و Preview تازه است؛
- Report Run و Agent Tool Invocation عملیات Server-side هستند و Offline فقط request draft نگه می‌دارد؛
- Retry نتیجهٔ تکراری ایجاد نمی‌کند و Idempotency response قابل بازیابی است.

## ۷. Login safety boundary

`LoginExperienceDescriptor` فقط به schema allowlisted نگاشت می‌شود. Assetها private-origin/same-site یا object URL امضاشدهٔ کنترل‌شده هستند. هیچ Script، iframe، remote executable URL یا inline event handler از Descriptor تولید نمی‌شود. خرابی تمام تنظیمات به Fallback داخلی و فرم Login قابل استفاده منجر می‌شود.

## ۸. Project Bootstrap contributor contract

هر `ProjectBootstrapContributor` باید این موارد را اعلام کند:

- `contributorId` و `schemaVersion`؛
- Scope و Permission لازم؛
- Dependencyها و ترتیب اجرا؛
- فهرست allowlisted داده‌های Cloneable؛
- conflict/skip policy؛
- Preview model و digest؛
- Execute با همان digest؛
- compensating behavior یا fail-safe state؛
- Validation پس از اجرا.

Preview منقضی یا دارای digest متفاوت قابل Execute نیست. پروژه مقصد تا Validation کامل Draft می‌ماند.

## ۹. Agent boundary

مسیر تنها مسیر مجاز است:

`Agent → Permission-aware Tool → PMCS Application Service → Business Rules → Database`

Stage 1 فقط Foundation و Reference read-only tool دارد. Model output هیچ‌گاه مستقیماً Migration، Event، Command یا SQL تولید و اجرا نمی‌کند.

## ۱۰. Compatibility Gate

- Consumer-driven contract test برای API و Event؛
- old-client behavior برای endpointهای موجود V1؛
- migration ledger comparison؛
- unknown manifest/version باید fail closed شود؛
- module disable نباید داده را حذف کند؛
- rollback Runtime باید دادهٔ جدید را نادیده بگیرد، نه حذف کند.
