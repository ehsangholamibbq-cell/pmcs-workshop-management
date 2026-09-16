# PMCS — سامانه کنترل مدیریت پروژه‌های عمرانی

این Repository نقطه شروع پیاده‌سازی PMCS است: سامانه‌ای که کاربران عملیاتی در آن «واقعیت پروژه» را ثبت می‌کنند و موتورهای قطعی، Project State قابل استناد برای مدیر پروژه و مدیرعامل می‌سازند.

## وضعیت فعلی

Foundation Sprint — checkpoint 23 verified; checkpoint 24 is next:

- Modular Monolith backend skeleton
- Tenant/actor boundary توسعه
- Platform foundations: audit، outbox، idempotency و migrations
- Project Setup و Daily Report ساختاریافته با Submit/Return/Approve و Permission enforcement
- اصلاح نسخه‌ای Daily Report بدون بازنویسی تاریخچه، با Fact lineage و Supersede اتمیک هنگام تأیید جایگزین
- کارتابل permission-scoped «کارهای من» برای گزارش‌های نیازمند تصمیم/اصلاح و Actionهای محول‌شده
- اعلان داخل سامانه با deduplication، Read/Acknowledge مستقل و ثبت تراکنشی همراه رویداد منبع
- Next.js RTL/PWA با فرم پویا، IndexedDB operation queue، Sync واقعی و Exception review
- transaction واحد Aggregate + Audit + Outbox + Idempotency
- Project State قطعی و نسخه‌دار فقط از Factهای Approved
- Coverage/Freshness/Confidence و Command Center متصل به Read Model واقعی
- Attention triage برای Issue/Stoppage با lineage تا Fact منبع
- Evidence metadata و private S3 upload/download با کنترل SHA-256 و اندازه
- IndexedDB attachment queue مستقل، upload retry و camera/file capture
- Action Control با Assignee، Due Date، Priority، Revision و workflow تعیین تکلیف
- Project State refresh خودکار از Outbox پس از Approval
- Project Calendar اختیاری و `project-state-v2` با Coverage بر مبنای روز کاری یا fallback صریح هفت‌روزه
- Finance Lite برای Receipt، Payment، Petty Cash Funding/Expense با Workflow کنترل‌شده
- Budget Baseline اختیاری با Submit/Approve/Return/Supersede
- Financial State مستقل از Health عملیاتی و قابل استفاده بدون بودجه اولیه
- Commercial bounded context مستقل برای Party، Contract، Amendment، Purchase Request و Purchase Order
- سقف اولیه قرارداد و برآورد خرید اختیاری؛ نبود آن‌ها هرگز صفر تلقی نمی‌شود
- Commitment واقعی فقط پس از صدور Purchase Order از درخواست Approved
- Commercial State مستقل Contract/Procurement و خروجی portfolio-ready
- اتصال اختیاری Finance Record به Contract/Commitment واقعی و هم‌پروژه
- PostgreSQL و S3-compatible development services
- CI، تست‌های دامنه و architecture checks
- رابط کامل فارسی و RTL با معادل فارسی برای اصطلاحات ضروری WBS، RFI و HSE
- ترجمه مرکزی خطاهای API و شبکه بدون نمایش پیام خام انگلیسی Backend
- گیت `audit:fa` برای جلوگیری از نشت دوباره متن لاتین به رابط کاربری
- Portfolio Command Center فارسی با فیلتر، مرتب‌سازی و ورود به مرکز فرمان هر پروژه
- وضعیت‌های مستقل Operational، Financial و Commercial بدون Composite Health
- Project Registry، مدیر پروژه و Action Exceptionهای بین‌پروژه‌ای با مسئول و سررسید
- تجمیع مالی/تعهد فقط به تفکیک ارز و بدون تبدیل ارزی پنهان
- نمایش صریح پروژه بدون Snapshot، WBS، بودجه اولیه یا HSE
- Project Intelligence مشورتی از داده ساختاریافته و permission-scoped
- صف پایدار تولید تحلیل، Structured Output، citation validation و safe failure
- بازبینی انسانی نسخه‌دار با Audit/Outbox، بدون تغییر Project State یا ساخت Action رسمی
- ثبت provenance شامل Snapshot، Context hash، Model، Prompt/Policy version و expiry
- مرز احراز هویت Production با OIDC/OAuth JWT Bearer و Claimهای `tenant_id/sub`
- Development identity به‌صورت Authentication Scheme ایزوله و بی‌اثر در Production
- fail-fast تنظیمات Production برای credential توسعه، CORS/host ناامن و auto-create Bucket
- rate limit سراسری per actor/IP و محدودیت مستقل تولید تحلیل پرهزینه
- correlation id امن، structured request telemetry و Meter پایدار `Pmcs.Api`
- health مستقل liveness/readiness و PostgreSQL readiness check
- Backup checksumدار، Restore Drill بدون overwrite و Runbook بازیابی PostgreSQL/Object Storage
- PostgreSQL/API integration smoke در CI و تست‌های مرز زیرساخت API
- Keycloak مستقل با Realm توسعه mount‌شده خارج از Image عملیاتی، Claim سازمان، audience API و ورود دومرحله‌ای
- BFF دیتابیس‌محور Next.js با Authorization Code + PKCE و Tokenهای رمز‌شده
- Cookie نشست HttpOnly/Secure/SameSite و عدم دسترسی Browser به Access/Refresh Token
- حذف Headerهای هویتی قابل جعل در BFF و محدودسازی Proxy به `/api/v1`
- Session واقعی فقط برای User و Tenant فعال و قطع مجوز عضویت در حالت تعلیق
- IndexedDB و Cache محلی مستقل برای هر Tenant/User و پاک‌سازی هنگام خروج امن
- Service Worker بدون Cache صفحه‌های احراز‌شده
- پنل فارسی مدیریت کاربران، دعوت‌ها، نقش سازمانی و عضویت پروژه
- دعوت پایدار Keycloak با Retry، Required Actions و جلوگیری از تصاحب حساب موجود بر اساس ایمیل
- پاک‌سازی پایدار حساب بیرونی در لغو، انقضا یا شکست نهایی دعوت
- تعلیق fail-closed همراه صف Disable/Logout، ابطال توکن‌های قبلی و همگرایی با آخرین وضعیت حساب داخلی
- حفاظت از آخرین مدیر فعال، Self-lockout، Audit، Outbox، Idempotency و Rate Limit مدیریت هویت
- ماژول مستقل Planning/Progress با کاتالوگ قلم اندازه‌گیری قابل استفاده بدون WBS
- اتصال اختیاری Fact پیشرفت به قلم اندازه‌گیری و کنترل دوباره شناسه/واحد هنگام Sync
- دفتر پیشرفت با تفکیک مقدار موقت از تأییدشده و حفظ واقعیت‌های بدون اتصال
- عدم تولید درصد کل، انحراف زمان‌بندی یا پیش‌بینی تا زمان وجود مبنای رسمی اندازه‌گیری/برنامه
- Planning Mode نسخه‌دار، Baseline مصوب برای Simple/Milestone/WBS/External و وزن‌دهی رسمی ۱۰۰٪
- Planned/Actual/Variance قطعی فقط از مبنای مصوب و Actual تأییدشده؛ بدون تبدیل داده ناقص به صفر
- پیشرفت دستی Milestone فقط همراه Evidence و Approval انسانی
- ماژول مستقل Technical Office برای Document Register، Revision، RFI، Submittal و Transmittal
- شماره رسمی مدارک، پرسش‌ها، بسته‌ها و ابلاغ‌ها فقط توسط سرور و بدون اعتماد به Browser
- Current Official Revision فقط پس از صدور Transmittal معتبر؛ Upload به‌تنهایی ابلاغ نیست
- تفکیک Answered/Accepted/Closed در RFI و ثبت Source/ReceivedBy برای پاسخ بیرونی
- Resubmission مستقل و غیرقابل‌بازنویسی؛ Approval فنی به‌معنی تحویل مصالح یا پذیرش کارگاه نیست
- دفتر فارسی وضعیت فنی با Cache کنترل‌شده و عملیات رسمی Online-only
- زنجیره واقعیت تأمین روی همان Purchase Request/Order موجود، بدون سفارش موازی
- قلم تدارکاتی نسخه‌دار با واحد پایه و تاریخچه تبدیل واحد
- تفکیک دریافت فیزیکی، بازرسی، پذیرش و ورود صریح مقدار پذیرفته‌شده به موجودی
- جلوگیری از اضافه‌تحویل بدون دلیل و Permission مستقل
- دفتر موجودی append-only با حفاظت Update/Delete در PostgreSQL و محاسبه مانده از کل Ledger
- انتقال متوازن بین محل‌ها و جلوگیری از موجودی منفی در عملیات آنلاین
- تفکیک تحویل ماده از مصرف، برگشت و پرت با Custody/Reconciliation مستند
- شمارش فیزیکی، Review و Adjustment رخدادمحور بدون بازنویسی موجودی
- پذیرش مستقل خدمت و اجاره تجهیز بدون ایجاد موجودی کالایی
- رابط فارسی تدارکات با Cache فقط‌خواندنی آفلاین و Commandهای رسمی Online-only
- ماژول مستقل Quality/HSE با Setup، مسئول و ماتریس ریسک نسخه‌دار برای هر حوزه
- ثبت سریع، Triage و Conversion انسانی بدون یکی‌گرفتن Intake با پرونده رسمی
- Inspection، NCR، Defect و Test Record با Readiness، Result، Disposition و Verification صریح
- Incident محرمانه، Permit-to-work، Toolbox Talk، Competency و Exposure Hours
- اقدام اصلاحی حوزه‌مند با تفکیک Completed، Verified و Closed و تمدید ممیزی‌شده
- نرخ رخداد فقط از کل Incident رسمی و کل نفر-ساعت تأییدشده، بدون صفر یا سلامت ساختگی
- نقش‌ها و Permissionهای مستقل کنترل کیفیت و HSE و Draft آفلاین فقط برای Intake اولیه
- دفتر کنترل مدیریتی مستقل برای Issue، Risk، Decision Request، Decision و Escalation
- ماتریس ریسک پنج‌درپنج و قواعد توافق سطح خدمت immutable و نسخه‌دار
- تحقق ریسک با ساخت Issue پیوندخورده، بدون بازنویسی Risk تاریخی
- تفکیک Resolved/Closed و الزام مدرک و کاربر راستی‌آزمایی مستقل برای بستن Issue
- درخواست تصمیم با جداسازی Facts/Assumptions/Predictions و حداقل دو Option
- تصمیم رسمی immutable، محدود به Authority مجاز و دارای supersession chain
- محاسبه مهلت elapsed یا working-day با Calendar/Time Zone واقعی پروژه و بدون fallback پنهان
- Deadline outlook قطعی و Escalation deduplicated؛ Acknowledge هرگز Resolution نیست
- طبقه‌بندی و Permission fail-closed برای رکوردهای محدود مدیریتی، HSE و تجاری
- نقش `ProjectController` و رابط فارسی Online-only برای چرخه‌های رسمی ریسک و تصمیم
- Offline Sync نسخه ۳ با Device/Lease/Session، Change Feed، Checkpoint و Conflict Center پایدار
- چرخه Recovery پایدار برای قطع/وصل، Crash، Retry محدود و تطبیق Local/Server
- Receipt تشخیصی بدون Payload برای Replay/Rejected و جلوگیری قابل اثبات از Duplicate
- سناریوی تعارض واقعی دو کاربر با Audit و Correlation قابل راستی‌آزمایی
- Release Identity تعبیه‌شده در Artifactهای API و Web با Commit، نسخه و زمان Build یکسان
- Pilot Gate fail-closed با هشت Evidence تاریخ‌دار، Hash گزارش و سه تأیید مستقل
- Smoke محیط برای جلوگیری از Deploy ترکیبی Web/API متعلق به Commitهای متفاوت
- تقویم هجری شمسی در تمام ورودی‌ها و خروجی‌های کاربر با ارقام فارسی و منطقه زمانی تهران
- Date Picker شمسی مشترک و Gate `audit:calendar`؛ نگهداری داخلی تاریخ روی ISO/UTC استاندارد
- Project Registry واقعی بدون شناسه نمونه، ایجاد Draft و فعال‌سازی ممیزی‌شده و نسخه‌دار
- Lifecycle guard برای جلوگیری از عملیات رسمی روی پروژه غیرفعال
- Location/LBS سلسله‌مراتبی با ROOT، بازنشستگی کنترل‌شده و شناسه پایدار تا Fact و Project State
- گیت سراسری قرارداد ۲۱۲ Endpoint و ۱۶۹ Mutation در CI
- گزارش صریح تطبیق Blueprint که قابلیت کامل، جزئی، باز و Gate محیط را از هم جدا می‌کند

## تصمیم‌های بنیادین

- WBS اختیاری است.
- Budget اختیاری است.
- Project Calendar اختیاری است؛ نبود آن با تقویم پیش‌فرض پنهان جایگزین نمی‌شود.
- HSE و Quality مستقل و اختیاری‌اند.
- Procurement نیز قابلیت مستقل است و می‌تواند `NotConfigured/NotEnabled/SetupRequired/Active/Suspended` باشد.
- AI هیچ محاسبه، تأیید یا تغییر رسمی انجام نمی‌دهد.
- خروجی AI فقط مشورتی، دارای ارجاع و نیازمند بازبینی انسان است.
- داده آفلاین تا پذیرش سرور رسمی نیست.
- Sync مبتنی بر Operation است و Last-write-wins عمومی ندارد.
- نبود داده رسمی با `NoData/InsufficientData` نمایش داده می‌شود و Health سبز مصنوعی ساخته نمی‌شود.
- حساب مشترک و دسترسی ضمنی وجود ندارد.

## Runtimeها

- .NET 10 LTS
- Node.js 24 LTS
- Next.js 16.3.3
- PostgreSQL 17 برای محیط توسعه

## اجرای Development

پیش‌نیاز: Docker Compose.

```bash
cp .env.example .env
docker compose up --build
```

سرویس‌ها:

- Web: `http://localhost:3000`
- API: `http://localhost:8080`
- API health: `http://localhost:8080/health`
- API liveness: `http://localhost:8080/health/live`
- API readiness: `http://localhost:8080/health/ready`
- API release identity: `http://localhost:8080/api/v1/release`
- Web release identity: `http://localhost:3000/release.json`
- MinIO console: `http://localhost:9001`
- Keycloak: `http://localhost:8081`
- Keycloak admin: `http://localhost:8081/admin`

## اجرای مستقیم

```bash
dotnet restore PMCS.slnx
dotnet build PMCS.slnx --no-restore
dotnet test PMCS.slnx --no-build

cd src/web
npm install
npm run check
```

## هویت Development

برنامه وب حتی در Development از Keycloak و BFF استفاده می‌کند. User نمونه `demo@pmcs.local` در ورود نخست ملزم به تغییر رمز و فعال‌سازی OTP است؛ رمز اولیه از `.env` خوانده می‌شود.

برای تست مستقیم API در محیط Development فقط، Adapter هویت نمونه headerهای زیر را می‌پذیرد:

- `X-Tenant-Id`
- `X-User-Id`
- `X-Device-Id` برای Sync

این Adapter در Production هیچ هویتی ایجاد نمی‌کند. API در Production فقط Access Token صادرشده توسط Keycloak را با signature، issuer، audience و expiry معتبر می‌پذیرد. Role داخل Token مجوز نمی‌سازد؛ IdentityAccess داخلی مرجع Tenant، Membership و Operation است. قرارداد کامل در `docs/security/production-authentication.md` آمده است.

## مستندات

- Roadmap قطعی توسعه و Qualification: [`docs/roadmaps/pmcs-v1-development-and-qualification.md`](docs/roadmaps/pmcs-v1-development-and-qualification.md)
- تصمیم‌های معماری: [`docs/adr`](docs/adr)
- API و قراردادهای توسعه: [`docs/api`](docs/api)
- پایه Permission: [`docs/security`](docs/security)
- Runbookها: [`docs/runbooks`](docs/runbooks)
- احراز هویت Production: [`docs/security/production-authentication.md`](docs/security/production-authentication.md)
- انتشار Pilot: [`docs/runbooks/pilot-release.md`](docs/runbooks/pilot-release.md)
- ممیزی جامع Blueprint و یکپارچگی: [`docs/audits/system-integrity-traceability-2026-09-13.md`](docs/audits/system-integrity-traceability-2026-09-13.md)
- قرارداد Project Setup و Location: [`docs/api/project-setup-and-locations-v1.md`](docs/api/project-setup-and-locations-v1.md)

Blueprint محصول خارج از کد نگهداری می‌شود و Repository باید در هر Vertical Slice با Acceptance Criteria آن هم‌راستا بماند.
