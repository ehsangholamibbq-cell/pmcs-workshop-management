# Permission foundation

مدل Foundation سه لایه را ترکیب می‌کند:

1. حساب مستقل کاربر در Tenant؛
2. نقش Tenant برای دسترسی Portfolio یا مدیریت سازمان؛
3. عضویت پروژه با Permission Code در سطح Module/Operation.

Permissionهای فعال در Vertical Slice اول:

- `projects.read`
- `projects.read-all`
- `projects.create`
- `field.daily-reports.read`
- `field.daily-reports.capture`
- `field.daily-reports.submit`
- `field.daily-reports.review`
- `planning.measurement-items.read`
- `planning.measurement-items.manage`
- `planning.progress.read`
- `planning.baselines.read`
- `planning.baselines.capture`
- `planning.baselines.submit`
- `planning.baselines.review`
- `planning.milestones.read`
- `planning.milestones.capture`
- `planning.milestones.submit`
- `planning.milestones.review`
- `technical.read`
- `technical.documents.create`
- `technical.documents.create-revision`
- `technical.documents.submit`
- `technical.documents.review`
- `technical.confidential.read`
- `technical.confidential.manage`
- `technical.transmittals.create`
- `technical.transmittals.issue`
- `technical.transmittals.acknowledge`
- `technical.rfis.create`
- `technical.rfis.submit`
- `technical.rfis.issue`
- `technical.rfis.respond`
- `technical.rfis.accept`
- `technical.rfis.close`
- `technical.submittals.create`
- `technical.submittals.submit`
- `technical.submittals.review`
- `technical.submittals.close`
- `project-state.read`
- `project-state.recalculate`
- `evidence.read`
- `evidence.upload`
- `actions.read`
- `actions.create`
- `actions.update`
- `actions.manage`
- `attention.triage`
- `projects.activate`
- `projects.calendar.configure`
- `projects.planning.configure`
- `projects.locations.manage`
- `projects.setup.configure`
- `projects.setup.configure-sensitive`
- `sync.conflicts.manage`
- `finance.records.read`
- `finance.records.capture`
- `finance.records.submit`
- `finance.records.review`
- `financial-state.read`
- `budget.baselines.read`
- `budget.baselines.capture`
- `budget.baselines.submit`
- `budget.baselines.review`
- `commercial.parties.read`
- `commercial.parties.manage`
- `contracts.read`
- `contracts.capture`
- `contracts.submit`
- `contracts.review`
- `contracts.amendments.capture`
- `contracts.amendments.submit`
- `contracts.amendments.review`
- `procurement.requests.read`
- `procurement.requests.capture`
- `procurement.requests.submit`
- `procurement.requests.review`
- `procurement.orders.read`
- `procurement.orders.issue`
- `procurement.orders.manage`
- `commercial-state.read`
- `supply.read`
- `supply.items.manage`
- `supply.locations.manage`
- `supply.receipts.capture`
- `supply.receipts.override-excess`
- `supply.inspections.review`
- `supply.inventory.post`
- `supply.inventory.issue`
- `supply.inventory.acknowledge`
- `supply.inventory.reconcile`
- `supply.inventory.transfer`
- `supply.inventory.count`
- `supply.inventory.adjust`
- `supply.services.accept`
- `insights.view`
- `insights.generate`
- `insights.review`

Endpoint تشخیصی `GET /api/v1/identity/permissions/preview?userId=...&projectId=...&proposedRoleCode=...` برای مدیر سازمان، نتیجه مؤثر فعلی یا نقش پیشنهادی قبل از ذخیره را با وضعیت مجاز/رد، منبع نقش، دامنه Tenant/Project، شرط عضویت فعال و تاریخ‌دار، علت رد، انقضا، نسخه Policy و اثر Delegation نمایش می‌دهد. شبیه‌سازی نقش پیشنهادی صریحاً با منبع `ProposedProjectRole` مشخص می‌شود. این Endpoint فقط‌خواندنی است و هیچ Grant یا Permission جدید ایجاد نمی‌کند.

نقش‌های پایه و دسترسی فعلی:

| نقش پروژه | دسترسی عملیاتی فعلی |
| --- | --- |
| `ProjectManager` | همه عملیات پروژه، شامل Recalculate، Evidence، Triage و مدیریت همه Actionها |
| `SiteSupervisor` | ثبت/Submit گزارش، Evidence، مشاهده مبنا/دفتر پیشرفت، ثبت دریافت و تحویل/تسویه امانی ماده، شمارش موجودی، ثبت و ارسال وضعیت Milestone، ساخت پیش‌نویس مدرک/RFI و Transition Actionهای واگذارشده به خودش |
| `TechnicalOffice` | گردش کامل Document Revision، Transmittal، RFI و Submittal غیرمحرمانه؛ مدیریت قلم، تصمیم بازرسی و پذیرش خدمت |
| `Observer` | مشاهده گزارش، Evidence، Action، Project State، Baseline، Milestone، دفتر پیشرفت، دفتر فنی و واقعیت موجودی |
| `FinanceOperator` | مشاهده/ثبت/Submit اسناد مالی و Budget Baseline و مشاهده Financial State و واقعیت پذیرفته‌شده تأمین |
| `FinanceManager` | همه عملیات FinanceOperator به‌اضافه Review/Post و تأیید بودجه؛ بدون اختیار ضمنی پیکربندی پروژه |
| `ContractAdministrator` | مدیریت Party و قرارداد/الحاقیه، گردش کامل مدارک و پذیرش خدمت بدون اختیار اصلاح موجودی کالا |
| `ProcurementOperator` | ثبت/ارسال درخواست خرید، مدیریت قلم/محل، ثبت دریافت فیزیکی و مشاهده سفارش/واقعیت تأمین |
| `ProcurementManager` | همه عملیات تدارکات و موجودی، شامل اضافه‌تحویل، بازرسی، ورود موجودی، انتقال و اصلاح کنترل‌شده |

`TenantAdministrator` همه Permissionهای Tenant و پروژه را دارد. `PortfolioViewer` می‌تواند پروژه‌ها، Actionها، Project State، Financial State، Commercial State، Baseline/Milestone، دفتر پیشرفت، دفتر فنی، واقعیت تدارکات/موجودی و Insightهای موجود همه پروژه‌های Tenant را فقط بخواند؛ اجازه Recalculate، تولید/بازبینی Insight، Upload، Triage یا تغییر داده رسمی را ندارد. Endpointها Tenant scope، عضویت فعال پروژه و Permission عملیات را هم‌زمان بررسی می‌کنند. ثبت دریافت، تصمیم بازرسی، ورود موجودی، تحویل، تأیید تحویل، تسویه امانی، شمارش و تصویب اصلاح Permissionهای مستقل دارند. Role/Grantهای فعلی Policy ثابت Foundation هستند و Preview دقیقاً همین واقعیت را توضیح می‌دهد. Explicit Deny، Delegation و Grantهای سفارشی Location/Contract/Party/Own/Assigned هنوز پیاده‌سازی نشده‌اند و Preview آن‌ها را `NotConfigured` گزارش می‌کند؛ مدیریت نسخه‌دار این Policyها یک Slice مستقل V1 است.

مدارک دارای برچسب محرمانگی و همهٔ Revision، RFI، Submittal و Transmittal وابسته به آن‌ها فقط برای `ProjectManager`، `TenantAdministrator` یا `ContractAdministrator` دارای `technical.confidential.read` قابل مشاهده و اقدام‌اند. ایجاد مدرک محرمانه یا Revision جدید برای آن علاوه بر Permission گردش عمومی به `technical.confidential.manage` نیاز دارد؛ کنترل هم در Query و هم در Command به‌صورت fail-closed اجرا می‌شود.
