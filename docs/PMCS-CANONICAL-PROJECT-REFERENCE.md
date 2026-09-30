# PMCS — Canonical Project Reference

- شناسه: `PMCS-CANONICAL-REF-001`
- نسخه: `1.144.0`
- آخرین کنترل: ۲۰۲۶-۱۰-۰۱
- وضعیت: `Authoritative working reference | V1 locked | UX2/MS100 Safe؛ INT1/QA1 باز`
- هدف: مرجع واحد Resume و کنترل انطباق؛ این سند جای Roadmap/ADR/Checkpoint را نمی‌گیرد، بلکه آخرین
  وضعیت معتبر آن‌ها را یکجا مشخص می‌کند.

## قاعده اعتبار و Supersession

ترتیب مرجع در صورت اختلاف چنین است: وضعیت واقعی Git tree و Runtime ← Evidence سبز CI ← آخرین سند
نسخه‌دار Governance/Roadmap/Checkpoint ← ادعاهای Conversation. هر ادعای قدیمی که با Evidence یا
تصمیم نسخه‌دار جدیدتر ناسازگار باشد `Superseded` است. «Done» فقط با کد/سند قابل ردیابی و Evidence
معتبر پذیرفته می‌شود.

## Current Baseline

| مورد | مرجع معتبر |
| --- | --- |
| Baseline قفل‌شده محصول | `PMCS V1 — Qualified | Final | Baseline Locked` |
| V1 source baseline | `26bf222d44634562ca7f3fc0931f3f8b79ca04a1` |
| خط فعال | `PMCS V1.1 — Development` روی `v1.1-development` |
| V1.1 repository start | `0389b52cbd3385bdcc9f0e2a94411800389ae2fc` |
| Stage مؤثر | `V1.1-UX2` بسته؛ Exact Next، DoR مستقل `V1.1-INT1` است؛ QA1 و قفل V1.1 جدا هستند |
| آخرین Source | MS100 print evidence correction `fd13282c7e71a77c5192761e127966467045f297`؛ tree `95a79963975b7a5afa783e1a1da7018883cea890`، Run 640 (`36772469486`) هشت Job سبز؛ Artifact `11124980874`، ZIP SHA-256 `f55bdc3d94d37fbe10fd0009131546e404236b7ca0f9f31a5ce6c55376f0040d` با ۲۵ Index/۲۹۶ PNG/۱۹۵ axe/۲۰ PDF معتبر و مرورشده |
| Current evidence-bearing source checkpoint | MS100 Source Run 640 هشت Job سبز؛ Artifact `11124980874` معتبر و مرورشده؛ docs Run 641 Attempt دوم و Run 642 هر دو هشت Job سبز؛ G5 و UX2 بسته |
| Source lineage | MS100 از Source پایه `b0ff48cc5e645a78ad20b49da9b4ac3c47f6baac` به اصلاح شاهد چاپ `fd13282c7e71a77c5192761e127966467045f297` و docs نهایی `c950ec0f495396a9503a40de825f6c6830f73041` با Commit/fast-forward رسید؛ بدون Reset/Force Push |
| Current safe checkpoint | `PMCS-V1.1-UX2-MS100-C1`؛ Source `fd13282c7e71a77c5192761e127966467045f297`، Run 640 هشت Job سبز؛ docs `c950ec0f495396a9503a40de825f6c6830f73041`، Run 642 هشت Job سبز؛ G5/UX2 بسته |
| UX2 predecessor safe checkpoint | `PMCS-V1.1-UX2-MS65-C1`؛ Source correction Run 474 و docs Run 475 هشت Job سبز |
| UX2 predecessor safe checkpoint | `PMCS-V1.1-UX2-MS64-C1`؛ Source Run 470، docs correction Run 472 هشت Job سبز |
| UX2 predecessor safe checkpoint | `PMCS-V1.1-UX2-MS63-C1`؛ Run 469 هشت Job سبز، `VX-G3` فقط در محدودهٔ Contract/Prototype پذیرفته |
| UX2 predecessor safe checkpoint | `PMCS-V1.1-UX2-MS62-C1`؛ جدول پُرداده و چاپ چندصفحه‌ای، Runهای 466/468 هشت Job سبز؛ مالک معیار چهارم را در سطح نمونه پذیرفت |
| UX2 predecessor safe checkpoint | `PMCS-V1.1-UX2-MS61-C1`؛ کنتراست Token و بستهٔ تصمیم، Runهای 461/462 هشت Job سبز |
| UX2 predecessor safe checkpoint | `PMCS-V1.1-UX2-MS60-C1`؛ فرم شمسی/فیلتر، Runهای 456/457 هشت Job سبز |
| UX2 predecessor safe checkpoint | `PMCS-V1.1-UX2-MS59-C1`؛ نمونهٔ هوشمندی مفهومی، Runهای 453/454 هشت Job سبز |
| UX2 predecessor safe checkpoint | `PMCS-V1.1-UX2-MS58-C1`؛ نمونهٔ گزارش/چاپ، Runهای 451/452 هشت Job سبز |
| UX2 predecessor safe checkpoint | `PMCS-V1.1-UX2-MS57-C1`؛ پیوست/مدرک، Runهای 448/449 هشت Job سبز |
| UX2 predecessor safe checkpoint | `PMCS-V1.1-UX2-MS56-C1`؛ Wizard تکثیر، Runهای 445/446 هشت Job سبز |
| UX2 predecessor safe checkpoint | `PMCS-V1.1-UX2-MS55-C1`؛ نمونهٔ پروفایل و فهرست شکاف G3، Runهای 443/444 هشت Job سبز |
| UX2 predecessor safe checkpoint | `PMCS-V1.1-UX2-MS54-C1`؛ تصمیم محدود مالک و اصلاح موبایل، Runهای 439/440 هشت Job سبز |
| UX2 predecessor safe checkpoint | `PMCS-V1.1-UX2-MS53-C1`؛ بستهٔ مرور مالک، Runهای 437/438 هشت Job سبز |
| UX2 predecessor safe checkpoint | `PMCS-V1.1-UX2-MS52-C1`؛ Login/Shell/Chart، Runهای 435/436 هشت Job سبز |
| UX2 predecessor safe checkpoint | `PMCS-V1.1-UX2-MS51-C1`؛ Overlay/ردیف موبایل، Runهای 433/434 هشت Job سبز |
| UX2 predecessor safe checkpoint | `PMCS-V1.1-UX2-MS50-C1`؛ بستهٔ بازبینی VX-G3، Runهای 431/432 هشت Job سبز |
| UX2 predecessor safe checkpoint | `PMCS-V1.1-UX2-MS49-C1`؛ Navigation/Feedback/Dialog، Runهای 429/430 هشت Job سبز |
| UX2 predecessor safe checkpoint | `PMCS-V1.1-UX2-MS48-C1`؛ وزیرمتن نسخهٔ 2.0.0، Runهای 425/426 هشت Job سبز |
| UX2 predecessor safe checkpoint | `PMCS-V1.1-UX2-MS47-C1`؛ نمونهٔ Foundation، Runهای 419/420 هشت Job سبز |
| UX2 predecessor safe checkpoint | `PMCS-V1.1-UX2-MS46-C1`؛ ماتریس State و Hover غیرفعال، Runهای 416/417 هشت Job سبز |
| UX2 predecessor safe checkpoint | `PMCS-V1.1-UX2-MS45-C1`؛ تمرین PDF/XLSX فونت، Runهای 414/415 هشت Job سبز |
| UX2 predecessor safe checkpoint | `PMCS-V1.1-UX2-MS44-C1`؛ Prototype/فونت مقایسه‌ای، Runهای 412/413 هشت Job سبز |
| UX2 predecessor safe checkpoint | `PMCS-V1.1-UX2-MS43-C1`؛ برگهٔ چاپ محدود و صادق، Runهای 409/410 هشت Job سبز |
| UX2 predecessor safe checkpoint | `PMCS-V1.1-UX2-MS42-C1`؛ Skeleton صادق Loading سبد، Runهای 406/407 هشت Job سبز |
| UX2 predecessor safe checkpoint | `PMCS-V1.1-UX2-MS41-C1`؛ Navigation موبایل و keyboard/۳۲۰ پیکسل، Runهای 404/405 هشت Job سبز |
| UX2 predecessor safe checkpoint | `PMCS-V1.1-UX2-MS40-C1`؛ FileInput فارسی Login، Runهای 402/403 هشت Job سبز |
| UX2 predecessor safe checkpoint | `PMCS-V1.1-UX2-MS39-C1`؛ کنتراست Wizard و VX-G1 Audit Complete، Runهای 400/401 هشت Job سبز |
| UX2 predecessor safe checkpoint | `PMCS-V1.1-UX2-MS38-C1`؛ ممیزی چاپ مرورگر، Runهای 398/399 هشت Job سبز |
| UX2 predecessor safe checkpoint | `PMCS-V1.1-UX2-MS37-C1`؛ خطای callback Login، Runهای 396/397 هشت Job سبز |
| UX2 predecessor safe checkpoint | `PMCS-V1.1-UX2-MS36-C1`؛ Preview مسدود تکثیر، Runهای 393/394 هشت Job سبز |
| UX2 predecessor safe checkpoint | `PMCS-V1.1-UX2-MS35-C1`؛ تعارض Revision پیام گروه پروژه، Runهای 388/389 سبز |
| UX2 predecessor safe checkpoint | `PMCS-V1.1-UX2-MS34-C1`؛ نشان ثابت، Sidebar مستقل، keyboard و عرض ۳۲۰، Runهای 386/387 سبز |
| UX2 predecessor safe checkpoint | `PMCS-V1.1-UX2-MS33-C1`؛ ۱۱ Route و ۳۸ screenshot، Run 380/381 سبز |
| UX2 predecessor safe checkpoint | `PMCS-V1.1-UX2-MS32-C1`؛ قرارداد مرکزی فونت، Run 377/378 سبز |
| UX2 predecessor safe checkpoint | `PMCS-V1.1-UX2-MS31-C1`؛ انتخاب پیوست RFI، Run 375 سبز |
| UX2 predecessor safe checkpoint | `PMCS-V1.1-UX2-MS30-C1`؛ انتخاب پیوست Issue، Run 373 سبز |
| UX2 predecessor safe checkpoint | `PMCS-V1.1-UX2-MS29-C1`؛ انتخاب پیوست Action، Run 371 سبز |
| UX2 predecessor safe checkpoint | `PMCS-V1.1-UX2-MS28-C1`؛ تبدیل تأییدشدهٔ Technical Document، Run 369 سبز |
| UX2 predecessor safe checkpoint | `PMCS-V1.1-UX2-MS27-C1`؛ تبدیل تأییدشدهٔ Evidence، Run 367 سبز |
| UX2 predecessor safe checkpoint | `PMCS-V1.1-UX2-MS26-C1`؛ تبدیل تأییدشدهٔ Daily Fact، Run 365 سبز |
| UX2 predecessor safe checkpoint | `PMCS-V1.1-UX2-MS25-C1`؛ تبدیل تأییدشدهٔ RFI، Run 363 سبز |
| UX2 predecessor safe checkpoint | `PMCS-V1.1-UX2-MS24-C1`؛ تبدیل تأییدشدهٔ Issue، Run 361 سبز |
| UX2 predecessor safe checkpoint | `PMCS-V1.1-UX2-MS23-C1`؛ تبدیل تأییدشدهٔ Action، Run 359 سبز |
| UX2 predecessor safe checkpoint | `PMCS-V1.1-UX2-MS22-C1`؛ مجوز تبدیل و lineage خواندنی، Run 356 سبز |
| UX2 predecessor safe checkpoint | `PMCS-V1.1-UX2-MS21-C1`؛ تاریخچهٔ خصوصی، Run 354 سبز |
| UX2 predecessor safe checkpoint | `PMCS-V1.1-UX2-MS20-C1`؛ تعدیل و Legal Hold، Run 352 سبز |
| UX2 predecessor safe checkpoint | `PMCS-V1.1-UX2-MS19-C1`؛ حذف نمایشی نویسنده، Run 350 سبز |
| UX2 predecessor safe checkpoint | `PMCS-V1.1-UX2-MS18-C1`؛ ویرایش پیام خود، Run 348 سبز |
| UX2 predecessor safe checkpoint | `PMCS-V1.1-UX2-MS17-C1`؛ اتصال سند Released، Run 345 سبز |
| UX2 predecessor safe checkpoint | `PMCS-V1.1-UX2-MS16-C1`؛ آپلود/قرنطینه، Run 343 سبز |
| UX2 predecessor safe checkpoint | `PMCS-V1.1-UX2-MS15-C1`؛ پیوست خواندنی و دانلود، Run 341 سبز |
| UX2 predecessor safe checkpoint | `PMCS-V1.1-UX2-MS14-C1`؛ سنجاق با مجوز تعدیل، Run 339 سبز |
| UX2 predecessor safe checkpoint | `PMCS-V1.1-UX2-MS13-C1`؛ واکنش‌های Chat گروه پروژه، Run 337 سبز |
| UX2 predecessor safe checkpoint | `PMCS-V1.1-UX2-MS12-C1`؛ دانلود F10، Run 334 سبز |
| UX2 predecessor safe checkpoint | `PMCS-V1.1-UX2-MS11-C1`؛ درخواست F10، Run 332 سبز |
| UX2 predecessor safe checkpoint | `PMCS-V1.1-UX2-MS01-C1`؛ نشان رسمی در Shell/Login، Run 307 سبز |
| COL1 predecessor safe checkpoint | `PMCS-V1.1-COL1-MS06-C1`؛ ساخت متصل و Qualification بسته، Run 305 سبز |
| RPT1 predecessor safe checkpoint | `PMCS-V1.1-RPT1-S07-MS43-C1`؛ F01 تا F10 End-to-End متصل، UX2/Production باز |
| Migration count | Safe Resume: `63` و Restore Drill سبز |

PMCS V1.1 هنوز `Feature Complete`، `Release Candidate`، `Qualified`، `Final` یا `Baseline Locked`
نیست. Baseline قفل‌شده V1 نیز باز نشده است.

## Effective Roadmap

| وضعیت | سند مؤثر |
| --- | --- |
| Active | `docs/roadmaps/pmcs-post-v1-product-evolution.md` — `PMCS-RM-POST-V1-001 v1.185.0` |
| Active program | `docs/roadmaps/pmcs-managerial-agent-seven-stage-roadmap.md` — `PMCS-RM-AGENT-001 v1.1.0` |
| Active program | `docs/roadmaps/pmcs-visual-excellence-program.md` — `PMCS-RM-VISUAL-001 v1.65.0` |
| Historical/Complete | `docs/roadmaps/pmcs-v1-development-and-qualification.md` |

ترتیب مؤثر V1.1: `G0 → UX1 → EXT1 → DOC1 → IAM1/PRJ1/RPT1/COL1 → UX2 → INT1 → QA1 → V1.1 Locked`.
وضعیت فعلی: G0، UX1، EXT1، DOC1، IAM1، PRJ1، G4 و G5/UX2 بسته؛ F01–F10 و COL1 متصل؛ INT1/QA1 و قفل V1.1 باز می‌مانند.

تصمیم مالک محصول برای INT1: انتخاب کنترل‌شدهٔ OpenAI/GPT، Google/Gemini و
Anthropic/Claude با Profile نسخه‌دار، Permission مستقل مدیر ارشد و تفویض صریح محدود،
سقف مصرف/داده، fallback هم‌سطح و audit lineage طبق `docs/adr/0032-int1-controlled-multi-provider-selection.md`
مصوب است. ثبت تصمیم، اجرای Runtime یا بسته‌شدن Gate INT1 محسوب نمی‌شود.

## آخرین تصمیم‌های مؤثر و Supersededها

| موضوع | وضعیت قبلی | مرجع مؤثر فعلی |
| --- | --- | --- |
| وضعیت V1 | `Feature Complete` یا Qualification در جریان | Superseded؛ V1 با Run 69 `Qualified | Final | Baseline Locked` است |
| Roadmap Post-V1 | نسخه‌های تا `v1.154.0` | Superseded؛ `v1.155.0` مرجع جاری است |
| انتهای Development 05 | توقف در RPT1/MS05 | Superseded؛ GitHub/CI پیشرفت معتبر تا `S07-MS33` را اثبات می‌کند |
| Agent مدیریتی | عنوان کلی یا پنج فاز | Superseded؛ دقیقاً هفت Stage مستقل با Gateهای مستقل |
| Reporting | Report Designer آزاد در V1.1 | Superseded/خارج از Scope؛ V1.1 فقط گزارش‌های استاندارد و تأییدشده، Designer در V1.2 |
| UI | بسته‌شدن UX1 یعنی پایان بازطراحی | Superseded؛ UX1 فقط جهت بصری «مدیریت ممتاز» را بست؛ مهاجرت کامل در UX2 است |
| PDF license | تصمیم ثبت‌نشده و `Unconfigured` | ADR 0030؛ `QuestPDF Community` برای Qualification، با default همچنان `Unconfigured` و بازاعتبارسنجی eligibility پیش از Production |
| کاتالوگ RPT1 | ابهام میان تکمیل ۹ خانواده یا کاهش Scope | ADR 0031؛ Scope هر ۱۰ خانواده حفظ شد، F02 تا F10 با Micro-Slice مستقل الزامی‌اند و RPT1 پیش از تکمیل آن‌ها بسته نمی‌شود |

تصمیم‌های پابرجا: Modular Monolith؛ داده رسمی فقط از state و Fact تأییدشده؛ Audit/Outbox/Idempotency؛
Permission و Tenant boundary؛ Offline و conflict semantics؛ Jalali/RTL در مرز UI؛ وضعیت‌های Operational،
Financial و Commercial مستقل؛ نبود داده هرگز صفر/سبز تلقی نشود؛ Agent فقط از Tool Registry مجاز و
Application Service استفاده کند و SQL/DB مستقیم نداشته باشد؛ Chat منبع حقیقت رسمی نیست؛ Login/Profile
و Project Bootstrap نسخه‌دار و محدود؛ Reporting یک Bounded Context مستقل؛ تمام flagهای RPT1 پیش‌فرض
خاموش و دسترسی به خروجی fail-closed باقی بماند.

مالک محصول در ۲۰۲۶-۰۹-۲۸ نشان کامل و نماد شفاف فعلی را به‌عنوان مبنای برند
تأیید کرد. تغییر آتی فونت فارسی باید در تمام UI فعال/Offline و PDF/Print از
قرارداد مرکزی نسخه‌دار ممکن باشد. در MS32 مسیر UI/Offline/Print/PDF/XLSX و assetهای
خودمیزبان از `assets/typography/pmcs-fonts.json` تولید و کنترل می‌شوند؛ جزئیات در
`docs/ux/pmcs-v1.1-typography-contract.md` است. در MS48 وزیرمتن با اختیار
واگذارشدهٔ مالک از مقایسهٔ MS44/45 برگزیده و در Candidate نسخهٔ `2.0.0`
برای Web/Offline/PDF/XLSX/Print متصل شد؛ Gate بصری `VX-G5` باز است.

UX2-MS33 inventory فعال ۱۱ Route و ۳۸ screenshot با manifest نسخه‌دار،
index/SHA-256 و Fixture مرورگر قابل بازتولید را در Run 380 تثبیت کرد. تصاویر
فرم، Offline، Loading/Failure، عدم دسترسی و حالت‌های پیش‌فرض خاموش را پوشش
می‌دهند. یافتهٔ P1 Sidebar دسکتاپ و Gapهای state/print در
`docs/ux/pmcs-v1.1-visual-audit.md` ثبت شده‌اند؛ `VX-G1/G3/G4/G5` بازند.

UX2-MS34 دسترسی به Sidebar بلند را در دسکتاپ با اندازهٔ ثابت نشان،
اسکرول مستقل و focus صفحه‌کلید اصلاح می‌کند؛ inventory به ۳۹ تصویر رسیده و
کنترل responsive عرض ۳۲۰ پیکسل افزوده شده است. Run 385 `.section-note` را عامل
overflow شناسایی کرد؛ Run 386 هشت Job سبز و آرشیو ۳۹ تصویر با SHA/ابعاد معتبر
و بازبینی قاب 04/39/21 دارد. CI مستندات Run 387 نیز هشت Job سبز شد؛
`VX-G1` همچنان باز است.

UX2-MS35 تصویر 40 تعارض Revision ویرایش پیام گروه پروژه را با
نمایش نسخهٔ فعلی سرور و حفظ پیش‌نویس نویسنده به همان E2E متصل می‌کند.
Run 388 هشت Job سبز و Artifact ۴۰تصویری با SHA/ابعاد معتبر و بازبینی تصویر
تعارض دارد؛ Run 389 مستندات نیز هشت Job سبز و MS35 Safe شد. Candidate بعدی
پیش‌نمایش مسدود تکثیر را با Capture 41 ثبت می‌کند؛ خطای Login و چاپ هنوز بازند.

UX2-MS36 source correction `9202660180e48a0124130123ae43fc1d6821597c`
در Run 393 هر هشت Job را پاس کرد. Artifact `10989402355` با digest
`sha256:c0269c2b570cd12a8d7f97fad24809955dbc8eec5f3edc5ce18a14eee0d8a8ef`
برای ۴۱ PNG از نظر index/SHA-256/ابعاد و Source/Run تطبیق شد؛ قاب 41
ردیف‌های افزودنی، تعارض و مسدود، تأیید و دکمهٔ اجرای غیرفعال را نشان می‌دهد.
CI مستندات Run 394 نیز هشت Job سبز شد و MS36 Safe است. Candidate MS37
حالت خطای callback هویت را با Capture 42 پیش از ورود واقعی OIDC ثبت می‌کند.
Correction `20e4a0e2ab64eb8893311cfc88daac6a60d07712` در Run 396 هر هشت
Job را پاس کرد. Artifact `10990146859` با digest
`sha256:36354a3f084a75b8fbd21454201e13b0df41dfba776b5873c42f4afd213e982a`
برای ۴۲ PNG از نظر index/SHA-256/ابعاد و Source/Run تطبیق شد؛ قاب 42 پیام
خطای فارسی و دکمهٔ ورود دوباره را در viewport نشان می‌دهد. CI مستندات هنوز
شرط پذیرش MS37 است.

CI مستندات MS37 در Run 397 هر هشت Job را پاس کرد و MS37 Safe شد. UX2-MS38
Source `8cdad07eb97a67f81e46f3e4bb0e34b0ace8a84b` در Run 398 هر هشت Job
را پاس کرد. Artifact `10990673834` با digest
`sha256:d4711cf4d416679014cc69b3824fc0967671a2c1c5f52f5a7d05fac31868aaa4`
۴۳ PNG، PDF چاپ مرورگر و index معتبر دارد. PDF A4 در ۲۲ صفحه، Navigation
بریده و کنترل‌های فرم صفحهٔ اول را آشکار می‌کند؛ این baseline ممیزی است،
نه Print System رسمی. CI مستندات شرط پذیرش MS38 است و Gateهای بصری بازند.

CI مستندات MS38 در Run 399 هر هشت Job را پاس کرد و MS38 Safe شد. UX2-MS39
Source `57d33dc05900f1a0a7994817b7cb84dabe34377e` در Run 400 هر هشت Job
را پاس کرد. Artifact `10992023150` با digest
`sha256:bd9c9d13854908142c0bc78e645f6ee122058d9c332f8d6f38534632e8485591`
۴۳ PNG، PDF همراه و index معتبر دارد؛ قاب 19 پس از اصلاح کنتراست خواناست.
ممیزی `VX-G1` پوشش ۱۱ Route/۳۰ State و Gapهای اولویت‌دار را ثبت کرد؛ پذیرش
Gate به CI مستندات MS39 وابسته است. `VX-G3/G4/G5` بازند.

CI مستندات MS39 در Run 401 هر هشت Job را پاس کرد و `VX-G1 Audit Complete`
پذیرفته شد. UX2-MS40 Source `9ea2a44f1574a6045ce47cecf51fbcf3d6c7e4e8`
در Run 402 هر هشت Job را پاس کرد. Artifact `10993615536` با digest
`sha256:22d894acbc312fc26251f5aafb3aaff4c7bc8a548a6291e78adb4a94afbe429d`
۴۴ PNG، PDF چاپ مرورگر و index معتبر دارد. قاب 18 حالت بدون فایل و قاب 44
نام فایل و حذف انتخاب را فارسی نشان می‌دهند. CI مستندات شرط پذیرش MS40 است؛
`VX-G3/G4/G5` و فونت/چاپ Qualified نشده‌اند.

CI مستندات MS40 در Run 403 هر هشت Job را پاس کرد و
`PMCS-V1.1-UX2-MS40-C1` Safe شد. UX2-MS41 Source
`d76a8645d9e784b71c2a167aa04d315f871762d7` / tree
`d90dc6ec188cec7ac52e39ed768641fc60c50181` راهنمای فارسی پیمایش
Navigation موبایل را در شش Shell فعال افزوده است. دسترسی با صفحه‌کلید و
کنترل overflow سند در عرض ۳۲۰ پیکسل در E2E سنجیده می‌شود. Run 404 هشت
Job سبز و Artifact `10993184623` با digest
`sha256:2d75917180d3f2d5474426afcec050e6175dcdec85491594f14acec299747edc`
از نظر ۴۴ PNG/PDF، SHA/ابعاد و Source/Run معتبر است؛ قاب‌های 20–23 بازبینی
شدند. CI مستندات شرط پذیرش MS41 است؛ جایگزین کامل
Responsive، فونت تازه و Qualification چاپ در `VX-G3/G4/G5` بازند.

CI مستندات MS41 در Run 405 هر هشت Job را پاس کرد و
`PMCS-V1.1-UX2-MS41-C1` Safe شد. UX2-MS42 Source
`41b5eeb06dedab3e9d7e4b2f7ee52ffeba8e44af` / tree
`010814c9f03923dad2189e238b549dcfedd84dfe` چیدمان خنثای Skeleton
را فقط برای انتظار اولیهٔ Portfolio نشان می‌دهد، بدون عدد یا Fact ساختگی.
اعلان متنی Loading برای فناوری کمکی باقی است و شکل‌ها از آن پنهان‌اند.
سناریوی مرورگر قاب 26 و حذف Placeholder پس از خطا را کنترل می‌کند؛ Run 406
هشت Job سبز و Artifact `10995736502` با digest
`sha256:a6cfa48ae684a52278c92bbf41124d67b893f99c628bb7b44009ec0e464a97b7`
از نظر ۴۴ PNG/PDF، SHA/ابعاد و Source/Run معتبر است؛ قاب‌های 26/27
بازبینی شدند. CI مستندات شرط پذیرش MS42 است. `VX-G3/G4/G5`، فونت
فارسی تازه و Print System بازند.

CI مستندات MS42 در Run 407 هر هشت Job را پاس کرد و
`PMCS-V1.1-UX2-MS42-C1` Safe شد. UX2-MS43 Source
`f9847db38e79bce7c7986f61fcdc5a628a407422` / tree
`a60ff1a03da6c78c69fa9ef3a0623244a24a83fa` برگهٔ چاپ مرورگر
مرکز فرمان را فقط با دادهٔ تصویر وضعیت مجاز یا نبود آن نشان می‌دهد؛ Sidebar،
فرم و کنترل تعاملی از چاپ حذف می‌شوند. قرارداد مرز Print System در
`docs/ux/pmcs-v1.1-print-system-contract.md` است. Run 408 هشت Job سبز و
PDF یک‌صفحه‌ای A4 داشت، اما بازبینی متن، پیام «تصویر رسمی دریافت شد» را
هم‌زمان با نبود Snapshot یافت. Correction
`ce179fd6ffadc0f90a203c522dbea1c106e67765` / tree
`ca08e1894453d922bf7885b4c52bc690a1385b75` متن را صادق کرد؛ Run
409 هشت Job سبز و Artifact `10996804117` با digest
`sha256:636f371259b6a888a09bbbdce5d52825d06983b4271bc2ab98a87a1b8a4c9761`
از نظر ۴۴ PNG/PDF، SHA/ابعاد و Source/Run معتبر است. PDF یک صفحهٔ A4،
فارسی قابل استخراج و بدون Navigation/فرم یا پیام متناقض است؛ قاب 43/PDF
بازبینی شدند. CI مستندات شرط پذیرش MS43 است. این برگه خروجی رسمی Reporting نیست و
`VX-G3/G4/G5` و فونت تازه بازند.

CI مستندات MS43 در Run 410 هر هشت Job را پاس کرد و
`PMCS-V1.1-UX2-MS43-C1` Safe شد. UX2-MS44 یک Prototype مستقل و دارای
برچسب دادهٔ فرضی برای ۱۰ سناریو × ۷ حالت و سه قلم مبنا/وزیرمتن/استعداد
افزود؛ فونت‌های OFL با Commit و SHA ثابت در پوشهٔ Review هستند و manifest
تولیدی تغییر نکرده است. Source `c58bf12830ab1bd36c5197cd5309860d8ac4c30d`
در Run 411 هر هشت Job را پاس کرد و Artifact آن بازبینی شد: لوگوی کامل روی
زمینهٔ تیره کوچک و کم‌خوانا بود. Correction
`efba632036098ca3e5dabab81ba5770c3561929e` / tree
`6e6de8594fda086a8b8ce0a6c12bcccac02b38f0` سطح روشن برای نشان رسمی،
برش رسمی نماد در Mobile و Capture مستقل Tablet را اضافه کرد. Run 412
(`36485197274`) هر هشت Job را پاس کرد؛ Artifact `10998892196` با digest
`sha256:bd9b537cd43437384f0320ba9cad84b23217b37ec51c5bc779f4031673d52203`
شامل ۱۱ فایل است؛ Source/Hash، ابعاد PNG، PDFهای یک‌صفحه‌ای A4 و قاب‌های
Desktop/Tablet/Mobile بازبینی شدند. CI مستندات شرط Checkpoint است. `VX-G3/G4/G5`،
انتخاب قلم نهایی و PDF/XLSX رسمی بازند.

CI مستندات MS44 در Run 413 هر هشت Job را پاس کرد و
`PMCS-V1.1-UX2-MS44-C1` Safe شد. UX2-MS45 Source
`43d263d836f719278b49ef4e983e36feda83f1da` / tree
`2deffd12cff01d5c4f88ab6170b98d91477b4a16` دو قلم TTF Regular/Bold
دارای Hash و OFL را فقط در QA ثبت و در فرآیند مجزای QuestPDF/Spreadsheet
نمونه ساخت. Run 414 (`36487255564`) هشت Job سبز و Artifact `10999743061`
با digest `sha256:401d64be76d8b2d4275d6091bbea9748fdd7f9245e46a82da447a990698d1406`
دارد. شش خروجی با Source/Hash تطبیق شدند؛ PDFهای A4 یک صفحه و PNGها از نظر
فارسی/اعداد/جدول بازبینی شدند؛ XLSXها با parser مستقل باز و RTL/نام قلم
در Style بررسی شد. این‌ها Renderer رسمی Reporting و Golden نیستند؛
`PdfLicense` تولیدی Unconfigured و manifest مرکزی بدون تغییرند. CI
مستندات شرط Checkpoint، `VX-G3/G4/G5` و انتخاب قلم بازند.

CI مستندات MS45 در Run 415 هر هشت Job را پاس کرد و
`PMCS-V1.1-UX2-MS45-C1` Safe شد. MS46 Source
`4d320c13e3a6f66c6c7d7d836891fcbbe14311b3` / tree
`2984d1db45a9a466b7ea3cee31e4afea3c5fa326` ماتریس
Stateهای Component مشترک و اصلاح محدود Hover دکمهٔ Disabled در Preview
مسدود را افزود. Run 416 (`36489364018`) هشت Job سبز و Artifact
`11001141954` با digest
`sha256:903dcb4bd7710b82a8c96acccb6c268bf0fb148c3e6502cbc257022a57de19b6`
دارد؛ ۴۵ PNG و PDF یک‌صفحه‌ای A4 با Source/Manifest/Hash/ابعاد تطبیق
شدند. قاب‌های 41/45 در Hover دکمهٔ Disabled از نظر پیکسل برابرند؛
آزمون Browser قفل Action و نبود Execute را هم Assert کرد. CI مستندات
شرط Checkpoint است؛ `VX-G3/G4/G5` بازند.

CI مستندات MS46 در Run 417 هر هشت Job را پاس کرد و
`PMCS-V1.1-UX2-MS46-C1` Safe شد. MS47 Source
`20b52c1e8f685b4efa1155c652512f3e63100538` / tree
`b27cc8be9d669eeacac5119b574a3a964830f4e3` نمونهٔ مستقل
و تعاملی Foundation Stateها را در ۹ حالت با Action/Field/Status/
Feedback/Table افزود. Run 419 هشت Job سبز و Artifact `11002615768`
با ۱۱ PNG/Source/Hash/ابعاد معتبر و تصویرهای نمایندهٔ بازبینی‌شده دارد؛
CI مستندات MS47 در Run 420 هشت Job سبز داشت و
`PMCS-V1.1-UX2-MS47-C1` Safe شد. MS48 Source Candidate
`218dbfff78b94e8f3ce897321136544d5ef3f630` / tree
`e4fa7ec1e393966bd01a1e1dd693863e7eef29bd` با فونت وزیرمتن و
Goldenهای PDF/XLSX نسخه‌دار ساخته شد. F05 پس از جابه‌جایی Lineage
به صفحهٔ اول، دو صفحهٔ A4 خوانا دارد. Artifact رسمی Backend ده PDF،
نه XLSX و ۲۶ PNG را با فونت embedded/Style ثبت می‌کند؛ Run 425
هر هشت Job را پاس کرد؛ Run 426 مستندات نیز هر هشت Job را سبز کرد و
`PMCS-V1.1-UX2-MS48-C1` Safe شد. MS49 Source Candidate
`7336fcae49505f0edfb78842316ba2bcf96b3094` / tree
`c66bb5b5832ff596c8d19be569397dc412610df4` نمونهٔ مستقل
Navigation جایگزین موبایل، پیام‌های داده/خطا/Permission/Offline و
Dialog تأیید نسخه را بدون اتصال عملیاتی می‌افزاید. Run 429 هر هشت
Job سبز و Artifact `11004643711` با ۱۱ PNG/Source/Hash/ابعاد معتبر
و تصویرهای نمایندهٔ بازبینی‌شده دارد؛ Run 430 مستندات هم هر هشت Job را
پاس کرد و `PMCS-V1.1-UX2-MS49-C1` Safe شد. MS50 Source
`2c2d2848dc5a174982daff2331d3f4093aee8ab5` / tree
`7ec5bf5b2c080635f83c7ee5d838c06dffa40204` ده قاب بدون دستکاری
از Artifactهای Runs 412/419/425/429 را با Manifest Hash و لینک نمونه‌ها
برای بازبینی مالک یکجا قرار می‌دهد؛ Run 431 هشت Job سبز و Artifact
`11005871839` با digest ZIP و سه قاب Desktop/Mobile/۳۲۰px معتبر و
بازبینی‌شده دارد؛ Run 432 مستندات هم هشت Job سبز و
`PMCS-V1.1-UX2-MS50-C1` Safe شد. MS51 Source
`215853f91c2bb9f4b2462ac4facc54520e65a1be` / tree
`5029652e405aefeeadac1c797ab9b1fd1c1a6fc4` یک Prototype
مستقل Popover/Drawer/Toast و fallback ردیف موبایل با هشت وضعیت داده
می‌سازد؛ Run 433 هشت Job سبز و Artifact `11005944021` با ۱۲ قاب
Hash-indexed معتبر/بازبینی‌شده دارد؛ Run 434 مستندات هشت Job سبز و
`PMCS-V1.1-UX2-MS51-C1` Safe شد. MS52 Source
`07e1b5d8cc9eac6dc1da30df9ce0eddd8fd56338` / tree
`d16dbd15a1b1043cd61e9e7a71f2118b557be5c2` نمونهٔ Login/Shell/
Chart با جدول جایگزین، حالت‌های بدون داده و Print A4 است. Run 435 هشت
Job سبز و Artifact `11007206344` با ۹ PNG/PDF تک‌صفحه‌ای A4 معتبر/
بازبینی‌شده دارد؛ Run 436 مستندات هشت Job سبز و
`PMCS-V1.1-UX2-MS52-C1` Safe شد. MS53 بستهٔ مرور مالک را در
`docs/ux/review/ms53/` با چهار قاب از MS50 و چهار قاب دست‌نخوردهٔ
MS51/MS52، Manifest منشأ و چهار معیار تصمیم آماده کرد. تأیید صریح
مالک و شکاف‌های Component برای `VX-G3` همچنان بازند؛ مهاجرت
`VX-G4` پیش از آن شروع نمی‌شود.
Run 437 و Artifact سه‌قابی معتبر، Run 438 مستندات هشت Job سبز و
`PMCS-V1.1-UX2-MS53-C1` Safe شد. مالک جهت بصری را با دو پیگیری
تراکم موبایل و متن فارسی پذیرفت. MS54 این تصمیم را با دامنهٔ محدود
ثبت و نمونهٔ مستقل MS52 را در دو عرض ۳۹۰/۳۲۰px به‌ترتیب ۱۱۲/۱۱۱px
کوتاه‌تر کرد. Run 439 هشت Job سبز و Artifact `11018211937` دو تصویر
Hash-indexed معتبر/بازبینی‌شده دارد؛ Run 440 مستندات هشت Job سبز و
`PMCS-V1.1-UX2-MS54-C1` Safe شد. این رأی، قبول کامل چهار معیار یا
`VX-G3` نیست. MS55 نمونهٔ مستقل Avatar/برش محلی/حریم خصوصی و
`PMCS-UX-VX-G3-CLOSURE-001` را برای شکاف‌های بعدی آماده می‌کند؛
Source Run 443 هشت Job سبز و Artifact `11019328744` با ۱۳ قاب
Hash-indexed معتبر/بازبینی‌شده دارد؛ Run 444 مستندات هشت Job سبز و
`PMCS-V1.1-UX2-MS55-C1` Safe شد. MS56 نمونهٔ مستقل Wizard تکثیر را
برای انتخاب، سیاست تعارض، Preview و تأیید محلی آماده می‌کند؛ Source
Run 445 هشت Job سبز و Artifact `11020397707` با ۱۴ قاب معتبر/
بازبینی‌شده دارد؛ Run 446 مستندات هشت Job سبز و
`PMCS-V1.1-UX2-MS56-C1` Safe شد. MS57 نمونهٔ مستقل وضعیت پیوست و
مرور منشأ مدرک را آماده می‌کند؛ Source Run 448 هشت Job سبز و Artifact
`11022376179` با ۱۵ قاب معتبر/بازبینی‌شده دارد؛ Run 449 مستندات هشت Job سبز و Checkpoint MS57 Safe شد. MS58 وضعیت‌های خروجی و برگه‌های چاپ A4/A3 را به‌صورت نمونهٔ مستقل آماده کرد؛ Run 450 هشت Job سبز/Artifact معتبر اما متن فنی انگلیسی داشت؛ correction Run 451 هشت Job سبز/Artifact `11023680355` با ۱۳ PNG/۵ PDF معتبر/بازبینی‌شده دارد؛ Run 452 مستندات هشت Job سبز و Checkpoint MS58 Safe شد. MS59 نمونهٔ مفهومی هوشمندی با منشأ، تازگی، عدم قطعیت، مجوز و تصمیم انسانی را آماده کرد؛ Run 453 هشت Job سبز/Artifact `11023743292` با ۱۵ قاب معتبر و بازبینی‌شده دارد؛ Run 454 مستندات هشت Job سبز و Checkpoint MS59 Safe شد. MS60 فرم شمسی و فیلتر را در ده وضعیت به‌صورت نمونهٔ مستقل آماده کرد؛ Run 455 هشت Job سبز ولی قاب ۳۲۰px متن فنی انگلیسی داشت؛ correction Run 456 هشت Job سبز/Artifact `11025098337` با ۱۶ قاب معتبر و بازبینی‌شده و Run 457 مستندات هشت Job سبز و Checkpoint MS60 Safe شد. MS61 Token کنتراست متن ثانویه و Reduced Motion/Focus را ممیزی و بستهٔ تصمیم چهار معیار را آماده کرد؛ Runهای 458/459 در آزمون نمایش معادل سفید شکست خوردند؛ اصلاح متن فارسی و آزمون در Run 460 هشت Job سبز ولی پیام بالای بسته مخفف انگلیسی داشت؛ اصلاح Run 461 هشت Job سبز/Artifact `11027117283` با سه قاب معتبر و بازبینی‌شده؛ Run 462 مستندات هشت Job سبز و MS61 Safe شد. MS62 چاپ/تراکم را با ۴۸ ردیف ساختگی، صفحه‌بندی نمایش و چهار چاپ A4/A3 آماده کرد؛ Source Run 466 هشت Job سبز و Artifact `11029742768` با هشت PNG/چهار PDF در مجموع ۲۴ صفحه معتبر و بازبینی‌شده دارد؛ Run 467 مستندات به‌دلیل inherited session در آزمون Focus شکست خورد؛ اصلاح محدود Run 468 هشت Job سبز و MS62 Safe شد. مالک پس از ارائهٔ بستهٔ MS62 به معیار چهارم پاسخ مثبت داد. `VX-G3` فقط پس از ممیزی قرارداد/نمونه و CI مستقل MS63 پذیرفته می‌شود؛ G4/G5 بازند.

## Completed & Verified Work

- V1: تمام Sliceهای توسعه و QA، Full Regression Run 69 و قفل Baseline تکمیل شده‌اند.
- V1.1: G0، UX1، EXT1، DOC1 (Run 83)، IAM1 (Run 88) و PRJ1 (Run 92) بسته‌اند.
- COL1 DoR مستقل روی source `56597133e6d85372b1d7c8e1b2940ade17bb97ef` با Run 280 و هر هشت Job سبز؛
  MS01 source `a09f52506158eaa69c8aa6692cc057c9704900b0` با Run 281 و MS02 source
  `005e25dad7925ea4d7d3f28a0b3b0b94a7af506d` با Run 283 و MS03 source
  `c47c7e4202b9c7616602bedda587bc24b75246ed` با Run 286 و هر هشت Job سبز است.
- COL1-MS05 شش تبدیل مالک رسمی را روی source `d9e327a3e04fb9d2ca22a403b6e84d612abfbaf3`
  / tree `5ab89f36cc93f832bbc752f3ce0c8416e18b1689` با Run 301 و هر هشت Job سبز بست؛
  MS05A/Action-Issue در Run 297 و MS05B/RFI-Technical Document در Run 299 نیز مستقل سبز شدند.
- COL1-MS06 روی source `856089b4070ef4c8720aa01a4289139a9a0e4adc` / tree
  `f98685e0dc45acdff075420d9d0a92408e3a7593` در Run 304 هر هشت Job را پاس کرد؛
  رقابت دو نقش برای یک مقصد، قطع دسترسی پس از تعلیق، شش owner، ۶۳ Migration و Restore متصل سبزند.
- COL1-MS06 checkpoint documentation روی `52f4a5191776d75b7915f0ddc36f3fb5151937d4` با Run 305 هر هشت Job را پاس کرد.
- UX2-MS01 نشان رسمی شفاف را بدون بازطراحی در Shell و Login Fallback روی source
  `16ade7062aef5090b1b6ebc2b9d9409d3db46e19` / tree
  `df807f044995ef6957089b4e699028d44f4e58ba` با Run 306 و هشت Job سبز متصل کرد.
- UX2-MS02 روی source `62bf1ef6c5c5b23478a0580786a4875b990f4ef8` / tree
  `f690245e91ac362fcc5abf290088718562e6955a` در Run 309 هر هشت Job را
  پاس کرد؛ Token/Focus/Reduced Motion و پیمایش موبایل به UI واقعی متصل شدند.
- UX2-MS03 روی source `282fc1726b332e4d060d4aae0c028017c00345ab` / tree
  `90351f1e7fb1691cb63366924ea648acfac4a2ec` در Run 313 هر هشت Job را
  پاس کرد؛ Chat خواندنی پروژه با Flag خاموش، 403، tombstone و موبایل فشرده در مرورگر واقعی تأیید شد.
- UX2-MS04 روی source `c09fd37ecd674fe888ca52c6e369d504d19d4802` / tree
  `b0fd1056967d91e052c16c6157e1d8aa0f34e6ed` در Run 315 هر هشت Job را
  پاس کرد؛ ارسال، Live، صف آفلاین با شناسه پایدار و ابطال عضویت در مرورگر واقعی تأیید شد.
- UX2-MS05 روی source `324e4f930e71fa072ded7b190eea935d468de74f` / tree
  `04a22d38ee4fe2181e58def8a7e61e2677c9af43` در Run 317 هر هشت Job را
  پاس کرد؛ جست‌وجو/Reply/read cursor Chat با مرز پروژه در مرورگر واقعی تأیید شد.
- UX2-MS06 روی source `5914b2184a15cbec0cc8c7224e05fecf8098fb19` / tree
  `094d693ddfdb01d8cc30d068cf5928d31ca667ef` در Run 319 هر هشت Job را
  پاس کرد؛ مرکز گزارش‌های خواندنی با Catalog/History مجاز و موبایل واقعی تأیید شد.
- UX2-MS07 روی source `70c547c2993c521246ff61e688b9ba98e28f70eb` / tree
  `ebf6ddd75ad5727f4924438861c70378318a2696` در Run 321 هر هشت Job را
  پاس کرد؛ درخواست استاندارد، هویت پایدار و Retry پس از Reload در مرورگر واقعی تأیید شد.
- UX2-MS08 روی source `89b6e378f25d5e7c470896c6c35013ccc8bb2dec` / tree
  `19841f9f92ac013bb8efb4199047473ec2d2a8a0` در Run 323 هر هشت Job را
  پاس کرد؛ F01 انتخاب گزارش‌های روزانهٔ تأییدشدهٔ همان پروژه با Revision
  و F02 دورهٔ هفتگی/ماهانهٔ شمسی با اعتبارسنجی مرز دوره در مرورگر تأیید شد.
- UX2-MS09 روی source `73b170aa011dc61f78a4de6f45c7a8747eb14721` / tree
  `140b9c99bb69b5c81a9dd7705f5364249f4588d9` در Run 326 هر هشت Job را
  پاس کرد؛ دانلود خروجی در مرز پروژه و Flag مستقل، اعتبارسنجی MIME/اندازه/هش
  و لغو دسترسی در UI و مرورگر واقعی تأیید شد.
- UX2-MS10 روی source `de22569ca12659fc96e3e3065d938a11dc5e3def` / tree
  `e4add5830df07ddc749b35fdc5a0599ee46667a3` در Run 328 هر هشت Job را
  پاس کرد؛ Catalog/History گزارش سبد فقط در دامنهٔ Tenant و با 403/Flag خاموش
  و رد Run پروژه‌ای در مرورگر تأیید شد.
- UX2-MS11 روی source `37cefe7bd76f2a1f4fa87fab03ecf07dfc05a69b` / tree
  `2178071f82d7d9a8c7d356ff76ff7b8b74c7da25` در Run 331 هر هشت Job را
  پاس کرد؛ درخواست F10 با Client ID پایدار و Retry قالب XLSX پس از Reload
  در مرورگر واقعی تأیید شد.
- UX2-MS12 روی source `6a5b0649d1709eb3d483331ffcec0ee52f998920` / tree
  `273511d67bc428d31e149e48e4630c45f465a3e3` در Run 334 هر هشت Job را
  پاس کرد؛ دانلود F10 در مرز Tenant با MIME/اندازه/هش و ابطال 403 در مرورگر
  واقعی تأیید شد.
- UX2-MS13 روی source `8cc8c54ad471481c6231a083baa4966b8ab9e28c` / tree
  `1e73b3aeb077c6336b3b42c942ff56188cd26b15` در Run 337 هر هشت Job را
  پاس کرد؛ واکنش‌های چهارگانه با شمارش، وضعیت کاربر، read-only، 403 و
  ماندگاری پس از Reload در مرورگر واقعی تأیید شدند.
- UX2-MS14 روی source `9185ab5bb65fb0af0c59fd1278aacd127c695486` / tree
  `400ee81a5c5c7bcf4d9faabe299820f24496539f` در Run 339 هر هشت Job را
  پاس کرد؛ مجوز تعدیل برای کنترل سنجاق، حالت خواندنی برای عضو عادی، Reload
  و قطع دسترسی در مرورگر واقعی تأیید شدند.
- UX2-MS15 روی source `a27f5b78a0bbc4cfc417e809f8d88330b224988e` / tree
  `b272a94a06a9781cb7038603643cf98c57b116c9` در Run 341 هر هشت Job را
  پاس کرد؛ پیوست Released همان پیام/پروژه با SHA-256، حذف کنترل روی tombstone
  و قطع دسترسی در مرورگر واقعی تأیید شد.
- UX2-MS16 روی source `56fdf8be44646901cc7d51b2f2c742e05fa4f9e7` / tree
  `c97c8e224b246e7255d910218aa21481304ef004` در Run 343 هر هشت Job را
  پاس کرد؛ آپلود نویسنده، وضعیت قرنطینه در مرز پیام و Reload/403 در مرورگر
  واقعی تأیید شد.
- UX2-MS17 روی source `ff11d7848e0f0fbab32ddd017b1f5bae0730e009` / tree
  `6c8c8cfe185248611b12ddc74cc572f6e89e787c` در Run 345 هر هشت Job را
  پاس کرد؛ اتصال Released با پاسخ سرور، بازخوانی فهرست و Reload در مرورگر
  واقعی تأیید شد.
- UX2-MS18 پس از source `8698034157666b36fc147aba52529cdc19af3bf4`
  و اصلاح `c740abebc5ba46f610a0365113cebfef7c90b216` / tree
  `c637ea3d5b2fead0c26c1519d9b0c3cd8976afe1` در Run 348 هشت Job
  را پاس کرد؛ ویرایش نویسنده با Conflict، Reload و 403 در مرورگر تأیید شد.
- UX2-MS19 روی source `a746981e2f0e1bac58d29880b49f3970bf1fe51c` / tree
  `aa6696686868c0c095eb5484911ad43b38b306fa` در Run 350 هشت Job
  را پاس کرد؛ حذف نمایشی نویسنده با Legal Hold، Conflict، Reload و 403 در مرورگر تأیید شد.
- UX2-MS20 روی source `f9bb46d655d8385c6581ec02fcf842c3526e69af` / tree
  `13859e54a56bc870e0e828b5f8ea173a7f4e39e8` در Run 352 هشت Job
  را پاس کرد؛ Redaction و Legal Hold با دلیل، Revision، Conflict، Reload و 403
  در مرورگر تأیید شد.
- UX2-MS21 روی source `04ecceea75bdf7964949b13b15279614bcf647d6` / tree
  `bbad3b5bebf4e132a59f0afe3d9c93bde86eecdf` در Run 354 هشت Job
  را پاس کرد؛ تاریخچهٔ مستقل نویسنده/ناظر، عدم نمایش برای خواننده و پاک‌سازی
  پس از 403 در مرورگر تأیید شد.
- UX2-MS22 روی source `ac2fe6e03496cc3f2805f1b96b5cca558d438cf5` / tree
  `b082f21a47a4a1f1bee5b2444892ac8e44e818d2` در Run 356 هشت Job
  را پاس کرد؛ مجوز مؤثر تبدیل و lineage خواندنی محدود با 403 در مرورگر تأیید شد.
- UX2-MS23 روی source `3ff5a49d9478a21ec6d2d2481c7af5c5c3caf80b` / tree
  `c8cbca067b87126ea100d085c52f05d23340fed6` در Run 359 هشت Job
  را پاس کرد؛ تبدیل تأییدشدهٔ Action با مسئول خود کاربر، Conflict/Reload
  و 403 در مرورگر واقعی و Retry با تست واحد تأیید شد. Run 358 ناموفق
  و با اصلاح تست superseded است.
- UX2-MS24 روی source `677176ec198fe8aca275e84c4a276a132a9609a3` / tree
  `f4b805cf9268256b5f722f7791db982f2d62c36a` در Run 361 هشت Job
  را پاس کرد؛ Issue عمومی با مسئول خود کاربر، جلوگیری سروری از تکرار،
  Conflict/Reload و 403 در مرورگر واقعی تأیید شدند.
- UX2-MS25 روی source `13a192fe490702a3e3632d52637baa11493e922a` / tree
  `02272e93bdc375790950db95e0ac60063c40896a` در Run 363 هشت Job
  را پاس کرد؛ پیش‌نویس RFI رسمی با پرسش، مخاطب، رشته، اثر و تاریخ شمسی،
  جلوگیری سروری از تکرار، Conflict/Reload و 403 در مرورگر واقعی تأیید شدند.
- UX2-MS26 روی source `a37e61313f775da914909f2db96361b2a89c1a23` / tree
  `2b182cd96807f68a478eea73f22b3f383c94d538` در Run 365 هشت Job
  را پاس کرد؛ واقعیت روزانه به گزارش Draft/Location فعال متصل، تعارض Revision
  گزارش و تکرار سروری در مرورگر واقعی و هارنس Integration تأیید شدند.
- UX2-MS27 روی source `41d610a5ffea4995a91389407be36958a1e71c0a` / tree
  `ee0e23ca1d308bfb2eaf7a7e948c9b18f253720c` در Run 367 هشت Job
  را پاس کرد؛ فایل Released همان پیام با Hash و نسخه به Evidence رسمی برای
  گزارش/Fact انتخاب‌شده تبدیل شد؛ استفادهٔ تکراری فایل، Reload و 403 تأیید شدند.
- UX2-MS28 روی source `6e7de6864669c66c4ae9043897aa003ab57dd414` / tree
  `47636c7b609d5fd1196af5d4258379e7bbaab850` در Run 369 هشت Job
  را پاس کرد؛ فایل Released به سند فنی Draft با Revision اولیه و تبار Hash/نسخه
  تبدیل شد؛ منع تکرار فایل، Conflict/Reload و 403 در مرورگر تأیید شدند.
- RPT1 Core/Generated Documents و PostgreSQL/MinIO: Run 99.
- Cancel/Security، دو Worker/Crash Recovery و Worker Revocation/Object Integrity: Runهای 102، 104 و 108.
- Capacity و connected load/poison/fairness: MS01/MS02، Runهای 110 و 113.
- Metrics/queue-age و OTLP/scrape/alert delivery: MS03-C1/C2، Runهای 117 و 120.
- Inventory/dry-run/remediation امن orphan با retention/legal hold/audit/idempotency: MS04، Run 123.
- Semantic cutoff و XLSX Golden قطعی برای `daily-report-certified/1.0.0`: MS05، Run 130.
- تصمیم Community، pin image/font و PDF Golden/visual/performance: MS06، Run 133.
- تصمیم صریح حفظ Scope ده‌گانه و الزام Micro-Slice مستقل F02 تا F10: S07-MS01، ADR 0031، Run 135.
- DoR و قرارداد معنایی گزارش هفتگی/ماهانه F02: S07-MS02، Run 137؛ Runtime/Renderer هنوز باز است.
- Checkpoint اسنادی S07-MS02: commit `42e607ee98e2bbdedafaec892c8af47b9f947aa1`، Run 138 هر هشت
  Job را سبز کرد.
- Runtime Core محدود F02: source `6fc28cf54a6df820c49a2365eab76e3550ae421a`، tree
  `5188dac79fe5187b319e6aa727da89163fa37c1b` و Run 139 با هر هشت Job سبز.
- Checkpoint Runtime Core F02: commit `d8fd4398309b08d1cdc90e26140c4581dc476636`، tree
  `5345fdfc0cf1b0663b9cb1fa3bb97a4b6abf7c9b` و Run 140 با هر هشت Job سبز.
- Renderer/Golden محدود F02: source `4f68f57de2c2a79b654a19128894d9c89878ab65`، tree
  `f4b592c72ea65974c00b936ca59c0428eb47f981` و Run 141 با هر هشت Job سبز؛ PDF/XLSX deterministic،
  Golden هفتگی، Monthly boundary و حالت‌های NoData/NotConfigured پاس شدند.
- Catalog/API/Worker متصل F02: source `7fc55c167ad2159a31c895b32a52d78f47574df9`، tree
  `d665fe4cdf29369f96ec0875bc6f1535db349d55` و Run 144 با هر هشت Job سبز؛ هارنس متصل `13/13`،
  Restore کامل ۴۴ Migration و Qualification برابر `7/7` پاس شدند.
- Checkpoint اسنادی F02: commit `a7b7e885c1e59de894100ade96444184692ab5d3`، tree
  `5a476abb23ad650cb7334f537583ee4fa7b3c628` و Run 145 با هر هشت Job، `355/355` تست C#،
  `62/62` تست Node، `139/139` تست Web و Restore ۴۴ Migration سبز.
- DoR و قرارداد معنایی Executive Project State F03: source
  `e3218555a38f7ba460558e51b4db3f8bc17fcd9c`، tree
  `1fe4cc804fdd078a71ff2633201c8c690447900e` و Run 146 با هر هشت Job، `355/355` تست C#،
  `63/63` تست Node، `139/139` تست Web، پنج browser scenario و Restore ۴۴ Migration سبز؛ Runtime
  هنوز پیاده نشده است.
- Runtime Core محدود Executive Project State F03: source
  `22d5b0f91edf8d733192fae0ba946c8538c63bca`، tree
  `eb5ea4253a7b80e8ab3320b9ccea747624f89790` و Run 148 با هر هشت Job، `377/377` تست C#،
  `64/64` تست Node، `139/139` تست Web، پنج browser scenario و Restore ۴۴ Migration سبز؛ Renderer و
  wiring همچنان بازند.
- Renderer/Golden قطعی Executive Project State F03: source
  `d9d7ddb17d222f3b53402f291bf3d0cb8a3f957f`، tree
  `58fb79b0ee3d7c9cfa11630635b8ebdfbcbce434` و Run 154 با هر هشت Job، `383/383` تست C#،
  `65/65` تست Node، `139/139` تست Web، پنج browser scenario و Restore ۴۴ Migration سبز؛
  Catalog/API/Worker wiring همچنان باز است.
- Catalog/API/Worker متصل Executive Project State F03: source
  `40afeb37d7bf90e97a988cae141901e28d336516`، tree
  `ae06285bf1a68fe2592dacc76c7d31cb291ab924` و Run 156 با هر هشت Job، `387/387` تست C#،
  `66/66` تست Node، `139/139` تست Web، پنج browser scenario، هارنس `14/14`، Restore ۴۵ Migration
  و Qualification `7/7` سبز؛ UI/Production همچنان خاموش است.
- Checkpoint اسنادی F03: commit `c59a2444f5d5dd70859441f27d05c23dea6c268e`، tree
  `8a7926ac11abb529a3887d1e8a2091f7aeedecbd` و Run 157 با هر هشت Job، `387/387` تست C#،
  `67/67` تست Node، `139/139` تست Web، پنج browser scenario، Restore ۴۵ Migration و
  Qualification `7/7` سبز.
- DoR و قرارداد معنایی پیشرفت فیزیکی F04: source
  `f8829027c2ce073c207cd0e04a49c306b546c6a1`، tree
  `2b784f135894092ef55bf7c7df201b1f03e0c77f` و Run 158 با هر هشت Job، `387/387` تست C#،
  `68/68` تست Node، `139/139` تست Web، پنج browser scenario و Restore ۴۵ Migration سبز؛ Runtime
  هنوز پیاده نشده است.
- Runtime Core محدود پیشرفت فیزیکی F04: source
  `deb1571ec66d820868e8f4b77b631471e3c8207c`، tree
  `9e41495a357480af03f1555ef640962ab863d332` و Run 163 با هر هشت Job، `412/412` تست C# شامل
  `25/25` case متمرکز F04، `69/69` تست Node، `139/139` تست Web، پنج browser scenario، Restore ۴۵
  Migration و Qualification `7/7` سبز؛ Renderer/Golden و wiring بازند.
- Renderer/Golden قطعی پیشرفت فیزیکی F04: source
  `6a717f10e4bff167ad7e2643313008f5afcc8264`، tree
  `995c7fae108bbb5265faa036f951036d36e7061e` و Run 167 با هر هشت Job، `418/418` تست C# شامل
  `31/31` case متمرکز F04، `70/70` تست Node، `139/139` تست Web، پنج browser scenario، Restore ۴۵
  Migration و Qualification `7/7` سبز؛ Catalog/API/Worker wiring باز است.
- Catalog/API/Worker متصل پیشرفت فیزیکی F04: source
  `4c48c03aad126a594e5328fc7995a72728ba2274`، tree
  `49f957729fdccb0397dd153b93135ce2eaddd68a` و Run 169 با هر هشت Job، `419/419` تست C#،
  `71/71` تست Node، `139/139` تست Web، پنج browser scenario، هارنس `15/15`، Restore ۴۶ Migration
  و Qualification `7/7` سبز؛ UI/Production همچنان خاموش است.
- DoR و قرارداد معنایی وضعیت مالی F05: source
  `72fa88349d01edd4c6455eb0af1aebfdeced8c35`، tree
  `d2722dd8fab797650ed0c9befb80df93fc0be135` و Run 171 با هر هشت Job، `419/419` تست C#،
  `73/73` تست Node، `139/139` تست Web، پنج browser scenario، Restore ۴۶ Migration و Qualification
  `7/7` سبز؛ Runtime هنوز پیاده نشده است.
- Runtime Core محدود وضعیت مالی F05: source
  `77ad46cbac12b899116516b0a58665ae888b3bf2`، tree
  `5c67523b0fbed8d521627fe406f74271a1bbdcfe` و Run 175 با هر هشت Job، `450/450` تست C# شامل
  `31/31` case متمرکز F05، `75/75` تست Node، `139/139` تست Web، پنج browser scenario، Restore ۴۶
  Migration و Qualification `7/7` سبز؛ Renderer/Golden و wiring بازند.
- Renderer/Golden قطعی وضعیت مالی F05: source
  `9ddf7f1d96324e7ffb22d2abec83071a6c087ec2`، tree
  `f873795dcb8893dc28f88d5e5fc8292c5201e1e4` و Run 178 با هر هشت Job، `456/456` تست C# شامل
  شش case Renderer/Golden تازه و `37/37` case متمرکز F05، `77/77` تست Node، `139/139` تست Web،
  پنج browser scenario، Restore ۴۶ Migration و Qualification `7/7` سبز؛ Catalog/API/Worker wiring
  باز است.
- Catalog/API/Worker متصل وضعیت مالی F05: source
  `6de1e9ac3b457426be5e50064d1767106cd50c39`، tree
  `a4a8e8e655c56d05da2be5d87e7b84a9bb9a7a1f` و Run 185 با هر هشت Job، `458/458` تست C# شامل
  `39/39` case متمرکز F05، `78/78` تست Node، `139/139` تست Web، پنج browser scenario، هارنس
  `15/15`، validator روی `394` فایل، Restore ۴۷ Migration و Qualification `7/7` سبز؛ UI/Production
  همچنان خاموش است.
- DoR و قرارداد معنایی قرارداد/خرید/تأمین F06: source
  `4c5d026466cb3f76f351221297540a0936337335`، tree
  `d9e8febc5f220f8d00eaff80926b00dec2ea0926` و Run 188 با هر هشت Job، `458/458` تست C#،
  `80/80` تست Node، `139/139` تست Web، پنج browser scenario، validator روی `394` فایل، Restore ۴۷
  Migration و Qualification `7/7` سبز؛ Runtime هنوز پیاده نشده است.
- Runtime Core محدود قرارداد/خرید/تأمین F06: source
  `177a1d89a07c23b2ae446218e98556cfbcf57a21`، tree
  `165cd1d451935f3cb94db7b9f5718678de00aca2` و Run 192 با هر هشت Job، `490/490` تست C# شامل
  `32/32` case متمرکز F06، `82/82` تست Node، `139/139` تست Web، پنج browser scenario، validator روی
  `402` فایل، Restore ۴۷ Migration و Qualification `7/7` سبز؛ Renderer/Golden و wiring بازند.
- Renderer/Golden قطعی قرارداد/خرید/تأمین F06: source
  `fb88b94d6949e7780f5f40aa567e2ea3f187a6e8`، tree
  `212c1193d249cf1120297920c59b4ea15cb80c07` و Run 196 با هر هشت Job، `496/496` تست C# شامل
  شش case Renderer/Golden تازه و `38/38` case متمرکز F06، `84/84` تست Node، `139/139` تست Web،
  پنج browser scenario، validator روی `405` فایل، Restore ۴۷ Migration و Qualification `7/7`
  سبز؛ Catalog/API/Worker wiring باز است.
- Catalog/API/Worker متصل قرارداد/خرید/تأمین F06: source
  `df3879dd8b17403787154a398cc114b27c7172bc`، tree
  `5483e684aaa220a32b3135ea0b2bb3b2136023be` و Run 202 با هر هشت Job، `497/497` تست C# شامل
  `39/39` case متمرکز F06، `85/85` تست Node، `139/139` تست Web، پنج browser scenario، هارنس
  `15/15`، validator روی `406` فایل، Restore ۴۸ Migration و Qualification `7/7` سبز؛ UI/Production
  همچنان خاموش است.
- Run 130: هر ۸ Job سبز، `329/329` تست C#، `51/51` تست قراردادی Node، `139/139` تست Web، پنج
  browser scenario، `13/13` Golden assertion و Restore کامل ۴۳ Migration.
- Evidence checkpoint معتبر: `docs/checkpoints/v1.1-rpt1-slice-07-ms22-candidate.md`.
- Anchor انتقال Run 132 (`35459192122`) روی commit `345d9d6fc2e661144a74e3001150c28d73a212c7`
  هر هشت Job را سبز کرد.

## Current In-Progress Work

`V1.1-RPT1` فعال است و Safe Resume Point قطعی فعلی آن `PMCS-V1.1-RPT1-S07-MS43-C1` است. روی این
Checkpoint، Catalog/API/Worker/OutputAccess و qualification متصل F01 تا F10 بسته شده‌اند.
Migration 48،
Definition/Template seed، strict empty-object API، Project profile pin، مجوز منبع definition-aware و
Worker/Renderer dispatch برای F03 در Run 156 qualify شده‌اند. تصمیم
`QuestPDF Community` در ADR 0030 ثبت و package/image/font digestها، PDF Golden متصل QA-only، visual
digest و performance budget در Run 133 qualify شده‌اند. `PdfLicense=Unconfigured` و
`Phase1Enabled/OutputAccessEnabled/WorkerEnabled=false` در defaults و `OrphanRemediationMode=Disabled`
حفظ شده‌اند. ADR 0031 انتخاب صریح مالک محصول برای حفظ Scope ده‌گانه را ثبت کرده است:
`RPT1-F01` تا `RPT1-F10` checkpoint متصل دارند؛ UI اختصاصی Reporting طبق UX2 و Production enablement بازند. قرارداد
`PMCS-RPT1-F02-SEMANTIC-001 v1.3.1` Runtime Core، Renderer/Golden و wiring checkpointed دارد:
identity/schema نسخه‌دار، Project configuration pin، period source/resolver، semantic Snapshot،
render request/model fail-closed و PDF/XLSX قطعی. Migration 44، Definition/Template seed، strict
API، Project profile pin و Worker/renderer dispatch با QA متصل در Run 144 تأیید شده‌اند. UI و feature
flag تازه‌ای وجود ندارد و production defaults خاموش‌اند. قرارداد
نسخه پایه `PMCS-RPT1-F03-SEMANTIC-001 v1.0.0` پارامتر Client خالی، انتخاب Snapshot رسمی تا cutoff،
currency و partial-state صریح، منع Composite Health و Golden matrix هفده‌سناریویی را تثبیت کرد و
Run 146 هر هشت Job را سبز کرد. نسخه `PMCS-RPT1-F03-SEMANTIC-001 v1.1.1` در Checkpoint `S07-MS07`
identity/schema نسخه‌دار، Project profile pin، Source contract و selector cutoff-aware در
ProjectIntelligence و semantic Snapshot builder را با Unit/contract test بست و Run 148 هر هشت Job را
سبز کرد. نسخه `PMCS-RPT1-F03-SEMANTIC-001 v1.2.1` در Checkpoint `S07-MS08`، Template/Renderer/Layout
identity، render model canonical و PDF/XLSX قطعی را بست و Run 154 هر هشت Job را سبز کرد. نسخهٔ
`PMCS-RPT1-F03-SEMANTIC-001 v1.3.1` این Runtime و Rendererها را از مسیر Catalog/API/Worker متصل و
در Run 156 checkpoint کرد؛ UI، feature flag تازه و Production enablement ندارد.
قرارداد پایه checkpointed `PMCS-RPT1-F04-SEMANTIC-001 v1.0.0`، Baseline رسمی،
Actual/Planned/Variance و S-Curve را تثبیت می‌کند. Client فقط `{}` می‌فرستد؛ lifecycle
Baseline/evidence تا cutoff، target snapshot، grid حداکثر ۳۶۶ نقطه‌ای، سه Permission خواندنی
Planning و Classification fail-closed قطعی‌اند و Run 158 هر هشت Job را سبز کرده است. endpoint زنده
Planning و lifecycle/target جاری برای تاریخچه کافی نیستند. نسخه checkpointed
`PMCS-RPT1-F04-SEMANTIC-001 v1.1.1` اکنون identity/schema، Contractهای نسخه‌دار FieldOperations و
Planning، selector lifecycle، calculator و semantic Snapshot builder را با Unit/contract test اضافه
کرده است. compatibility projection هر history غیرقابل‌اثبات را fail-closed می‌کند. Run 163 هر هشت
Job را سبز و Runtime Core را checkpoint کرده است؛ هیچ Migration، API، Catalog/Template seed، Worker
dispatch یا Renderer برای F04 در آن Checkpoint وجود نداشت. نسخه
`PMCS-RPT1-F04-SEMANTIC-001 v1.2.1` در Checkpoint `S07-MS12`، Template/Renderer/Layout identity،
render model canonical و PDF/XLSX قطعی را بست. نسخه `PMCS-RPT1-F04-SEMANTIC-001 v1.3.1` در
Checkpoint `S07-MS13`، Migration 46، Catalog/Template، strict API، Project profile pin، سه
Permission خواندنی Planning، Worker/Renderer dispatch و qualification متصل را روی همان قراردادها
بست و Run 169 هر هشت Job را سبز کرد. UI، feature flagها، license و Production enablement باز و
defaultها خاموش‌اند.
قرارداد checkpointed `PMCS-RPT1-F05-SEMANTIC-001 v1.3.1`، Cash Position را فقط از رکوردهای Posted
تا cutoff، تعهدات Approved و settlementهای immutable می‌سازد؛ Payable/Receivable و Aging چهار-bucketی
جدا هستند و Budget Baseline اختیاری lifecycle مستقل دارد. Client فقط `{}` می‌فرستد؛ چهار Permission
Finance/Budget و Classification حداقل `Confidential` قطعی‌اند. Runtime Core نسخه‌دار اکنون Contract
و compatibility source در Finance، selector lifecycle، calculator و semantic Snapshot builder را
دارد؛ history غیرقابل‌اثبات، currency/overlap/over-allocation و classification ناسازگار fail-closed
هستند. Run 175 Runtime Core را سبز کرد و Checkpoint `S07-MS16` نیز Template/Renderer/Layout
identity، render model canonical، PDF/XLSX deterministic و Goldenهای binary/visual/performance را
بست. Checkpoint `S07-MS17` با Migration 47، Catalog/Template، strict `{}`، Project profile pin، چهار
Permission منبع definition-aware و Worker/Renderer dispatch، این مسیر را End-to-End متصل کرد؛ Run
185 هر هشت Job و هارنس `15/15` را سبز کرد. defaultهای Production همچنان خاموش‌اند.

قرارداد checkpointed `PMCS-RPT1-F06-SEMANTIC-001 v1.3.1`، زنجیرهٔ Contract/Amendment/Request/
Order/Receipt/Service Acceptance را با lifecycle و cutoff مستقل تثبیت می‌کند. Client فقط `{}`
می‌فرستد؛ مبلغ/مدت مؤثر Contract، commitment سفارش، fulfillment، وضعیت تحویل و supplier count/rate
فقط از evidence رسمی، Party/Item snapshot و quantity basis نسخه‌دار ساخته می‌شوند. F06 هیچ عددی از
F05، Inventory/Stock، Invoice/Payment، RFQ/Tender، ranking یا AI تولید نمی‌کند. شش Permission
Commercial/Procurement/Supply و Classification حداقل `Confidential` قطعی‌اند. Runtime Core
نسخه‌دار اکنون Contract و compatibility source در Commercial، selector lifecycle، calculator و
semantic Snapshot builder را دارد؛ Item snapshot در زمان Issue پین و هر history غیرقابل‌اثبات
fail-closed می‌شود. Run 192 Runtime Core را سبز کرد. Checkpoint `S07-MS20` نیز
Template/Renderer/Layout identity، render model canonical، PDF فارسی/RTL سه‌صفحه‌ای، XLSX ده-Sheet
و Goldenهای binary/visual/performance را بست و Run 196 هر هشت Job را سبز کرد. Checkpoint
`S07-MS21` با Migration 48، Catalog/Template، strict `{}`، Project profile pin، شش Permission منبع
definition-aware و Worker/Renderer dispatch این مسیر را End-to-End متصل کرد؛ Run 202 هر هشت Job و
هارنس `15/15` را سبز کرد. defaultهای Production همچنان خاموش‌اند.

قرارداد checkpointed `PMCS-RPT1-F07-SEMANTIC-001 v1.0.0` در `S07-MS22` فقط مرز
Document/Revision، Transmittal، RFI و Submittal را با cutoff، history/completeness، status/reason،
دو Permission `technical.read` و `technical.confidential.read`، Classification حداقل
`Confidential` و Golden matrix ۲۷ سناریویی بست. `GET /technical-office/state` current و capped
است و Source Certified تاریخی نیست. Run 205 هر هشت Job را سبز کرد. F07 هنوز Runtime، Renderer،
Migration، Catalog/API/Worker و UI ندارد؛ گام بعد فقط Runtime Core محدود F07 است.

در `S07-MS23` Runtime Core نسخه‌دار F07 روی Application Contract مالک TechnicalOffice، خواندن
سازگار و bounded، selector/calculator، completeness manifest و Snapshot builder اضافه شد. برای
RFI/Submittal موجود، تاریخچهٔ میانی در مدل فعلی کامل نیست و Source آن بخش را
`InsufficientData` با count نامعلوم می‌دهد؛ Document/Transmittal دارای lineage معتبر مستقل
محاسبه می‌شوند. Run 211 همهٔ هشت Job را سبز کرد.

در `S07-MS24` قرارداد Template/Renderer/Layout F07، مدل render fail-closed، PDF چهار بخش RTL و
XLSX شش Sheet قطعی با Coverage مستقل اضافه شدند. `InsufficientData` بخش‌های تاریخی همراه با
علت و count نامعلوم نمایان است؛ `NoData` صفر اثبات‌شده می‌ماند. Golden PDF/XLSX و چهار visual
digest، bound و محافظت فرمول با Run 215، `518/518` C# و هشت Job سبز ثابت شدند. F07 هنوز
historical transition producer و Catalog/API/Worker ندارد.

در `S07-MS25` مالک TechnicalOffice زمان و sequence هر transition جدید RFI/Submittal را در ledger
کمینهٔ JSON ثبت کرد. Migration 49 forward-only و nullable است؛ legacy بدون history کامل هیچ
backfill حدسی ندارد و `InsufficientData` می‌ماند. Source رخدادهای تازه را با revision، وضعیت،
response/outcome و timestamp ذخیره‌شده در دقت میکروثانیه می‌سنجد. Candidate
`b2cc811e9202b49dd643972bde547c105fd9dc02`، tree
`1673d1b48ca41fd425199da9235ec87c712d81b2` و PR merge
`a5843ace11e1546a472e76633fc13354c0570e3c` با همان tree در Run 219 همهٔ هشت Job،
`522/522` C#، `92/92` Node، `139/139` Web، پنج browser scenario و Restore ۴۹ Migration را
سبز کردند. این گزاره وضعیت تاریخی MS25 است؛ اتصال مستقل F07 در MS26 بسته شد.

در `S07-MS26` Migration شمارهٔ ۵۰ Definition/Template نسخه‌دار F07 را منتشر کرد؛ Catalog و
Create/List/Get/Download/Verify با مجوز هم‌زمان `technical.read` و
`technical.confidential.read`، پروژه و cutoff پین‌شده، Worker source/snapshot و PDF/XLSX
متصل شدند. نقش `TechnicalOffice` به‌تنهایی confidential read ندارد؛ آزمون موفق با
`ContractAdministrator` واجد هر دو مجوز و آزمون deny برای نقش فاقد مجوز انجام شد. Candidate
`b7a44b35eb7f498bf4382990324e3253033c0284`، tree
`bb6ebad2d3435caf4e085a65e08a085a2271761f`، PR merge
`a771d7113286a06606d89c55c1894d181cc29406` با همان tree و Run 222
(`36305583760`) هر هشت Job، `523/523` C#، `94/94` Node، `139/139` Web، پنج مرورگر،
F07 connected `17/17`، Restore ۵۰ Migration و Qualification `7/7` را سبز کردند.

در `S07-MS27` قرارداد مستقل `PMCS-RPT1-F08-SEMANTIC-001 v1.0.0` برای دو section
Quality/HSE و مالک `QualitySafety`، سه permission whole-definition، cutoff و classification
fail-closed ثبت شد. Candidate `b72ab1c09376e0ec45b6b52a460660f4b807b15a`، tree
`14334896b2155f6ebefe612a29123bd73bce3760` و Run 224 (`36307100818`) هشت Job
و Full Node `98/98` را سبز کردند. Runtime/Renderer/wiring F08 هنوز بازند.

در `S07-MS28` Source مالک از ۱۴ دفتر QualitySafety با repeatable-read/bound،
linkage و طبقه‌بندی fail-closed و manifest نسخه‌دار ساخته شد؛ Reporting صرفاً از
Application Contract، Project profile pin و Snapshot سازنده استفاده می‌کند. Candidate
`36682da970d0489569ed62a1d02684b5d6c588e9`، tree
`2f44542a094a86d5782b01a31bc8092a97b71b39` و Run 231 (`36308573306`) هر هشت
Job را سبز کردند. Renderer/Golden و wiring F08 هنوز بازند.

در `S07-MS29`، PDF دو صفحه و XLSX چهار Sheet مستقل Quality/HSE با status/count و
classification مجزا، متن امن و نرخ حادثهٔ nullable به Golden قطعی pin شدند. Candidate
`68a8c49311d05480824f3c7ec58933510877c8e6`، tree
`14fe30b65d983c553cf3675dc202b682d90f5c9d` و Run 235 (`36309751881`)
هر هشت Job را سبز کردند؛ wiring F08 در MS30 باز است.

در `S07-MS30`، Migration 51 و Definition/Template مستقل F08 با سه permission
whole-definition به API و Worker متصل شدند. هارنس PDF/XLSX و DB `20/20` assertion،
Restore ۵۱ Migration و Run 237 (`36310745011`) هر هشت Job را سبز کردند. Candidate
`786f032e5ce91ceffa80599c4ce02ba23f30bb1d` با tree
`9adcf1558c1bcab8a40a242c49efdf4df816a2dc` است. F08 End-to-End بسته شد.

در `S07-MS31`، `PMCS-RPT1-F09-SEMANTIC-001 v1.0.0` پنج دفتر مستقل
Issue/Risk/Decision/Escalation/Action، Source مالک ActionControl، cutoff،
permission/classification، legacy incomplete و ۲۷ fixture پذیرش را تثبیت کرد.
Candidate `67b60c00779d51ea9479dee4d887c810d572d485`، tree
`400add67f58dc03b559c3f1420d186fb6cf5ec15` و Run 239 (`36318261047`)
هر هشت Job را سبز کردند. F09 فقط contract ready است؛ Runtime/Renderer/wiring
هنوز باز و Migration همچنان ۵۱ است.

در `S07-MS32`، ActionControl Source هشت‌دفترهٔ bounded/repeatable-read، پنج بخش
مستقل، cutoff/Project pin، digest/manifest و classification محافظه‌کارانهٔ Action
را به Snapshot builder محدود F09 داد. Candidate اصلاحی
`2000b965dc31e6a83f0b1366c9a203ae809cee03` با tree
`14e9b0c6bcc4c4a1b785e761c678e18f0d216a8c` در Run 242 (`36321908108`)
هر هشت Job را سبز کرد؛ C# `534/534` و Node `107/107`. Legacy Decision و سایر
transitionهای بی‌تاریخ count نامعلوم دارند؛ producer، Renderer و wiring هنوز بازند.

در `S07-MS33`، Renderer قطعی پنج صفحهٔ PDF و هفت Sheet XLSX برای پنج بخش F09
با Golden byte/visual و اعتبارسنجی Snapshot/Template/Classification ساخته شد.
Candidate `0701a44155d1955a1b8e27db01d561c370b6be6e`، tree
`db7347e04344394f219cd68257a23c27159fd200`، Run 245 (`36324102914`)
هر هشت Job را سبز کرد؛ C# `538/538` و Node `108/108`. Producer تاریخچه و
Catalog/API/Worker هنوز بازند و Migration ۵۱ باقی است.

در `S07-MS34`، producer مالک شش ledger زمان‌دار را برای رکوردهای تازه ثبت کرد.
Migration ۵۲ nullable ماند و legacy backfill نشد. Candidate
`79bc3c62f17711eaa61281f90d995852f9128147` با tree
`e32fb658426a74fcfb0f61a463ab9748d1b8b686` در Run 248 (`36327282876`)
هشت Job را سبز کرد؛ C# `540/540`، Node `109/109` و Restore Drill ۵۲ Migration.
Selector cutoff-aware و wiring هنوز بازند.

در `S07-MS35`، Source مالک ledger را در cutoff انتخاب و وضعیت جاری، revision و
sequence را اعتبارسنجی کرد. Legacy count نامعلوم دارد و hash هر دفتر از تصویر
cutoff است. Candidate `44bd46689bcb795534bcf059f42591cecdaa35ba`، tree
`d849b915a0157dd10812acb8271c8b64dd108c29`، Run 250 (`36329655993`)
هشت Job، C# `542/542` و Restore Drill ۵۲ را سبز کرد. Wiring مستقل باز است.

در `S07-MS36`، Migration ۵۳ Definition/Template F09، سه permission whole-definition،
strict `{}`، Project pin و Worker dispatch به Snapshot/Renderer پنج‌بخشی را متصل کرد.
Candidate `2bb925b8cf8531442c5136e55811a9f8c31db655` با tree `3085dc51931b2b7965521b7b18fe0b9bfa55ec70` در Run 252 (`36331528136`)
هشت Job را سبز کرد؛ هارنس متصل `18/18`، C# `542/542`، Node `110/110`،
Web `139/139` و Restore Drill ۵۳ موفق‌اند. F09 End-to-End بسته و F10 باز است.

در `S07-MS37`، قرارداد `PMCS-RPT1-F10-SEMANTIC-001 v1.0.0` Portfolio واقعی
با cohort permission، cutoff، currency جدا و ۳۰ سناریوی پذیرش تثبیت شد. Candidate
`bb17a37fa19b06d111443fad178c2589e376bb25`، tree
`82eba0ffebc1c631281a78727f4b2325a50f0ecb` در Run 254 (`36333343004`)
هشت Job، C# `542/542`، Node `114/114`، Web `139/139`، پنج مرورگر و Restore ۵۳
را سبز کرد. F10 Runtime/Renderer/wiring ندارد و MS38 زیرساخت Tenant-scope است.

در `S07-MS38`، Migrationهای افزایشی Documents و Reporting، Scope Portfolio
با ProjectId تهی، owner سند Tenant، قید SQL و deny عمومی Documents را متصل کردند.
Worker پروژه‌ای فقط scope Project را claim می‌کند. Candidate
`ea5215e26ba382bdecd756adec1083dd3ef28d06`، tree
`63e5542679e9d0427c6ec96ad7fa3404810f340d` در Run 256 (`36335141253`)
هشت Job، C# `544/544`، Node `117/117`، Web `139/139`، پنج مرورگر و Restore
۵۵ Migration را سبز کرد. F10 Source/Renderer/wiring باز است.

در `S07-MS39`، Source فقط cohort مجاز و mask مستقل F05/F06 را از permission می‌گیرد
و selectorهای مالک F03/F05/F06 را در cutoff مصرف می‌کند. Snapshot Portfolio ارزها را
مستقل، وضعیت ناقص و manifest نسخه‌دار را ثبت می‌کند. Candidate نهایی
`c45b72be7bc913a6dc65b656bea0188f7da0dc8c`، tree
`00ad037a2eec41c2d5dbacfe0c186e04c4584cce` در Run 261 (`36338179481`)
هشت Job، C# `549/549`، Node `120/120`، Web `139/139` و Restore ۵۵ را سبز کرد.
Renderer و wiring F10 بازند.

در `S07-MS40`، PDF/XLSX فقط Snapshot و manifest immutable را پس از replay
semantic، template، hash و classification render می‌کنند. Golden PDF سه صفحه،
XLSX شش sheet و مقادیر نامعلوم/ارز مستقل پین شدند. Candidate `d57f73611e647d352ec5c59a19cd996e589d0fe2`، tree
`52daac29eff755e3a95c132d581fa7de37ef6387` در Run 264 (`36340600926`) هشت Job، C# `552/552`، Node `123/123`،
Web `139/139`، Restore ۵۵ و Qualification `7/7` را سبز کرد. Catalog/API/Worker
F10 هنوز باز است.

در `S07-MS41`، Migration ۵۶ Catalog/Template Tenant F10 را ثبت کرد؛ API مستقل
Create/List/Get با cohort/mask immutable و permission recheck، F10 را از
Project route جدا نگه می‌دارد. Candidate `c22f08778a4c9b523d926f1aedc9f8d9142655d4`، tree `3e5c6f93fae75200f3abba18b292eac4a17d3069` در Run 266
(`36343299949`) هشت Job، C# `553/553`، Node `126/126`، Web `139/139`،
Restore ۵۶ و Qualification `7/7` را سبز کرد. Worker/OutputAccess باز است.

در `S07-MS42`، Worker مستقل Portfolio با claim/lease و recheck cohort/mask،
Snapshot و دو Output PDF/XLSX Tenant را به سند `TenantReportOutput` متصل کرد.
cutoff در دقت میکروثانیهٔ DB پین و JSONB پیش از Hash canonical می‌شود. Candidate
`7a8a560fec6da560aa6982f1b9f11fc253dafef4`، tree `0683b1cbe844a321862e3e25fe2aa4e01e93174c`
در Run 272 (`36349571188`) هشت Job، C# `554/554`، Node `128/128`،
Web `139/139`، Restore ۵۶ و QA متصل F10 `4/4` را سبز کرد. OutputAccess و
Qualification کامل F10 در MS43 باز است.

در `S07-MS43`، Tenant OutputAccess فقط سند `TenantReportOutput` را پس از
recheck cohort/mask و اعتبارسنجی Snapshot، manifest، template، owner و byte/hash
می‌دهد. Retry فقط خطاهای allowlist و attempt محدود را با Snapshot قبلی می‌پذیرد؛
Cancel پیش از Render و idempotency به‌طور متصل کنترل شدند. Candidate نهایی
`84e4e76ca6173f834cec5ea481adc475e9dfd22f`، tree `033b2e4cc3652735b60b7fa9b849707906f8acc0`
در Run 275 (`36352517816`) هشت Job، C# `554/554`، Node `130/130`، Web
`139/139`، پنج browser scenario، Restore ۵۶ و Qualification `7/7` را سبز کرد.
F01 تا F10 End-to-End متصل‌اند؛ RPT1/UX2 و Production gateهای جدا دارند.

## Remaining Work

1. خانواده‌های `RPT1-F01` تا `RPT1-F10` متصل و checkpointed هستند.
2. گام بعدی طبق Roadmap، `V1.1-COL1` است؛ UI اختصاصی Reporting و visual regression در UX2 می‌آیند.
3. COL1/UX2/INT1/QA1 و gateهای Production طبق ترتیب مصوب بازند.
4. Pilot و gateهای وابسته به محیط واقعی فقط در زمان مقرر؛ Evidence فعلی مجوز Production rollout نیست.

## Known Gaps / Issues

| شدت | مورد | اثر/اقدام لازم |
| --- | --- | --- |
| Implementation | هر ده خانوادهٔ استاندارد Reporting متصل‌اند | RPT1 UI در UX2 و gateهای Production طبق Roadmap |
| Temporal source | endpoint جاری Planning زمان‌های Approval/Supersede، target و configuration تاریخی کافی ندارد | Contract/selector نسخه‌دار و compatibility producer متصل‌اند؛ history غیرقابل‌اثبات fail-closed است و توسعهٔ تاریخچهٔ کامل باید Slice دامنه‌ای مستقل باشد |
| Finance temporal source | read modelها و serviceهای legacy Finance cutoff تاریخی، postedAt/approvedAt مستقل، Budget supersession history و Aging دوطرفهٔ کامل ندارند | Contract/selector/compatibility source نسخه‌دار F05 متصل است و history غیرقابل‌اثبات را fail-closed رد می‌کند؛ producer تاریخی غنی‌تر در صورت نیاز Slice دامنه‌ای مستقل است |
| Commercial temporal source | source و endpointهای legacy current-state/truncated هستند؛ activation history، Party/Item snapshot، conversion version و completeness cutoff کامل ندارند | Contract/selector/compatibility source نسخه‌دار F06 متصل است و history غیرقابل‌اثبات را fail-closed رد می‌کند؛ producer تاریخی غنی‌تر در صورت نیاز Slice دامنه‌ای مستقل است |
| Technical temporal source | `/technical-office/state` current و capped است؛ legacy RFI/Submittal ledger کامل ندارد | MS25 رویدادهای تازه را ثبت می‌کند؛ legacy بدون backfill همچنان `InsufficientData` است و MS26 فقط مسیر Certified را متصل کرد |
| Traceability | دو ADR با شماره `0027` وجود دارد | بدون renumber شتاب‌زده، یک تصمیم نسخه‌دار برای شناسه یکتا ثبت شود |
| Ownership | اسناد، UI Reporting را هم «gate باز RPT1» و هم کار UX2 می‌خوانند | مالک gate بسته‌شدن RPT1/UX2 باید در Roadmap صریح شود |
| Legal operations | Community انتخاب شده، اما eligibility دائمی از code استنباط نمی‌شود | پیش از Production و حداقل سالانه توسط مالک تجاری/حقوقی بازاعتبارسنجی شود |
| Intentional gate | PDF license در defaults پیکربندی نشده و feature flagها خاموش‌اند | نقص Runtime نیست؛ ADR 0030 فقط QA qualification را مجاز کرده است |

## Current GitHub / CI State

- Repository: `ehsangholamibbq-cell/pmcs-workshop-management`؛ PR #2 از `v1.1-development` به `main`.
- PR: `open`، `draft` و ادغام‌نشده است.
- Checkpoint head پیش از F02 Candidate: `a5d80de29f945c504ffaa9562b4e0993a2a44aea`؛ Run 136
  (`35467329335`) هر هشت Job را سبز کرد.
- F02 semantic contract Candidate: `b4a59fa966320a1da4b53759814224e21893c01e`؛ tree
  `6b5b486dace3c07b0b4e0385413bf1add5aee7a3`؛ Run 137 (`35474388839`) هر هشت Job موفق،
  `330/330` تست C#، `56/56` تست قراردادی Node، `139/139` تست Web، پنج browser scenario و Restore
  کامل ۴۳ Migration.
- F02 semantic Checkpoint: `42e607ee98e2bbdedafaec892c8af47b9f947aa1`؛ tree
  `aa2aa2af814f64b0806a15e59c4def0a151eaf5e`؛ Run 138 (`35474992254`) هر هشت Job موفق.
- F02 Runtime Core Source: `6fc28cf54a6df820c49a2365eab76e3550ae421a`؛ tree
  `5188dac79fe5187b319e6aa727da89163fa37c1b`؛ Run 139 (`35477179493`) هر هشت Job موفق،
  `346/346` تست C#، `58/58` تست Node، `139/139` تست Web، پنج browser scenario و Restore ۴۳
  Migration.
- F02 Runtime Core Checkpoint: `d8fd4398309b08d1cdc90e26140c4581dc476636`؛ tree
  `5345fdfc0cf1b0663b9cb1fa3bb97a4b6abf7c9b`؛ Run 140 (`35477677819`) هر هشت Job موفق.
- F02 Renderer/Golden Source: `4f68f57de2c2a79b654a19128894d9c89878ab65`؛ tree
  `f4b592c72ea65974c00b936ca59c0428eb47f981`؛ Run 141 (`35495791821`) هر هشت Job موفق،
  `353/353` تست C#، `60/60` تست Node، `139/139` تست Web، پنج browser scenario و Restore ۴۳
  Migration.
- F02 Catalog/API/Worker Source: `7fc55c167ad2159a31c895b32a52d78f47574df9`؛ tree
  `d665fe4cdf29369f96ec0875bc6f1535db349d55`؛ PR validation merge
  `2a91fc442a3a84a2ee3d6c59fe5186c8f0ed3efb` با همان tree؛ Run 144 (`35498990050`) هر هشت Job
  موفق، `355/355` تست C#، `61/61` تست Node، `139/139` تست Web، پنج browser scenario، هارنس F02
  برابر `13/13` و Restore ۴۴ Migration.
- F02 Connected Checkpoint: `a7b7e885c1e59de894100ade96444184692ab5d3`؛ tree
  `5a476abb23ad650cb7334f537583ee4fa7b3c628`؛ Run 145 (`35499768898`) هر هشت Job موفق،
  `355/355` تست C#، `62/62` تست Node، `139/139` تست Web، پنج browser scenario و Restore ۴۴
  Migration.
- F03 semantic contract Candidate: `e3218555a38f7ba460558e51b4db3f8bc17fcd9c`؛ tree
  `1fe4cc804fdd078a71ff2633201c8c690447900e`؛ PR validation merge
  `a6987cd47bb1be917d34a94fb064aa72ec6b89c2` با همان tree؛ Run 146 (`35500809115`) هر هشت Job
  موفق، `355/355` تست C#، `63/63` تست Node، `139/139` تست Web، پنج browser scenario و Restore ۴۴
  Migration.
- F03 Runtime Core Source: `22d5b0f91edf8d733192fae0ba946c8538c63bca`؛ tree
  `eb5ea4253a7b80e8ab3320b9ccea747624f89790`؛ PR validation merge
  `b89ca9920fc216644203d9cd5b3868cabed453fe` با همان tree؛ Run 148 (`35507127968`) هر هشت Job
  موفق، `377/377` تست C#، `64/64` تست Node، `139/139` تست Web، پنج browser scenario و Restore ۴۴
  Migration.
- F03 Renderer/Golden Source: `d9d7ddb17d222f3b53402f291bf3d0cb8a3f957f`؛ tree
  `58fb79b0ee3d7c9cfa11630635b8ebdfbcbce434`؛ PR validation merge
  `235e0a0f12b560bba06792c3290724d739117b73` با همان tree؛ Run 154 (`35512969648`) هر هشت Job
  موفق، `383/383` تست C#، `65/65` تست Node، `139/139` تست Web، پنج browser scenario و Restore ۴۴
  Migration.
- F03 Connected Source MS09: `40afeb37d7bf90e97a988cae141901e28d336516`؛ tree
  `ae06285bf1a68fe2592dacc76c7d31cb291ab924`؛ PR validation merge
  `4944731391c2b649cd401fc9095619cb19d41f72` با همان tree؛ Run 156 (`35515989200`) هر هشت Job
  موفق، `387/387` تست C#، `66/66` تست Node، `139/139` تست Web، پنج browser scenario، هارنس F03
  برابر `14/14` و Restore ۴۵ Migration.
- F03 Connected Checkpoint: `c59a2444f5d5dd70859441f27d05c23dea6c268e`؛ tree
  `8a7926ac11abb529a3887d1e8a2091f7aeedecbd`؛ PR validation merge
  `060f4807d640aff0d0014cddac27ea9aca7ad5ef` با همان tree؛ Run 157 (`35516916383`) هر هشت Job
  موفق، `387/387` تست C#، `67/67` تست Node، `139/139` تست Web، پنج browser scenario، هارنس F03
  برابر `14/14`، validator روی `367` فایل و Restore ۴۵ Migration.
- F04 semantic contract Source: `f8829027c2ce073c207cd0e04a49c306b546c6a1`؛ tree
  `2b784f135894092ef55bf7c7df201b1f03e0c77f`؛ PR validation merge
  `80830a48ffd5b84ecdc97990b052f6d7037eda42` با همان tree؛ Run 158 (`35522512734`) هر هشت Job
  موفق، `387/387` تست C#، `68/68` تست Node، `139/139` تست Web، پنج browser scenario، validator
  روی `367` فایل و Restore ۴۵ Migration؛ Runtime F04 هنوز پیاده نشده است.
- F04 Runtime Core Source: `deb1571ec66d820868e8f4b77b631471e3c8207c`؛ tree
  `9e41495a357480af03f1555ef640962ab863d332`؛ PR validation merge
  `f0d3a5550d9bd1c10d8ddd5a3c0ada24eb0fead5` با همان tree؛ Run 163 (`35527577826`) هر هشت Job
  موفق، `412/412` تست C# شامل `25/25` case متمرکز F04، `69/69` تست Node، `139/139` تست Web، پنج
  browser scenario، validator روی `378` فایل، audit `274/204/5` و Restore ۴۵ Migration؛ Renderer و
  wiring F04 باز است.
- F04 Renderer/Golden Source: `6a717f10e4bff167ad7e2643313008f5afcc8264`؛ tree
  `995c7fae108bbb5265faa036f951036d36e7061e`؛ PR validation merge
  `782f42ff73425cf5cad69b0635bacf05790d2ff1` با همان tree؛ Run 167 (`35532522587`) هر هشت Job
  موفق، `418/418` تست C# شامل شش case Renderer/Golden تازه و `31/31` case متمرکز F04، `70/70`
  تست Node، `139/139` تست Web، پنج browser scenario، validator روی `381` فایل، audit `274/204/5`
  و Restore ۴۵ Migration؛ Catalog/API/Worker wiring F04 باز است.
- F04 Connected Source MS13: `4c48c03aad126a594e5328fc7995a72728ba2274`؛ tree
  `49f957729fdccb0397dd153b93135ce2eaddd68a`؛ PR validation merge
  `05ca8ac7e3fa643e111b9c8511e3e08d62be60a5` با همان tree؛ Run 169 (`35535904655`) هر هشت Job
  موفق، `419/419` تست C#، `71/71` تست Node، `139/139` تست Web، پنج browser scenario، هارنس F04
  برابر `15/15`، validator روی `382` فایل، audit `274/204/5` و Restore ۴۶ Migration؛ UI/Production
  خاموش است.
- F05 semantic contract Source: `72fa88349d01edd4c6455eb0af1aebfdeced8c35`؛ tree
  `d2722dd8fab797650ed0c9befb80df93fc0be135`؛ PR validation merge
  `6f1918ed1323fa3f6f14eeeaedcad8b2cf241ff7` با همان tree؛ Run 171 (`35538654765`) هر هشت Job
  موفق، `419/419` تست C#، `73/73` تست Node، `139/139` تست Web، پنج browser scenario، validator
  روی `382` فایل، audit `274/204/5` و Restore ۴۶ Migration؛ Runtime F05 هنوز پیاده نشده است.
- F05 Runtime Core Source: `77ad46cbac12b899116516b0a58665ae888b3bf2`؛ tree
  `5c67523b0fbed8d521627fe406f74271a1bbdcfe`؛ PR validation merge
  `5f9ad7bd2bf0cb48c5a47dbfbe29ab09afc3f92c` با همان tree؛ Run 175 (`35541740268`) هر هشت Job
  موفق، `450/450` تست C# شامل `31/31` case متمرکز F05، `75/75` تست Node، `139/139` تست Web، پنج
  browser scenario، validator روی `390` فایل، audit `274/204/5` و Restore ۴۶ Migration؛
  Renderer/Golden و wiring F05 باز است.
- F05 Renderer/Golden Source MS16: `9ddf7f1d96324e7ffb22d2abec83071a6c087ec2`؛ tree
  `f873795dcb8893dc28f88d5e5fc8292c5201e1e4`؛ PR validation merge
  `72ab7827731fa763828c049be953ee9ca8c128a4` با همان tree؛ Run 178 (`35558202348`) هر هشت Job
  موفق، `456/456` تست C# شامل شش case Renderer/Golden تازه و `37/37` case متمرکز F05، `77/77`
  تست Node، `139/139` تست Web، پنج browser scenario، validator روی `393` فایل، audit `274/204/5`
  و Restore ۴۶ Migration؛ Catalog/API/Worker wiring F05 باز است.
- F05 Connected Source MS17: `6de1e9ac3b457426be5e50064d1767106cd50c39`؛ tree
  `a4a8e8e655c56d05da2be5d87e7b84a9bb9a7a1f`؛ PR validation merge
  `cbd27a673b1887b2e245bef5340eb8199f480be4` با همان tree؛ Run 185 (`35563055242`) هر هشت Job
  موفق، `458/458` تست C# شامل `39/39` case متمرکز F05، `78/78` تست Node، `139/139` تست Web، پنج
  browser scenario، هارنس F05 برابر `15/15`، validator روی `394` فایل، audit `274/204/5` و Restore
  ۴۷ Migration؛ UI/Production خاموش است.
- F06 Semantic Contract Source MS18: `4c5d026466cb3f76f351221297540a0936337335`؛ tree
  `d9e8febc5f220f8d00eaff80926b00dec2ea0926`؛ PR validation merge
  `9bfb7badcde8a67d385567db997f265a67497f0f` با همان tree؛ Run 188 (`35567252567`) هر هشت Job
  موفق، `458/458` تست C#، `80/80` تست Node، `139/139` تست Web، پنج browser scenario، validator روی
  `394` فایل، audit `274/204/5` و Restore ۴۷ Migration؛ Runtime F06 پیاده نشده است.
- F06 Runtime Core Source MS19: `177a1d89a07c23b2ae446218e98556cfbcf57a21`؛ tree
  `165cd1d451935f3cb94db7b9f5718678de00aca2`؛ PR validation merge
  `2dbaf0ba14cf80ee863e07c2561ab7392037fd7c` با همان tree؛ Run 192 (`35573450703`) هر هشت Job
  موفق، `490/490` تست C# شامل `32/32` case متمرکز F06، `82/82` تست Node، `139/139` تست Web، پنج
  browser scenario، validator روی `402` فایل، audit `274/204/5` و Restore ۴۷ Migration؛
  Renderer/Golden و wiring F06 باز است.
- F06 Renderer/Golden Source MS20: `fb88b94d6949e7780f5f40aa567e2ea3f187a6e8`؛ tree
  `212c1193d249cf1120297920c59b4ea15cb80c07`؛ PR validation merge
  `9f8d6ce25b5ddbeec777d0e104024e9389b9a513` با همان tree؛ Run 196 (`35582136746`) هر هشت Job
  موفق، `496/496` تست C# شامل شش case Renderer/Golden تازه و `38/38` case متمرکز F06، `84/84`
  تست Node، `139/139` تست Web، پنج browser scenario، validator روی `405` فایل، audit `274/204/5`
  و Restore ۴۷ Migration؛ Catalog/API/Worker wiring F06 باز است.
- F06 Connected Source MS21: `df3879dd8b17403787154a398cc114b27c7172bc`؛ tree
  `5483e684aaa220a32b3135ea0b2bb3b2136023be`؛ PR validation merge
  `0900def8f237a8d282501a8ee4ae0be5676f2fda` با همان tree؛ Run 202 (`36235821024`) هر هشت Job
  موفق، `497/497` تست C# شامل `39/39` case متمرکز F06، `85/85` تست Node، `139/139` تست Web، پنج
  browser scenario، هارنس F06 برابر `15/15`، validator روی `406` فایل، audit `274/204/5` و Restore
  ۴۸ Migration؛ UI/Production خاموش است.
- F07 Semantic Contract Source MS22: `11168534372487bff3cc798861ca036489b3dea0`؛ tree
  `af7e9e48c706c23f50521dbf715e6027e1512194`؛ PR validation merge
  `438ac2c4feeab006f850fe9be6c5641bbdcd5368` با همان tree؛ Run 205 (`36281376786`) هر هشت Job
  موفق، `497/497` تست C#، `89/89` تست Node شامل سه case F07، `139/139` Web، پنج browser scenario،
  validator `406` فایل، audit `274/204/5`، Restore ۴۸ Migration و Qualification `7/7`؛
  F07 Runtime/Renderer/wiring هنوز باز است.
- F07 Runtime Core Source MS23: `7f66113d3fe091829747d1f5059eb8f16c82cb88`؛ tree
  `88ba4957b76c6803893afa04d618b22c47c919e9`؛ PR validation merge
  `22ec3d66aeeb5f7e15070bfdaa3ba2f6b1c17cf2` با همان tree؛ Run 211 (`36296626010`) هر هشت Job
  موفق، `512/512` تست C# شامل ۱۵ مورد متمرکز F07، `90/90` Node، `139/139` Web، پنج browser
  scenario، validator `415` فایل، audit `274/204/5`، Restore ۴۸ Migration و Qualification `7/7`؛
  Renderer/Golden، producer تاریخی RFI/Submittal و Catalog/API/Worker بازند.
- F07 Renderer/Golden Source MS24: `8a08d3a0876cb6307613cb3eb51d918ff0269564`؛ tree
  `ffa44deb661c4055f06fd32064bdfa8f61de425f`؛ PR validation merge
  `121960696bb6c3fd4a7c490371ee20367840cb0b` با همان tree؛ Run 215 (`36301524177`) هر هشت Job
  موفق، `518/518` C# شامل شش case Renderer تازه، `91/91` Node، `139/139` Web، پنج browser
  scenario، validator `419` فایل، audit `274/204/5`، Restore ۴۸ Migration و Qualification `7/7`؛
  historical producer و Catalog/API/Worker هنوز بازند.
- F07 Historical Producer Source MS25: `b2cc811e9202b49dd643972bde547c105fd9dc02`؛ tree
  `1673d1b48ca41fd425199da9235ec87c712d81b2`؛ PR validation merge
  `a5843ace11e1546a472e76633fc13354c0570e3c` با همان tree؛ Run 219 (`36304170407`) هشت
  Job موفق، `522/522` C#، `92/92` Node، `139/139` Web، پنج browser scenario، validator `420`
  فایل، Restore ۴۹ Migration و Qualification `7/7`؛ wiring F07 و F08–F10/UI/Production بازند.
- F07 Connected Source MS26: `9e477aae9937edc937f3026d794954461cb4b37b`؛ fix
  `b7a44b35eb7f498bf4382990324e3253033c0284`؛ tree
  `bb6ebad2d3435caf4e085a65e08a085a2271761f`؛ PR merge
  `a771d7113286a06606d89c55c1894d181cc29406` با همان tree؛ Run 222
  (`36305583760`) هشت Job موفق، `523/523` C#، `94/94` Node، `139/139` Web، پنج browser
  scenario، validator `421` فایل، Restore ۵۰ Migration، F07 connected `17/17` و Qualification
  `7/7`؛ F08–F10/UI/Production بازند.
- F08 Semantic Contract MS27: `b72ab1c09376e0ec45b6b52a460660f4b807b15a`؛ tree
  `14334896b2155f6ebefe612a29123bd73bce3760`؛ PR merge
  `6ba577ee13b559afe10ea62b7f2a6310ef22f1a8` با همان tree؛ Run 224
  (`36307100818`) هشت Job موفق، Node `98/98` و Qualification `7/7`؛ Runtime باز.
- F08 Runtime Core MS28: `36682da970d0489569ed62a1d02684b5d6c588e9`؛ tree
  `2f44542a094a86d5782b01a31bc8092a97b71b39`؛ PR merge
  `f692b47ed00ac6b2c0b6c71007a9618fe40adb0f` با همان tree؛ Run 231
  (`36308573306`) هشت Job موفق، Node `99/99`؛ Renderer/Golden و wiring باز.
- F08 Renderer/Golden MS29: `68a8c49311d05480824f3c7ec58933510877c8e6`؛ tree
  `14fe30b65d983c553cf3675dc202b682d90f5c9d`؛ PR merge
  `4609e3bbb316b6c6a71bc8a117f3be4664c17868` با همان tree؛ Run 235
  (`36309751881`) هشت Job موفق، C# `530/530` و Node `100/100`؛ wiring باز.
- F08 Connected MS30: `786f032e5ce91ceffa80599c4ce02ba23f30bb1d`؛ tree
  `9adcf1558c1bcab8a40a242c49efdf4df816a2dc`؛ PR merge
  `aff478c94695c33695b691d165fff0516f374ebd` با همان tree؛ Run 237
  (`36310745011`) هشت Job موفق، C# `530/530`، Node `102/102`، F08 connected `20/20`،
  Restore ۵۱ Migration و Qualification `7/7`؛ F09/F10/UI/Production بازند.
- F09 Semantic Contract MS31: `67b60c00779d51ea9479dee4d887c810d572d485`؛ tree
  `400add67f58dc03b559c3f1420d186fb6cf5ec15`؛ Run 239 (`36318261047`)
  هشت Job موفق، Node `106/106` شامل سه تست متمرکز F09، Migration همچنان ۵۱؛
  Runtime/Renderer/wiring F09 و F10/UI/Production بازند.
- F09 Source/Runtime Core MS32: `2000b965dc31e6a83f0b1366c9a203ae809cee03`؛ tree
  `14e9b0c6bcc4c4a1b785e761c678e18f0d216a8c`؛ Run 242 (`36321908108`)
  هشت Job موفق، C# `534/534`، Node `107/107`، Web `139/139`؛ Migration ۵۱؛
  Renderer/producer/wiring F09 و F10/UI/Production بازند.
- F09 Renderer/Golden MS33: `0701a44155d1955a1b8e27db01d561c370b6be6e`؛ tree
  `db7347e04344394f219cd68257a23c27159fd200`؛ Run 245 (`36324102914`)
  هشت Job موفق، C# `538/538`، Node `108/108`، Web `139/139`؛ PDF/XLSX و
  پنج visual digest pin؛ Migration ۵۱؛ producer/wiring F09 و F10 بازند.
- F09 Owner History Producer MS34: `79bc3c62f17711eaa61281f90d995852f9128147`؛ tree
  `e32fb658426a74fcfb0f61a463ab9748d1b8b686`؛ Run 248 (`36327282876`)
  هشت Job موفق، C# `540/540`، Node `109/109`، Web `139/139` و Restore Drill ۵۲؛
  selector/wiring F09 و F10 بازند.
- F09 Historical Selector MS35: `44bd46689bcb795534bcf059f42591cecdaa35ba`؛ tree
  `d849b915a0157dd10812acb8271c8b64dd108c29`؛ Run 250 (`36329655993`)
  هشت Job، C# `542/542`، Node `109/109`، Web `139/139` و Restore Drill ۵۲؛
  Catalog/API/Worker F09 و F10 بازند.
- F09 Connected MS36: `2bb925b8cf8531442c5136e55811a9f8c31db655`؛ tree `3085dc51931b2b7965521b7b18fe0b9bfa55ec70`؛ Run 252 (`36331528136`)
  هشت Job، هارنس F09 `18/18`، Restore ۵۳ و Qualification `7/7`؛ artifact
  `10936011116` (`sha256:044284ae2594b020e489b895f6bb1bef9801e21d0e3dca5ee1e6d09ef838efeb`).
- F10 Contract MS37: `bb17a37fa19b06d111443fad178c2589e376bb25`؛ tree
  `82eba0ffebc1c631281a78727f4b2325a50f0ecb`؛ Run 254 (`36333343004`)
  هشت Job و Qualification `7/7`؛ artifact `10936771990`
  (`sha256:cecc6cf9a6e438789fd5a79dd91b8fda1a03cc1dd817d99464b87e1a6c697754`).
- F10 Tenant Scope MS38: `ea5215e26ba382bdecd756adec1083dd3ef28d06`؛ tree
  `63e5542679e9d0427c6ec96ad7fa3404810f340d`؛ Run 256 (`36335141253`)
  هشت Job و Qualification `7/7`؛ artifact `10936753009`
  (`sha256:e2cf47c1d4cfab57912daa7e525d5891dc3f31861b41d51eeced7cac7924c3cd`).
- F10 Runtime MS39: `c45b72be7bc913a6dc65b656bea0188f7da0dc8c`؛ tree
  `00ad037a2eec41c2d5dbacfe0c186e04c4584cce`؛ Run 261 (`36338179481`)
  هشت Job و Qualification `7/7`؛ artifact `10938470711`
  (`sha256:981dfc244427999078e9e4e9c13d281197fb9e721520cc421e0b5f1820cda122`).
- F10 Renderer MS40: `d57f73611e647d352ec5c59a19cd996e589d0fe2`؛ tree
  `52daac29eff755e3a95c132d581fa7de37ef6387`؛ Run 264 (`36340600926`)
  هشت Job و Qualification `7/7`؛ artifact `10938752849`
  (`sha256:fc9031bdc74b7fbc5b5c92fb2edcf19093c48e0b396c2dc18760349522961706`).
- F10 Tenant API MS41: `c22f08778a4c9b523d926f1aedc9f8d9142655d4`؛ tree `3e5c6f93fae75200f3abba18b292eac4a17d3069`؛ Run 266 (`36343299949`)
  هشت Job و Qualification `7/7`؛ artifact `10940021865`
  (`sha256:2d4bb99532b8cb2d34cfac8e6f2f0b5715023ec1d2f515c365bd597de9649498`).
- F10 Worker MS42: `7a8a560fec6da560aa6982f1b9f11fc253dafef4`؛ tree
  `0683b1cbe844a321862e3e25fe2aa4e01e93174c`؛ Run 272 (`36349571188`)
  هشت Job و QA متصل `4/4`؛ artifact `10942071124`
  (`sha256:10c07cd9b8060792561902140ff9f83ef67538143c9913fa9c152fe1f3c82de3`).
- F10 Connected MS43: `84e4e76ca6173f834cec5ea481adc475e9dfd22f`؛ tree
  `033b2e4cc3652735b60b7fa9b849707906f8acc0`؛ Run 275 (`36352517816`)
  هشت Job، F10 متصل `12/12`، Retry `3/3`، Cancel `4/4`، Restore ۵۶ و
  Qualification `7/7`؛ artifact `10942892666`
  (`sha256:fab4c0d3cfa0086f6ebd4dd14db9b344866c30c626b248722f37671fa4d3586b`).
- Catalog Decision Candidate: `d81ecc00762145210e1c688f8f5843f46d62fc04`؛ tree
  `5f40383ad506d94520c741eb69fcd00086283734`؛ Run 135 (`35466775368`) هر هشت Job موفق،
  `330/330` تست C#، `54/54` تست قراردادی Node، `139/139` تست Web و پنج browser scenario.
- Source Candidate MS06: `b8f21492a4f44c7c412e5b7eda0b164e7f256758`؛ tree
  `e94b6ba3753e67b42ea0ec99e998761fdad0bcc3`.
- آخرین CI Source: MS46 Run 416 (`36489364018`) هشت Job موفق؛
  Artifact `11001141954` با ۴۵ PNG/PDF، Source/Hash/ابعاد معتبر و
  قاب 45 بازبینی‌شده؛ MS45 documentation Run 415 Safe است و CI
  مستندات شرط MS46.
- Qualification artifact Run 222 برابر `10927131413` با digest
  `sha256:06ecf278af9f42d78aa96e788b3e6b2d00476199c21e4b3f84ec91541bcd3842` است؛ Integration artifact
  `10927275947` با digest `sha256:e3c4021e7c96e79bdc4b929581bbcd8d007fbdac7322f5f89d2a202056eb476b`
  و UI-E2E artifact `10927575941` با digest
  `sha256:0ffa69bee5195c6674cba494b8bf7ef4c30714511604cb7a1eed368a4e12f637` ثبت شدند.
- Run 133: build بدون warning، `330/330` تست C#، `52/52` تست قراردادی Node، `139/139` تست Web، پنج
  browser scenario و PDF Golden متصل `8/8` پاس شدند؛ PDF برابر `42489` byte و
  SHA-256 `cc188c842ddcced9a24acd5e18f92c4a6511252c40104055c8c38627cdfac863` بود.
- visual digest در ۹۶ DPI برابر
  `95d6e71de15d9d130041d5c295c94239b9fe9095e6572e581aa9a655ee85c9b2` و بازبینی مستقل Poppler
  بدون defect بود. Qualification artifact `10590681541` با digest
  `sha256:00f630ce572f49041af75d5687f0e79dafccb3a8359bc2ddb741c9e7b3c8d9e7` ثبت شد.

## Exact Next Micro-Step

در handoff تاریخی MS43، «گام بعدی طبق ترتیب Roadmap، `V1.1-COL1` با DoR مستقل Project Collaboration» بود؛ DoR و MS01–MS06 اکنون سبزند.
**MS99 با Source Run 623 و docs Run 624 هشت Job سبز و Safe، G4 را بست. MS100 با Source `fd13282` و Run 640 هشت Job سبز، Artifact `11124980874` معتبر/مرور شده و docs `c950ec0`/Run 642 هشت Job سبز، G5 و UX2 را بست. Exact Next، DoR مستقل `V1.1-INT1` است.**
INT1/QA1، Production و قفل V1.1 Gateهای جدا می‌مانند.

## Resume Rule

در ادامه‌های بعدی ابتدا همین سند، GitHub branch head/tree و آخرین Checkpoint خوانده شوند. فقط اگر
یکی از آن‌ها تغییر کرده بود، اختلاف هدفمند بررسی شود؛ مرور دوباره همه Conversationها لازم نیست.
