# سیاست حاکم Version، Checkpoint و Baseline در PMCS

- شناسه سند: `PMCS-GOV-BASELINE-001`
- نسخه سند: `1.1.0`
- وضعیت: لازم‌الاجرا برای تمام تغییرات پس از PMCS V1
- تاریخ اجرا: ۱۴۰۵/۰۶/۲۶ (۲۰۲۶-۰۹-۱۷)
- Baseline مبدأ: `PMCS V1` با Commit منبع `26bf222d44634562ca7f3fc0931f3f8b79ca04a1`

## ۱. هدف

این سیاست تضمین می‌کند در هر لحظه مشخص باشد:

- محصول از کدام Commit و Baseline آغاز شده است؛
- چه Scopeای در حال توسعه است و چه چیزی خارج از Scope است؛
- کدام Checkpoint بسته یا باز است؛
- کدام تست و Artifact به کدام Commit تعلق دارد؛
- آیا یک نسخه فقط Feature Complete است یا واقعاً Qualified، Final و Locked؛
- مسیر Rollback، Maintenance و نسخهٔ بعد چیست.

عبارت‌های مبهمی مانند «آخرین نسخه»، «کد فعلی»، «main جدید» یا «تقریباً نهایی» مرجع Baseline محسوب نمی‌شوند.

## ۲. انواع مرجع

### Product Baseline

نسخهٔ رسمی و قابل استناد محصول با Commit، Artifact، Migration Ledger و Qualification Evidence مشخص.

### Locked Product Baseline

Product Baselineای که پس از Qualification نهایی قفل شده است. هیچ Feature جدیدی داخل آن Commit یا نسخه تزریق نمی‌شود. تغییر بعدی باید Patch یا Feature Line جدید باشد.

### Development Baseline

نقطهٔ شروع دقیق یک خط توسعه. این مرجع شامل Parent Product Baseline و Repository Start Commit است، اما به‌معنی Qualified یا Final بودن خروجی در حال توسعه نیست.

### Checkpoint Snapshot

ثبت immutable از Commit پایان یک مرحله، تست‌ها و Artifactهای همان مرحله. Checkpoint Snapshot برای Traceability است و عنوان Locked Product Baseline ندارد.

### Release Candidate

Candidate ثابت و قابل بازتولید که Feature Freeze شده و برای Qualification کامل ارزیابی می‌شود. هر تغییر Runtime پس از آن Candidate جدید می‌سازد.

## ۳. وضعیت‌های رسمی و معنی آن‌ها

| وضعیت | معنی مجاز | عبارت‌های ممنوع |
| --- | --- | --- |
| `Planned` | Scope و Roadmap ثبت شده؛ کد محصول شروع نشده | Complete، Ready |
| `Architecture Approved` | ADR/Contract/Permission/Test Plan تصویب شده | Feature Complete |
| `In Development` | پیاده‌سازی فعال است | Final، Qualified |
| `Feature Complete` | Scope کدنویسی کامل؛ Qualification هنوز باز است | Final، Locked |
| `Release Candidate` | Candidate ثابت برای Full Regression | Qualified بدون Evidence |
| `Qualified` | تمام Test Contract و Evidence معتبر است | Locked بدون Manifest |
| `Final` | نسخهٔ Qualified برای انتشار انتخاب شده | — |
| `Baseline Locked` | Commit و Artifact نهایی قفل و Hash شده‌اند | تغییر مستقیم Runtime |

## ۴. قانون مبدأ هر نسخه

هر نسخه باید این دو مرجع را جدا ثبت کند:

1. `parentProductBaselineCommit`: Baseline محصول قبلی؛
2. `repositoryStartCommit`: Commit واقعی که شاخهٔ توسعه از آن ایجاد شده است.

اگر پس از Source Baseline فقط اسناد و Evidence به Repository افزوده شده باشد، Runtime Parent همان Source Baseline می‌ماند و Repository Start Commit جدید جدا ثبت می‌شود. این دو نباید با یکدیگر اشتباه شوند.

برای V1.1:

- `parentProductBaselineCommit = 26bf222d44634562ca7f3fc0931f3f8b79ca04a1`
- `repositoryStartCommit = 0389b52cbd3385bdcc9f0e2a94411800389ae2fc`
- `developmentBranch = v1.1-development`

Commit شروع فقط Evidence قفل V1 را به Runtime Parent افزوده و Runtime محصول را تغییر نداده است. هیچ کدنویسی Runtime V1.1 تا بسته‌شدن `V1.1-G0` و ثبت CI همان Governance Candidate آغاز نمی‌شود.

## ۵. سیاست نسخه‌گذاری

PMCS از SemVer محصولی استفاده می‌کند:

- `MAJOR`: تغییر ناسازگار قرارداد، معماری یا مدل عملیاتی؛
- `MINOR`: قابلیت سازگار جدید؛
- `PATCH`: رفع خطای سازگار یا امنیتی بدون قابلیت محصولی جدید.

نمونه:

- `1.0.x`: نگهداری V1 قفل‌شده؛
- `1.1.0`: Extensibility/Collaboration/Reporting/UX؛
- `1.2.0`: بلوغ Report Builder و Intelligence integration؛
- `2.x`: ماژول‌های دامنه‌ای بزرگ و توسعهٔ قراردادهای عمده.

نسخهٔ Database schema، API contract، event contract، report template و Agent tool مستقل از نسخهٔ UI و Product قابل ردیابی است و نباید ضمنی فرض شود.

## ۶. Change Control قبل از کدنویسی

هر تغییر باید یک Change Record داشته باشد و در یکی از طبقات زیر قرار گیرد:

| نوع | مثال | مسیر |
| --- | --- | --- |
| `Clarification` | اصلاح متن بدون تغییر رفتار | Documentation review |
| `Defect` | رفتار برخلاف Contract مصوب | Patch یا Checkpoint جاری + Regression |
| `Additive Feature` | قابلیت سازگار جدید | MINOR Roadmap/ADR/DoR |
| `Breaking Change` | تغییر Contract/Data/Permission ناسازگار | MAJOR decision + migration plan |
| `Emergency Security Fix` | آسیب‌پذیری بحرانی | Patch ایزوله + expedited evidence + full retrospective |

هیچ درخواست شفاهی یا پیام گفتگو مستقیماً به کد تبدیل نمی‌شود. ابتدا Version، Checkpoint، Scope، Acceptance Criteria و اثر Regression آن ثبت می‌شود.

## ۷. Definition of Ready اجباری

قبل از اولین تغییر Runtime، موارد زیر باید کامل باشند:

- Change/Checkpoint ID؛
- Parent Baseline و Start Commit؛
- Scope و Non-Scope؛
- ADR و Dependency map؛
- Permission/Role/Classification؛
- Data ownership و migration strategy؛
- API/Event/Tool contract version؛
- Offline/Conflict semantics؛
- UX prototype برای تغییر UI؛
- Security/Privacy/Retention review؛
- Test matrix، negative test و regression scope؛
- rollout/rollback/observability؛
- مالک تصمیم محصول و Evidence reviewer.

## ۸. Checkpoint Manifest

هر Checkpoint بسته‌شده باید حداقل این داده‌ها را ثبت کند:

- Product/version/checkpoint ID؛
- Parent baseline commit؛
- Start و End commit؛
- Scope delivered و deferred؛
- Runtime/schema/API/event/template/tool versions؛
- Migrationهای افزوده‌شده؛
- Test suiteها، count، result و CI run URL/ID؛
- Artifact digest و Build identity؛
- known limitations و open risks؛
- rollback/restore evidence؛
- زمان و تصمیم بسته‌شدن.

Checkpoint با Worktree کثیف، Commit نامشخص، تست مخلوط چند Commit یا Artifact فاقد Hash بسته نمی‌شود.

## ۹. قواعد Branch و Integration

- Feature work از Development Baseline ثبت‌شده آغاز می‌شود؛
- هر شاخه فقط به یک Checkpoint/Change Record تعلق دارد؛
- Migration number/name باید یکتا و متعلق به ماژول مالک باشد؛
- Integration فقط پس از Unit/Contract/Architecture gate انجام می‌شود؛
- ادغام چند Feature بدون Integration Baseline مشترک ممنوع است؛
- Rebase/force-push روی Baseline یا Tag قفل‌شده ممنوع است؛
- Tag قفل‌شده باید Protected و Artifact آن immutable باشد؛
- Runtime code، docs و evidence مربوط به یک Candidate باید Commit lineage قابل اثبات داشته باشند.

## ۱۰. Migration و سازگاری داده

- Migrationهای Production forward-only و قابل ممیزی هستند؛
- تغییر مخرب با Expand → Migrate → Verify → Contract انجام می‌شود؛
- حذف Column/Table تا پایان compatibility window ممنوع است؛
- rollback Runtime نباید نیازمند از بین‌بردن دادهٔ جدید باشد؛
- قبل از Candidate، Upgrade از Baseline قبلی روی Backup واقعی/نماینده تمرین می‌شود؛
- Restore Drill مستقل از Migration test باقی می‌ماند؛
- دادهٔ V1 بدون تصمیم و migration صریح بازتعبیر نمی‌شود.

## ۱۱. Qualification Contract

هر MINOR/MAJOR release باید دست‌کم موارد زیر را از صفر روی Candidate ثابت اجرا کند:

- Architecture and repository contracts؛
- Backend build/domain/unit؛
- Database/API/integration/object storage؛
- Permission/Tenant/Project negative boundaries؛
- Web unit/lint/RTL/Jalali/production build؛
- real-browser UI/E2E و Offline؛
- Identity/OIDC؛
- Backup/Restore و migration from parent baseline؛
- Security tests مرتبط؛
- Suiteهای اختصاصی Feature جدید؛
- Full Regression تمام Baseline قبلی.

تمام Suiteها باید Commit کامل یکسان داشته باشند. گزارش مخلوط، تست ناقص، Run متوقف‌شده یا Evidence متعلق به Commit دیگر قابل قبول نیست.

## ۱۲. قفل Baseline

Baseline فقط وقتی قفل می‌شود که:

1. Scope و deferredها نهایی شده‌اند؛
2. Feature Freeze برقرار است؛
3. همهٔ Suiteهای قرارداد Qualification سبز هستند؛
4. Release identity در API/Web/Artifact یکسان است؛
5. Migration ledger و Backup/Restore evidence معتبر است؛
6. Qualification report ماشین‌خوان و انسانی تولید شده است؛
7. Source commit، artifact digest، run ID و زمان ثبت شده‌اند؛
8. هیچ Runtime change پس از Candidate وجود ندارد؛
9. وضعیت‌های `Qualified → Final → Baseline Locked` به‌ترتیب ثبت شده‌اند.

## ۱۳. Maintenance Line برای Baseline قفل‌شده

- Feature جدید روی نسخهٔ Locked ممنوع است؛
- Defect یا Security Fix از همان Baseline fork می‌شود؛
- Patch version مستقل، migration و changelog مستقل دارد؛
- Regression کامل اجرا می‌شود، مگر Emergency موقت با Risk Acceptance صریح؛
- Emergency bypass باید بلافاصله با Full Qualification و retrospective بسته شود؛
- Patch جدید Baseline قبلی را پاک یا بازنویسی نمی‌کند و Baseline مستقل می‌سازد.

## ۱۴. Roadmap Change Policy

Roadmap قابل تکامل است، اما تغییر آن نیز نسخه‌دار است:

- جابه‌جایی اولویت بدون تغییر معماری: MINOR document revision؛
- افزودن Scope به نسخه فعال: Impact Assessment و تصویب مجدد DoR؛
- حذف تصمیم قطعی یا تغییر مرز معماری: ADR جایگزین و MAJOR roadmap revision؛
- تغییر پس از Feature Freeze: Candidate لغو و Candidate جدید؛
- تصمیم Deferred نباید در کد به‌صورت قابلیت ناقص یا Flag پنهان باقی بماند.

## ۱۵. مرجع جاری

تا زمان ایجاد Development Baseline V1.1، وضعیت رسمی چنین است:

| مورد | مقدار |
| --- | --- |
| آخرین Locked Product Baseline | `PMCS V1` |
| Source commit | `26bf222d44634562ca7f3fc0931f3f8b79ca04a1` |
| خط برنامه‌ریزی فعال | `PMCS V1.1` |
| وضعیت V1.1 | `Planned / Governance` |
| Repository Start Commit V1.1 | `0389b52cbd3385bdcc9f0e2a94411800389ae2fc` |
| Runtime تغییرکرده برای V1.1 | خیر |
