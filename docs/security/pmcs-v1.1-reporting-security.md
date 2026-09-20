# PMCS V1.1 — Reporting Permission، Classification و Threat Contract

- شناسه: `PMCS-SEC-RPT1-001`
- نسخه: `1.12.1`
- وضعیت: F01/F02/F03 connected؛ F04 semantic security contract passed in Run 158؛ Runtime/Production disabled
- Checkpoint: `V1.1-RPT1`

## ۱. اصل دسترسی

گزارش یک راه میان‌بر برای دیدن داده نیست. Actor فقط وقتی Definition، Run، Snapshot metadata یا
Output را می‌بیند که هم Permission مربوط به Reporting و هم Permissionهای Source همان Definition
را در Scope جاری داشته باشد.

سه ارزیابی مستقل لازم است:

1. هنگام Create Run؛
2. هنگام پردازش Worker؛
3. هنگام View/Download/Verify.

Permission snapshot فقط Evidence است و به Token دائمی تبدیل نمی‌شود.

## ۲. Role mapping اولیه

| Role | Catalog | Create Run | Download | Publish Template |
| --- | --- | --- | --- | --- |
| Tenant Administrator | Allow | Allow | Allow | Allow |
| Project Manager | Allow | Allow | Allow | Deny |
| Project Controller | Allow | Allow | Allow | Deny |
| Technical Office | Allow | Allow | Allow | Deny |
| Site Supervisor | Allow | Allow فقط گزارش‌های Source مجاز | Allow | Deny |
| Finance Manager/Operator | Allow فقط Definitionهای Source مجاز | مطابق Source | مطابق Source | Deny |
| Procurement/Quality/HSE | Allow فقط Definitionهای Source مجاز | مطابق Source | مطابق Source | Deny |
| Observer | Catalog و Download خواندنی مطابق Source | Deny Create | Allow مطابق Source | Deny |
| Portfolio Viewer | Catalog/Run/Download فقط در Project scopeهای مجاز | Allow Definitionهای read-only | Allow | Deny |

Role mapping به‌تنهایی کافی نیست؛ Project membership فعال، Source permission، Classification و
هر Field-level restriction دوباره اعمال می‌شود. `reporting.*` مجوز `hse.confidential.read`،
`governance.sensitive.read` یا هر Permission حساس دیگری تولید نمی‌کند.

## ۳. Classification propagation

- Output حداقل Classification خود Definition را دارد؛
- بالاترین Classification Source/Field واردشده بر Output اعمال می‌شود؛
- فیلد بدون Permission به‌جای mask مبهم، طبق Template یا حذف می‌شود یا کل Definition برای Actor
  unavailable می‌شود؛ رفتار هر Definition ثابت و تست‌شده است؛
- Restricted output از generic Documents list/download حذف می‌شود؛
- Notification گزارش فقط metadata classification-safe دارد؛
- نام فایل نباید شرح حساس یا نام فرد HSE را افشا کند.

گزارش روزانه اولیه فقط داده‌ای را وارد می‌کند که `field.daily-reports.read` اجازه می‌دهد و Source
contract هیچ دادهٔ محرمانهٔ HSE/مالی یا فایل binary را ضمنی join نمی‌کند.

### ۳.۱ سیاست ثابت F02

گزارش هفتگی/ماهانه F02 نیز فقط Source رسمی Daily Report را با `field.daily-reports.read` مصرف
می‌کند. نبود این Permission کل Definition/Run/Output را unavailable یا denied می‌کند؛ حذف خاموش یک
روز یا Fact برای ساخت گزارش ظاهراً کامل مجاز نیست. Create/Retry به `reporting.run.create` و
Download/Verify به `reporting.output.download` نیز نیاز دارد و هر سه نقطه request، processing و
download دوباره ارزیابی می‌شوند.

Classification F02 بیشترین مقدار میان Definition، configuration و Sourceهای واردشده است. Source
بالاتر از `Internal` یا همان Classification را به Snapshot/Output propagate می‌کند یا در نبود مجوز
fail-closed می‌شود؛ Caller، Template و Renderer اجازه downgrade ندارند. F02 هیچ داده مالی، HSE
محرمانه یا Source خانواده‌های دیگر را ضمنی join نمی‌کند.

Runtime Core Checkpoint این مرز را با predicate صریح Tenant/Project/range/cutoff در Source، تطبیق
دوباره Scope و window در Builder، حذف stateهای غیررسمی و propagation `Internal/Confidential/Restricted`
پیاده می‌کند. Root/date یا current official تکراری، Source contract/version ناسازگار و Fact/lineage
نامعتبر failure پردازشی‌اند؛ به `NoData` یا redaction خاموش تبدیل نمی‌شوند.

Renderer Checkpoint علاوه بر contractهای schema/hash/cutoff، Snapshot را پیش از تولید bytes
fail-closed validate می‌کند و ordering مستقل از ترتیب collection می‌سازد. XLSX تمام textها را برای
prefixهای `= + - @` خنثی می‌کند، فرمول/Macro ندارد و unitهای متفاوت یا missing را هرگز ادغام
نمی‌کند. PDF فقط از runtime/font digest پین‌شده استفاده می‌کند و Narrative/Fact را به‌صورت text
رندر می‌کند؛ هیچ HTML/JS یا template code اجرا نمی‌شود. `NoData` و `NotConfigured` Sheetهای داده
را header-only نگه می‌دارند و عدد صفر ساختگی ندارند.
Source `4f68f57de2c2a79b654a19128894d9c89878ab65` این isolation و formula-safety را در Run 141 با
هر هشت Job سبز تأیید کرده است.

Checkpoint `S07-MS05` همان سه re-evaluation را برای F02 فعال می‌کند: Catalog/Create در request،
`reporting.run.create + field.daily-reports.read` در Worker و Source permission همراه
`reporting.output.download` در Download/Verify. Definitionها allowlisted هستند، parser هیچ field
اضافی نمی‌پذیرد و Project profile سروری با Tenant/Project/Time Zone/accepted-at pin و دوباره validate
می‌شود. TestHarness deny مربوط به Observer، replay/conflict و Download/Verify هر دو فرمت را پوشش
می‌دهد. Source `7fc55c167ad2159a31c895b32a52d78f47574df9` در Run 144 (`35498990050`) هر هشت Job و هارنس
متصل F02 را با `13/13` assertion پاس کرد. UI و Production defaults همچنان خاموش‌اند.

### ۳.۲ سیاست ثابت F03

F03 فقط Source رسمی Project State را با `project-state.read` مصرف می‌کند. نبود این Permission کل
Definition/Run/Output را deny می‌کند؛ حذف خاموش Attention یا trend point مجاز نیست. Create/Retry به
`reporting.run.create` و Download/Verify به `reporting.output.download` نیز نیاز دارند و هر سه نقطه
request، processing و download دوباره ارزیابی می‌شوند. `project-state.recalculate` هرگز بخشی از
اجرای F03 نیست.

Client فقط `{}` می‌فرستد و نمی‌تواند `snapshotId` یا Source مطلوب انتخاب کند. Source رسمی با
Tenant/Project و cutoff محدود می‌شود و Snapshotهای بعد از cutoff، Command Center joinهای جاری،
Finance/Commercial و dispositionهای mutable وارد F03 نمی‌شوند. Cross-tenant ID، calculation version
ناشناخته، lineage ناقص، hash conflict یا currency اثبات‌نشده failure امن هستند.

Classification F03 بیشترین مقدار میان Definition، Project/source configuration و Snapshotهای
واردشده و حداقل `Internal` است. Source contract باید classification را صریح صادر کند؛ نبود آن یا
Permission طبقه بالاتر کل Run را fail-closed می‌کند. متن Attention، Location و Source IDs در
filename، event یا diagnostic ثبت نمی‌شوند.

Renderer Checkpoint نیز payload و identity/hash/cutoff را پیش از تولید bytes دوباره validate می‌کند.
XLSX فقط cellهای text/number کنترل‌شده دارد، prefixهای `= + - @` را خنثی می‌کند و هیچ Formula/Macro
نمی‌سازد. PDF هیچ HTML/JS یا template code اجرا نمی‌کند و فقط از QuestPDF/font/image contract
پین‌شده استفاده می‌کند. Attention یا trend silently truncate نمی‌شوند؛ متن/row/page budget
non-transient و fail-closed است. Source `d9d7ddb17d222f3b53402f291bf3d0cb8a3f957f` این سیاست را در
Run 154 با هر هشت Job، Runtime/Renderer پایه را checkpoint کرد. Checkpoint متصل اکنون Source
permission را از allowlist ثابت هر Definition resolve می‌کند: F03 فقط `project-state.read` و F01/F02
فقط `field.daily-reports.read`. Catalog و فهرست Runها Definitionهای غیرمجاز را حذف می‌کنند؛
Get/Retry/Cancel/Download/Verify برای Run موجود permission جاری همان Source را دوباره بررسی می‌کنند و
سرویس read-only ابزارهای Reporting نیز Catalog/Run/Output metadata را با همین policy فیلتر و Definition
ناشناخته را fail-closed می‌کند. idempotent replay نیز پیش از این re-evaluation برگردانده نمی‌شود.
Worker هنگام ساخت Snapshot و دوباره
پیش از Storage، `reporting.run.create + project-state.read` را ارزیابی می‌کند. هارنس با Finance Manager
دارای Project State و فاقد Daily Report permission، عدم نشت F01/F02 و اجرای کامل F03 را می‌سنجد.
Source `40afeb37d7bf90e97a988cae141901e28d336516` در Run 156 هر هشت Job و `14/14` assertion امنیتی/
یکپارچگی F03 را پاس کرد. این اتصال Safe Checkpoint است و feature flag، license و Production defaults
خاموش می‌مانند.

### ۳.۳ سیاست ثابت F04

F04 فقط Source رسمی پیشرفت را با هر سه Permission `planning.progress.read`،
`planning.baselines.read` و `planning.milestones.read` مصرف می‌کند. نبود هرکدام کل Definition/Run/
Output را deny می‌کند؛ حذف خاموش entry، milestone یا curve point مجاز نیست. Create/Retry به
`reporting.run.create` و Download/Verify به `reporting.output.download` نیز نیاز دارند و هر سه نقطه
request، processing و download دوباره ارزیابی می‌شوند. Permissionهای write/review/configure برای
این گزارش read-only لازم نیستند و Reporting هیچ Command Planning را فراخوانی نمی‌کند.

Client فقط `{}` می‌فرستد و نمی‌تواند Baseline، cutoff محلی یا grid مطلوب را انتخاب کند. Source با
Tenant/Project/cutoff محدود و lifecycle Approval/Supersede مستقل ارزیابی می‌شود. status/target جاری،
endpoint `GET /planning/progress`، SQL یا DbContext مستقیم و `IProgressFactSource` بدون cutoff برای
F04 مجاز نیستند. lineage مبهم، target pin گمشده، Baseline هم‌پوشان، contract version یا
Classification ناشناخته failure امن‌اند و به NoData تبدیل نمی‌شوند.

Classification F04 بیشترین مقدار میان Definition، Project/configuration، Baseline و evidenceهای
واردشده و حداقل `Internal` است. Source بالاتر باید propagate شود یا کل Run deny شود. Narrative،
Evidence Reference، Review Comment، نام Actor و Factهای غیرپیشرفت وارد Snapshot نمی‌شوند و Source ID
یا عنوان حساس در filename/event/diagnostic ثبت نمی‌شود. این سیاست در `S07-MS10` فقط قرارداد است و
Candidate `f8829027c2ce073c207cd0e04a49c306b546c6a1` آن را در Run 158 با هر هشت Job checkpoint کرد؛
این گیت هنوز Runtime، Catalog، Worker یا Renderer F04 را فعال نمی‌کند.

## ۴. Threat model

| تهدید | کنترل |
| --- | --- |
| Cross-tenant/project IDOR | Tenant/Project predicate، current Actor و negative tests |
| Revocation پس از Queue | re-evaluation در Worker و download |
| SQL/Template injection | Catalog allowlist، parameter parser بسته، عدم دریافت SQL/code |
| Spreadsheet formula injection | نوشتن text امن برای prefixهای `= + - @` و عدم Macro |
| Stored HTML/script | Renderer هیچ HTML/JS کاربر را اجرا نمی‌کند؛ text encode می‌شود |
| Path/object-key traversal | object key فقط Documents adapter می‌سازد؛ Reporting key دریافت نمی‌کند |
| Hash substitution | Snapshot/output SHA-256، media/size/signature verify و immutable manifest |
| Retry duplicate | stable output identity، idempotency و unique constraints |
| Stale source | `asOf`، source revision/official timestamps و snapshot hash |
| Log leakage | code/correlation/duration only؛ no payload/query/token/object key |
| Public QR bypass | QR فقط شناسه verification است؛ endpoint همچنان Auth/Permission می‌خواهد |
| Decompression/zip bomb در XLSX | فقط Server renderer تولید می‌کند؛ download content محدود و hash شده است |
| Resource exhaustion | row/page/size/time budget، queue quota، rate limit و cancellation |

## ۵. Data minimization و Audit

- Snapshot فقط فیلدهای لازم Template را نگه می‌دارد؛
- Permission snapshot Role header یا Token خام ذخیره نمی‌کند؛
- IP، credential، provider secret و payload کامل در Audit نیست؛
- Audit create/process/retry/download/verify شامل Actor، Project، Run/Output، Template version،
  `asOf`, result، Correlation ID و hash است؛
- مشاهده و Download Output Restricted نیز Audit می‌شود؛
- System worker با actor type صریح اجرا می‌شود و هرگز به‌جای requester ثبت نمی‌شود.

## ۶. Retention و Privacy

- output و snapshot دارای Retention policy نسخه‌دار هستند؛
- Legal Hold از Shared Documents رعایت می‌شود؛
- hard delete endpoint در RPT1 وجود ندارد؛
- archive دسترسی را افزایش نمی‌دهد؛
- تغییر Profile/Role بعدی نام یا permission snapshot تاریخی را بازنویسی نمی‌کند؛
- Download پس از انقضای Membership یا Revocation fail-closed است.

## ۷. Negative gate اجباری

حداقل سناریوهای زیر باید در CI متصل پاس شوند:

- Tenant A با Output Tenant B؛
- Project A با Run Project B؛
- Actor دارای `reporting.run.create` ولی فاقد Source permission؛
- Actor دارای Source read ولی فاقد create/download؛
- Membership suspended/revoked میان Queue و processing؛
- Permission revoked پس از success و پیش از download؛
- generic Documents download برای `ReportOutput`؛
- تغییر `outputId/documentId/ownerId`؛
- template/parameter ناشناخته؛
- payload spreadsheet formula؛
- hash، size، media type یا object bytes دستکاری‌شده؛
- retry هم‌زمان و Worker crash-after-storage-before-commit؛
- QR/Verification بدون Session؛
- Agent Tool آینده با Permission کمتر از user یا تلاش privilege elevation.

## ۸. وضعیت کنترل‌های Source و Checkpointهای متصل

در commit `ef5d68e5d35b7f2b58ebd3da87b3b35dadf19173`، Generated Document با object key
server-generated، signature/size/SHA validation، read-after-write و exact-byte check منتشر می‌شود؛
generic Documents برای `ReportOutput` بسته می‌ماند و Download/Verify مالک/type/version/tenant/
project/classification/retention/manifest/hash را دوباره کنترل می‌کند. mismatch با پاسخ 502 و Audit
مجزا fail-closed است. XLSX macro/external link/formula تولید نمی‌کند و prefixهای خطرناک را text
می‌نویسد. تمام feature switchها default-off هستند.

Run 99 (`35381177208`) سناریوی هارنس را روی PostgreSQL/Object Storage واقعی اجرا و هر `13/13`
assertion را پاس کرد: Observer deny، replay/conflict، cross-project denial، integrity دانلود و PDF
license fail-closed. این Evidence جایگزین Gateهای باقی‌ماندهٔ بخش ۷ نیست: cross-tenant، revocation
میان Queue/processing/download، tamper/malformed object، retry هم‌زمان/crash window، verification
بدون Session و آزمون privilege elevation Tool هنوز باید به qualification متصل افزوده شوند.

Run 102 (`35383686315`) شش کنترل متصل دیگر را پاس کرد: Verify ناشناس، cross-tenant، منع generic
Documents content، suspend شدن Membership پیش از Download، metadata hash tamper با پاسخ 502/Audit
و Verify سالم پس از restore. Cancel نیز با Worker خاموش در شش assertion مستقل، idempotent و بدون
Snapshot/Output اثبات شد. هنوز revocation میان Queue/processing، object-byte/missing-object tamper،
دو Worker/crash window و Tool privilege elevation باز هستند.

Run 104 (`35390054888`) دو Worker واقعی، `SKIP LOCKED`، rollback پس از `SIGKILL`، stale lease و
crash-before/after-storage را پاس کرد. Worker instance ID در Audit ثبت می‌شود و crash-after-storage
همان Document با identity پایدار را reuse می‌کند؛ Duplicate Output/Document/Audit/Outbox ایجاد
نشد. pauseهای Qualification فقط در QA Gateway ایزوله و برای Run هدف فعال‌اند و در غیر این صورت
startup fail-closed است. revocation حین Worker، object-byte/missing/malformed tamper، orphan
inventory و Tool privilege elevation همچنان باز هستند.

Run 108 (`35393509764`) revocation حین Worker و object integrity را روی PostgreSQL/MinIO واقعی
پاس کرد. Worker پس از Rendering و بلافاصله پیش از Storage هر دو Permission را دوباره ارزیابی کرد؛
تعلیق Membership، Run را با snapshot ردشده و `reporting.permission.revoked` بدون انتشار
Document/Output fail-closed کرد. byte-tamper، missing و malformed object نیز Verify/Download را با
502 و Audit مستقل متوقف کردند و fixture در `finally` به byte اصلی بازگشت. orphan موقت
crash-after-storage inventory و پس از recovery صفر شد. sweeper/remediation Production و Agent Tool
privilege-elevation qualification همچنان باز هستند.
