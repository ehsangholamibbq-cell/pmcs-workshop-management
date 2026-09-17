# Post-V1 checkpoint — Visual Direction, Member Profile and Project Bootstrap Decisions

- Checkpoint ID: `PMCS-PV1-DEC-01`
- Date: ۱۴۰۵/۰۶/۲۶ (۲۰۲۶-۰۹-۱۷)
- Status: Decision Baseline Registered
- Runtime change: None
- Active product line: `PMCS V1.1`
- Parent locked product baseline: `PMCS V1`
- Parent source commit: `26bf222d44634562ca7f3fc0931f3f8b79ca04a1`

## Decisions registered

- مالک محصول مسیر بصری «مدیریت ممتاز» را تأیید کرد؛
- سه معیار ترکیب Login، گرمی محیط و شدت Motion تأیید شدند؛
- لوگوی رسمی بتن بسپار قزوین نشان PMCS است؛
- سرمه‌ای برند رنگ پایه و سبز/نقره‌ای Accent محدود هستند؛ Surfaceها گرم و کم‌خستگی باقی می‌مانند؛
- Login معماری با Wireframe متحرک و درخشش کوتاه مجاز است، مشروط به performance budget و reduced-motion؛
- گرافیک Login از Authentication جدا و با Descriptor/Asset نسخه‌دار، Preview، Publish و Rollback قابل تغییر است؛
- همهٔ اعضا Profile شخصی حداقلی و تصویر قابل مدیریت دارند؛
- Profile شخصی از Project Membership/Role جدا است؛
- ساخت پروژه از روی پروژهٔ موجود با انتخاب Setup و Member، Preview و Confirmation در Scope V1.1 ثبت شد؛
- داده‌های عملیاتی، مالی، فایل، پیام، Audit، Sync و سابقه به‌صورت ضمنی قابل کپی نیستند.

## Documents updated

- `docs/roadmaps/pmcs-visual-excellence-program.md` نسخه `1.1.0`؛
- `docs/roadmaps/pmcs-post-v1-product-evolution.md` نسخه `1.2.0`؛
- `docs/adr/0028-configurable-login-member-profile-project-bootstrap.md`؛
- `docs/roadmaps/README.md`.

## Gate effect

`VX-G2 Direction Approved` برای انتخاب Art Direction بسته شد. این Checkpoint موارد زیر را تکمیل‌شده اعلام نمی‌کند:

- `V1.1-G0 Governance Approved`؛
- `VX-G1` evidence inventory؛
- `VX-G3 Design System Ready`؛
- کدنویسی Runtime؛
- Migration یا Qualification V1.1.

## Required next work

1. تعیین `repositoryStartCommit` روی Worktree پاک و بستن `V1.1-G0`؛
2. تکمیل ADR/Permission/API/Event/Test contracts تفصیلی؛
3. تکمیل Visual Audit و Design Token/Component contract؛
4. افزودن Profile و Project Duplication Wizard به Review Pack کامل؛
5. آغاز Runtime فقط پس از Gateهای Governance و UX مربوط.

