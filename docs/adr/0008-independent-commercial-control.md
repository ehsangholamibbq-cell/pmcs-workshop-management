# ADR 0008 — Independent Contract and Procurement Control

- Status: Accepted
- Date: 2026-09-09

## Context

کنترل قرارداد و خرید در پروژه عمرانی باید حتی در نبود WBS، Budget Baseline یا برآورد اولیه قابل استفاده باشد. در عین حال یک درخواست خرید هنوز تعهد مالی قطعی نیست و مبلغ قراردادی نامعلوم نباید به صفر تبدیل شود. اتصال Finance به متن آزاد قرارداد نیز برای گزارش مدیریتی و lineage کافی نیست.

## Decision

- bounded context مستقل `Commercial` مالک Party، Project Contract، Contract Amendment، Purchase Request، Purchase Order و Commercial State است.
- Party در V1 در محدوده Tenant/Project ثبت می‌شود؛ یک Vendor Master سراسری یا deduplication سازمانی در این Slice وجود ندارد.
- WBS در هیچ Aggregate تجاری اجباری نیست و ستون فقرات Commercial نیست.
- `OriginalApprovedAmount` قرارداد و `EstimatedAmount` درخواست خرید nullable هستند. نبود آن‌ها به معنی «نامشخص» است، نه صفر.
- قرارداد و الحاقیه workflow و Revision مستقل دارند؛ فقط قرارداد Active و الحاقیه Approved در سقف مصوب محاسباتی اثر می‌گذارند.
- مسیر خرید Lite عبارت است از `Draft → Submitted → Approved → Ordered`. فقط صدور Purchase Order از یک درخواست Approved تعهد واقعی ایجاد می‌کند.
- Purchase Order شناسه Contract اختیاری دارد، اما Party و مبلغ مثبت و ارز پایه پروژه اجباری‌اند.
- Commercial State دو محور مستقل Contract و Procurement با `NotConfigured/SetupRequired/NoData/Available/Suspended` دارد و در Operational یا Financial Health ادغام نمی‌شود.
- Finance علاوه بر `contractReference` تاریخی، می‌تواند به `contractId` و `commitmentId` واقعی و هم‌پروژه متصل شود. اعتبار این شناسه‌ها از Integration Contract ماژول Commercial بررسی می‌شود؛ Finance جدول Commercial را مستقیم نمی‌خواند.
- تمام commandها Tenant/Project scoped، permission controlled، revision controlled و idempotent هستند و Audit/Outbox را در همان transaction می‌نویسند.

## Consequences

- پروژه بدون WBS، بودجه، سقف اولیه قرارداد یا برآورد اولیه خرید همچنان می‌تواند واقعیت تجاری را ثبت کند.
- نبود داده یا قابلیت غیرفعال هرگز به وضعیت سالم یا مقدار صفر تبدیل نمی‌شود.
- مدیر می‌تواند قراردادهای منقضی، approvals معطل و تعهدات سررسیدگذشته را جدا از وضعیت عملیاتی ببیند.
- چندارزی/FX، انبار و رسید کالا، RFQ/Tender، سه‌طرفه‌سازی Invoice/Order/Receipt و پرداخت قراردادی تفصیلی به Sliceهای بعدی نیاز دارند.

