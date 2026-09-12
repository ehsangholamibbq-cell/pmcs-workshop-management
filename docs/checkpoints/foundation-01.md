# Foundation checkpoint 01

- Date: 2026-09-09
- Status: Implemented and locally validated
- Product source: PMCS Product & System Blueprint V1.3

## Outcome

اولین برش اجرایی از مسیر «ثبت واقعیت → پذیرش سرور → داده رسمی» آماده است. کاربر کارگاه می‌تواند یک واقعیت روزانه را بدون اینترنت ذخیره کند؛ Client آن را به‌صورت Operation پایدار نگه می‌دارد و هنگام اتصال برای ارزیابی مجدد Permission و Domain Rule به سرور می‌فرستد.

## Implemented

- Monorepo و Solution مبتنی بر .NET 10 و Next.js 16؛
- Modular Monolith با ماژول‌های Platform، Identity & Access، Projects و Field Operations؛
- PostgreSQL schema migrations با ترتیب قطعی و advisory lock؛
- Project Setup با Contract، Planning، Budget، Quality و HSE مستقل؛
- حالت معتبر پروژه بدون WBS، بدون Budget Baseline و بدون HSE؛
- Tenant role، Project membership و Permission code در سطح عملیات؛
- Daily Report aggregate با Draft/Submit، Factهای ساختاریافته و Revision check؛
- Audit record، durable outbox scaffold و idempotency record؛
- Operation-based Sync با `Applied`، `Conflict`، `Rejected` و `Unsupported`؛
- PWA RTL، Service Worker، IndexedDB queue، recovery و automatic retry؛
- Seed مصنوعی و قطعی برای توسعه؛
- Docker Compose برای PostgreSQL، MinIO، API و Web؛
- GitHub Actions و architecture guard.

## Synthetic development identity

- Tenant: `11111111-1111-1111-1111-111111111111`
- User: `22222222-2222-2222-2222-222222222222`
- Project: `33333333-3333-3333-3333-333333333333`
- Membership: `44444444-4444-4444-4444-444444444444`

هیچ‌یک از این موارد داده واقعی کاربر نیست و Seed فقط در Development و با flag صریح اجرا می‌شود.

## Verification evidence

- .NET restore: successful؛
- .NET Release build: ۶ پروژه محصول + پروژه تست، ۰ warning و ۰ error؛
- Domain tests: ۷/۷ passed؛
- Frontend unit tests: ۴/۴ passed؛
- ESLint: passed؛
- Next.js production build و TypeScript: passed؛
- Production shell smoke: `/`، `/manifest.webmanifest` و `/sw.js` همگی HTTP 200؛
- Repository architecture validation و whitespace check: passed.

## Not production-ready yet

- Authentication واقعی/OIDC هنوز جایگزین Development identity adapter نشده است.
- Role policyها هنوز ثابت‌اند و UI مدیریت Role/Grant ندارند.
- Audit، Outbox و business write در این checkpoint در transaction واحد ذخیره نمی‌شوند؛ transactional boundary باید پیش از Pilot بسته شود.
- Integration test با PostgreSQL و اجرای Docker Compose در محیط فعلی انجام نشد، چون runtime کانتینر/PostgreSQL در دسترس نبود.
- Photo/file upload، S3 binding، فرم‌های تخصصی نیروی انسانی/مصالح/تجهیزات، Approval کامل و Project State engine هنوز در Sprintهای بعدی هستند.
- AI عمداً هنوز وارد runtime نشده است؛ ابتدا باید داده رسمی و engine قطعی شکل بگیرد.

## Next checkpoint

Vertical Slice «گزارش روزانه واقعی کارگاه»:

1. transactional write برای Aggregate + Audit + Outbox + Idempotency؛
2. فرم‌های Labor، Equipment، Material، Progress، Issue و Stoppage؛
3. Photo queue و S3 upload session؛
4. Submit/Return/Approve workflow و Inbox؛
5. sync conflict review screen؛
6. اولین Project State projection بر پایه Coverage و داده تأییدشده، بدون وابستگی اجباری به WBS یا Budget.
