# INT1 — Definition of Ready و قرارداد Foundation

- شناسه: `PMCS-V1.1-INT1-DOR-001`
- نسخه: `1.2.0`
- وضعیت: DoR مستقل با Run 645 (`36783866696`) و هشت Job سبز بسته شد؛ این سند به‌تنهایی Gate خروج INT1 را نمی‌بندد.
- Parent: `PMCS-V1.1-UX2-MS100-C1`، SHA آغاز `52a56f9967309c7961e132d68504c7c679159bcd`
- تصمیم لازم‌الاجرا: [ADR 0032](../adr/0032-int1-controlled-multi-provider-selection.md)

## شکاف نسبت به V1

`Pmcs.Modules.Intelligence` اکنون Advisory محدود، ContextAssembler با مجوز پروژه، Worker، اعتبارسنجی خروجی و اتصال مستقیم OpenAI دارد. این دارایی یک Gateway سه‌ارائه‌دهنده‌ای، Profile نسخه‌دار، سطح مدیریت مدل، Permission مستقل، fallback کنترل‌شده یا Tool Registry برای Agent نیست. `TenantAdministrator` فعلی در `ProjectPermissionService` به‌صورت گسترده مجوز می‌گیرد؛ این رفتار برای مجوزهای جدید INT1 قابل استفاده نیست. Role عمومی `admin` یا `ProjectManager` هیچ‌کدام مجوز مدیریت مدل ایجاد نمی‌کنند. مسیر تاریخی Advisory V1 و قرارداد `ADR 0011` تا زمان مهاجرت اثبات‌شده پابرجاست.

## مرز Context و جریان داده

`PMCS.Intelligence` مالک Profile، Catalog، Session/Request/Run، تصمیم fallback، lineage و Audit مربوط به مدل است. خواندن دادهٔ رسمی فقط از Application Contract ماژول مالک و از Tool Registry مجاز است. هیچ آداپتور Provider به DbContext، connection string، SQL، endpoint ابزار میزبانی‌شدهٔ Provider یا شبکهٔ داخلی PMCS دسترسی ندارد. مدل حق تغییر Business Truth ندارد. در INT1 فقط ابزارهای خواندنی مرجع Report Catalog، Report Status و Collaboration context پس از مجوز مستقل منبع و Scope فعال می‌شوند؛ هیچ Agent پرسش‌وپاسخ عمومی یا Action عملیاتی عرضه نمی‌شود.

## قرارداد Permission نسخه `pmcs-int1-permissions-v1`

| Operation | Scope | منشأ مجوز | پیش‌فرض |
| --- | --- | --- | --- |
| `intelligence.providers.manage` | Global | Grant مستقل مدیر ارشد سامانه، صادرشده خارج از نقش Tenant | Deny |
| `intelligence.profiles.publish` | Global | همان Grant مستقل؛ Credential، Policy، allowlist و rollback سراسری | Deny |
| `intelligence.profiles.select` | Tenant | تفویض صریح، نسخه‌دار و قابل‌لغو از مدیر ارشد به actor/tenant؛ فقط allowlist همان Tenant | Deny |
| `intelligence.catalog.read` | Global/Tenant | Grant مدیریت یا تفویض خواندن محدود و بدون Secret | Deny |
| `intelligence.tools.invoke` | Tenant + Project + Tool | مجوز کاربر جاری برای ابزار ثبت‌شده و منبع داده در لحظهٔ هر فراخوانی | Deny |

Grant مدیر ارشد در Store مستقل با Actor ID، Tenant مبنا برای انتساب هویت، scope، صادرکننده، زمان اعتبار و revocation ثبت می‌شود؛ از `TenantAdministrator`، `PortfolioViewer`، project wildcard یا token claim آزاد استنتاج نمی‌شود. Bootstrapping این Grant یک عملیات صریح و ممیزی‌شدهٔ استقرار است، نه اعطای خودکار به کاربران موجود. تفویض Tenant نمی‌تواند Provider، Credential، Global Policy یا مدل بیرون allowlist را تغییر دهد. تغییر/ابطال Grant، عضویت پروژه و Permission منبع در زمان فرمان و هر Tool call دوباره بررسی می‌شود. Preview نیز همان تصمیم واقعی را نشان می‌دهد. Cross-tenant حتی برای شناسه‌های موجود پاسخ امن می‌گیرد.

نخستین Grant سراسری فقط با `ops/intelligence/bootstrap-superadmin.sh` و تأیید صریح اپراتور پایگاه‌داده، پس از Migration و احراز فعال‌بودن حساب مقصد/اپراتور ایجاد می‌شود. این عملیات یک‌باره، تراکنشی، دارای Audit و انقضای ۹۰روزه است؛ هیچ حسابی در Migration یا startup خودکار ارتقا نمی‌گیرد. پس از آن صدور/ابطال Grant با Permission مستقل `intelligence.providers.manage`، revision، idempotency و audit در سطح مدیریت محدود انجام می‌شود.

## قرارداد داده و نسخه

- `ProviderCatalog`: شناسه و نسخه، adapter kind، endpoint HTTPS تأییدشده، secret reference بیرون DB و log، وضعیت فعال/غیرفعال، capability flags. Catalog و Model فعال تنها پس از validation قابل انتخاب‌اند؛ نبود credential = `Unavailable`.
- `Model`: شناسه ثابت Provider، نسخهٔ تنظیم، قابلیت‌های `toolCalling`/`structuredOutput`، سقف token و classification پشتیبانی‌شده. شناسه و endpoint از input کاربر در درخواست Run ساخته نمی‌شوند.
- `ExecutionProfile`: ID و revision تغییرناپذیر، use case، prompt/policy version، Tenant/Project scope، default/allowlist مرتب، capability لازم، maximum classification، context/output token، زمان، بودجهٔ مصرف، retry و fallback policy. انتشار نسخهٔ جدید و rollback با pointer اتمیک به نسخهٔ قبلاً منتشرشده است؛ Runهای قبلی تغییر نمی‌کنند.
- `Run`: correlation و actor/tenant/project، ID/revision Profile، Provider/Model version، Prompt/Policy version، state transition، Tool decision، fallback reason، usage/cost/latency و error code. Audit فقط metadata مجاز را نگه می‌دارد؛ Secret، prompt، context، پاسخ مدل و payload ابزار در log/telemetry ذخیره نمی‌شوند.
- State: `Requested → Validated → Running → Completed | Failed | Cancelled`، با ثبت timestamp و دلیل در هر گذار. Session/Conversation context کوتاه‌مدت، bounded و scoped است؛ حذف/انقضا/تغییر مجوز آن را نامعتبر می‌کند. پاسخ Structured Output در schema بسته و bounded سنجیده می‌شود و خروجی ناقص یا ابزار ناشناخته fail-closed است.

## ترتیب اجرا و Gateهای میانی

1. `INT1-MS01`: این DoR، permission matrix، Gap Analysis و آزمون معماری ثبت شوند. CI مستقل و parent SHA، PR Draft باقی بماند.
2. `INT1-MS02`: Store نسخه‌دار، migration و grant/delegation واقعی با آزمون‌های منفی؛ هیچ اعطای ضمنی به admin فعلی.
3. `INT1-MS03`: Gateway و سه adapter جدا، catalog/credential validation، contract suite مشترک و test اتصال واقعیِ اختیاری با نتیجهٔ صریح `Unavailable` در نبود credential.
4. `INT1-MS04`: Profile publish/select/rollback و سطح مدیریت محدود، سقف‌ها، data minimization، fallback هم‌سطح و lineage.
5. `INT1-MS05`: Session/Request/Run، registry ابزارهای read-only مرجع، per-call permission، audit/telemetry امن و خطا/لغو/timeout.
6. `INT1-MS06`: آزمون مستقل adversarial، تعویض سه مدل بدون تغییر Business Logic، cross-tenant/project، no-SQL، migration/restore، CI و checkpoint خروج INT1.

هر MS فقط با source و CI همان SHA، آزمون مربوط و ثبت وضعیت باز/بسته Safe می‌شود. Gate خروج INT1 مستلزم شواهد همهٔ بندهای ADR 0032 است؛ سبزشدن Build تنها کافی نیست. Credential واقعی در repository و CI عمومی ذخیره نمی‌شود. آزمون اتصال واقعی با Credential پیکربندی‌شده اجرا و نتیجهٔ آن ثبت می‌شود؛ نبود Credential موفقیت کاذب نیست.

## مرز خروج

Stage 2 Agent خواندنی کامل، RAG، Executive Intelligence UI، Draft/Controlled Write، Chat عمومی `@PMCS`، QA1 و قفل V1.1 در این Gate شروع نمی‌شوند. هیچ endpoint ناقص یا Profile فعال بدون Gate خروج INT1 در Production پیش‌فرض روشن نمی‌شود.

## ثبت شروع اجرا

DoR در Commit `241892fca22a995e31f922d35320eb771059d346` با CI شمارهٔ ۶۴۵ بسته شد. پس از آن Store Grant، Catalog و Profile نسخه‌دار، سه Probe Provider، Tool Registry و مسیر Reference Run پشت Feature Flag خاموش‌به‌پیش‌فرض در Commitهای افزایشی شاخه پیاده‌سازی شدند. قرارداد Runtime و Gateهای باقیمانده در [سند Runtime مرجع](pmcs-v1.1-int1-runtime-contract.md) ثبت است. این ثبت، MS02–MS06 یا خروج INT1 را Safe اعلام نمی‌کند.

## الحاقیهٔ تصمیم مالک محصول، ۲۰۲۶-۱۰-۰۱

[ADR 0033](../adr/0033-int1-v11-dormant-foundation-and-live-qualification.md)
ترتیب Gate را پس از بسته‌شدن این DoR اصلاح کرد. متن بالا قرارداد و ترتیب تاریخی
آغاز INT1 را ثبت می‌کند. برای V1.1، MS02–MS06 با fixture متصل و CI مستقل، سپس
Checkpoint محدودِ Foundation خاموش بررسی می‌شوند؛ پس از آن QA1 می‌تواند نسخهٔ بدون
Runtime فعال Agent را ارزیابی کند. آزمون اتصال واقعی با Credential و Qualification
نهایی `AGENT-S1` در Gate `AGENT-S1-LIVE` پس از Baseline V1.1 و پیش از Stage 2
الزامی است. هیچ MS یا QA1 صرفاً با این الحاقیه Safe نمی‌شود.
