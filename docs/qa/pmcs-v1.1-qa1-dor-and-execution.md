# PMCS V1.1 — DoR و نقشهٔ اجرای QA1

- شناسه: `PMCS-V1.1-QA1-DOR-001`
- وضعیت: `DoR Ready` در Run 679؛ اجرای QA1 با Run 683 `Qualified → Final → Baseline Locked` شد؛ مرجع خروج `PMCS-V1.1-QA1-LOCK-C1`
- Parent product baseline: `26bf222d44634562ca7f3fc0931f3f8b79ca04a1`
- Repository start: `0389b52cbd3385bdcc9f0e2a94411800389ae2fc`
- شاخه/PR: `v1.1-development` / Draft PR #2
- پیش‌نیاز: `PMCS-V1.1-INT1-FOUNDATION-C1` فقط برای Foundation خاموش Safe؛ Run 678 روی تصمیم پذیرش مالک، هشت Job سبز
- قرارداد: `PMCS-QA-V1.1-001 v1.2.0`، ADR 0033 و ADR 0034

## هدف و مرز

QA1 باید یک Candidate ثابت V1.1 را با شواهد هم‌Commit برای Scope کامل، Full Regression
نسخهٔ V1، Migration/Restore، امنیت، Offline، UI و کارایی تأیید کند. خروج آن شامل گزارش
مستقل `PMCS V1.1`، سپس ثبت `Qualified → Final → Baseline Locked` است. آزمون دستی
مالک و انتشار رسمی بعد از قفل‌اند و در این Gate اجرا نمی‌شوند.

INT1 در Candidate نسخهٔ 1.1 خاموش است؛ نبود Grant راه‌انداز، Provider/Model فعال و
Credential ویژهٔ INT1 باید از تنظیمات Candidate و دیتابیس تازه اثبات شود. آزمون زندهٔ
GPT، Gemini و Claude در `AGENT-S1-LIVE` پس از انتشار V1.1 می‌ماند. `R-AI-03` باز است.

## بررسی DoR و شکاف‌های فعلی

| شرط | شاهد موجود تا شروع QA1 | کار لازم در QA1 |
| --- | --- | --- |
| Scope و مرز نسخه | Roadmap 1.191.0، Scope 1.3.0 و ADR 0033/0034 | Candidate ثابت و فهرست deferredها ثبت شود |
| Source و وابستگی‌ها | UX2 و INT1 Foundation خاموش Safe؛ Run 678 هشت Job سبز | همهٔ Suiteها روی SHA واحد Candidate دوباره اجرا شوند |
| Permission و طبقه‌بندی داده | Catalog و آزمون‌های منفی ماژول‌ها وجود دارند | matrix نسخهٔ 1.1 و default-deny روی Candidate ثبت شود |
| مالکیت داده/Migration | ledger و Restore ایزوله در CI موجود است | Upgrade نماینده V1، تکرار Migration، Runtime rollback و recovery قطع عملیات ارزیابی شود |
| API/Event/Tool | قراردادهای نسخه‌دار و آزمون‌های مالک موجودند | Full Compatibility و منع Tool نوشتنی/SQL مستقیم تجمیع شود |
| Offline و تعارض | تست Domain، API و مرورگر در CI موجود است | رفتار قطع/وصل و حفظ پیش‌نویس در Full Regression سنجیده شود |
| UI، دسترس‌پذیری و چاپ | UX2 G5 Safe با Artifact مرورشده | Visual/Print روی Candidate نهایی به SHA و Artifact متصل شود |
| Security/Privacy/Retention | آزمون‌های Documents، Identity و Permission موجودند | شواهد منفی و ریسک‌های باز Candidate بازبینی شوند |
| Rollout/Rollback/Observability | Pilot contract و Runbook موجود است | rehearsal و نتیجهٔ Load/Soak، recovery و بازگشت Runtime ثبت شود |
| گزارش Qualification | CI فعلی هفت Suite و هشت Job را سبز می‌کند، اما خروجی گزارش‌ساز `PMCS V1` است | گزارش مستقل V1.1 با Gate fail-closed و Artifact/Commit یکسان ساخته شود |

سبز بودن Run 678 شرط آمادگی برای آغاز QA1 است و به‌تنهایی هیچ‌کدام از وضعیت‌های
`Release Candidate`، `Qualified`، `Final` یا `Baseline Locked` را نمی‌سازد.

Run 679 روی سند DoR هر هشت Job و گزارش Regression پایه را سبز گذراند؛ DoR مرحله
Ready است. این Run فقط مجوز اجرای QA1 را می‌دهد و شاهد Qualification نهایی نیست.

## دفتر اجرای QA1

قرارداد مستقل `tools/qa/v1.1-qualification-requirements.json` هجده شاهد الزامی دارد.
`tools/qa/v1.1-test-report-generator.mjs` روی هفت Suite کامل V1 و همهٔ این شاهدها
یک Commit و tree، نتیجهٔ passed، Artifact با SHA-256 معتبر و نبود شاهد تکراری را
می‌خواهد؛ تا آن زمان وضعیت `failed` و `baselineLockEligible=false` است. گزارش V1
تاریخی دست نخورده باقی می‌ماند.

Run 683 (`36839494362`) روی Source `b223fa69ee40b467a014e816b3bc021fa42e2ae0`
و Tree `12388c888e33460f122c9cf4d4dfe65e53178eb7` هر هشت Job را سبز گذراند.
گزارش مستقل V1.1 تمام ۱۸ شاهد، هفت Suite پایه، مهاجرت خالی/تکرار/Restore، Upgrade
از V1، Runtime rollback، قطع تراکنش و Recovery، بار، مرورگر/چاپ/دسترس‌پذیری،
هویت ساخت و Digest را با صفر شکست تأیید کرد. تصمیم خروج و Artifact ID/Hashها در
[`PMCS-V1.1-QA1-LOCK-C1`](../checkpoints/v1.1-qa1-qualified-final-baseline-lock.md)
ثبت شده‌اند؛ Gate مالک و انتشار همچنان بازند.

## نقشهٔ اجرایی و معیار خروج

برش‌های کاری ممکن است در صورت کشف نقص جدا شوند؛ شماره‌ها تعهد به تعداد ثابت نیستند:

1. DoR و فهرست شکاف‌ها، سپس CI همان سند؛
2. قرارداد Candidate و گزارش مستقل V1.1 با رد Evidence ناقص/مخلوط؛
3. Matrix امنیت، Permission، Documents، IAM و Project Bootstrap؛
4. Collaboration/Offline و Reporting/Export؛
5. UI/Visual/Print/Accessibility و بودجهٔ کارایی؛
6. INT1 خاموش در تنظیم انتشار و DB تازه، همراه Regression Advisory تاریخی V1؛
7. Migration از V1، Restore/Runtime rollback، Load/Soak و recovery؛
8. Freeze Candidate، اجرای Full Regression هم‌SHA، بررسی Artifactها، گزارش نهایی و قفل.

هر برش فقط با Source و CI مربوط به خود Checkpoint ایمن دارد. نقص مسدودکننده Candidate
را عوض می‌کند و اجرای کامل Gate نهایی باید روی Candidate تازه تکرار شود.

## شواهد خروج QA1

- manifest Candidate با Commit کامل، tree، نسخهٔ محصول/API/Web/Schema، فهرست Migration و deferredها؛
- هفت Suite پایه و Suiteهای اختصاصی V1.1 با گزارش fail-closed یک SHA و Run موفق؛
- نتیجهٔ هر هفت سناریوی Migration/Rollback قرارداد QA، Restore و Load/Soak؛
- ثبت حالت خاموش INT1 در تنظیم انتشار و Registry تازه، بدون ادعای اتصال واقعی Provider؛
- Build/Artifact digest، گزارش انسانی و ماشین‌خوان، CI Run URL/ID و ریسک‌های باز؛
- تصمیم مستند `Qualified`، انتخاب `Final` و قفل Baseline تنها بعد از تمام شروط بالا.

اگر هر شاهد کم یا نامعتبر باشد، QA1 باز می‌ماند. پس از قفل، فرم
`PMCS-V1.1-OWNER-ACCEPTANCE-001` به مالک برای آزمون کامل تحویل می‌شود؛ انتشار و شروع
V1.2 پس از پذیرش او و ثبت انتشار هستند.
