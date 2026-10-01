# PMCS V1.1 — Permission Catalog اولیه

- شناسه: `PMCS-SEC-V1.1-PERM-001`
- نسخه: `1.0.0`
- وضعیت: Governance Contract؛ Role mapping در Checkpoint مالک قابلیت انجام می‌شود

## ۱. قواعد حاکم

- تصمیم نهایی فقط در Permission Engine مرکزی PMCS گرفته می‌شود؛
- هیچ Header، Token آزمایشی، UI state یا Agent prompt حق تزریق Role ندارد؛
- Deny پیش‌فرض است و نبود Project/Tenant context به Deny منجر می‌شود؛
- Role فقط مجموعه Permission است و در Domain code hard-code نمی‌شود؛
- Permissionهای Self، Tenant و Project از هم تفکیک می‌شوند؛
- هر Mutation دارای Audit، Actor، Correlation و در صورت نیاز Idempotency است؛
- Agent دقیقاً Permission کاربر فعلی را به ارث می‌برد و Permission جدید تولید نمی‌کند.

## ۲. Catalog

| Permission | Scope | Risk | کاربرد |
| --- | --- | --- | --- |
| `platform.modules.read` | Tenant | Low | مشاهده Capability/Moduleهای فعال |
| `platform.modules.manage` | Tenant | High | فعال‌سازی Feature/Module سازگار |
| `login-experience.manage` | Tenant | High | ساخت Draft، پیش‌نمایش، انتشار و Rollback طرح Login بدون دسترسی به Authentication |
| `member-profile.read-self` | Self | Low | مشاهده پروفایل خود |
| `member-profile.update-self` | Self | Medium | ویرایش فیلدهای Self-service |
| `member-profile.avatar.publish-self` | Self | Medium | Release تصویر پاک و policy-constrained متعلق به پروفایل خود |
| `member-profile.read-directory` | Tenant/Project | Medium | مشاهده Directory فقط در محدوده مجاز یا پروژه مشترک |
| `member-profile.manage-directory` | Tenant | High | اصلاح فیلدهای سازمانی مجاز |
| `projects.bootstrap.preview` | Project/Tenant | Medium | Dry-run ساخت از پروژه موجود |
| `projects.bootstrap.create` | Tenant | High | ایجاد مقصد Draft و اجرای Setup clone |
| `projects.bootstrap.members_copy` | Source+Target Project | High | ایجاد Membershipهای انتخاب‌شده |
| `projects.bootstrap.activate` | Target Project | High | فعال‌سازی مستقل پروژه مقصد |
| `documents.read` | Owner/Project | Medium | خواندن فایل Released مجاز |
| `documents.upload` | Owner/Project | Medium | ایجاد Upload Session |
| `documents.classify` | Project/Tenant | High | تعیین Classification/Retention |
| `documents.quarantine.release` | Tenant | Critical | Release کنترل‌شده پس از Scanner policy |
| `collaboration.room.read` | Project | Low | مشاهده Room پروژه |
| `collaboration.message.send` | Project | Medium | ارسال پیام در Room |
| `collaboration.attachment.upload` | Project | Medium | پیوست از Shared Documents |
| `collaboration.message.moderate` | Project | High | Moderation با history/Audit |
| `collaboration.record.convert` | Project+Domain | High | تبدیل صریح به رکورد رسمی |
| `reporting.catalog.read` | Project/Tenant | Low | مشاهده گزارش‌های مجاز |
| `reporting.run.create` | Project/Tenant | Medium | اجرای گزارش استاندارد |
| `reporting.output.download` | Project/Tenant | Medium | دانلود خروجی مجاز |
| `reporting.template.publish` | Tenant | High | انتشار Template Certified |
| `intelligence.session.create` | Project/Tenant | Medium | ایجاد Session مرحله Foundation |
| `intelligence.tool.invoke_read` | Inherited | High | فراخوانی Tool خواندنی با Permission کاربر |
| `intelligence.audit.read` | Tenant | High | مشاهده lineage و Audit مجاز |

## ۳. Separation of Duties

- `login-experience.manage` هیچ دسترسی‌ای به OIDC، Credential، Session یا تنظیمات Authentication ایجاد نمی‌کند؛
- `member-profile.avatar.publish-self` فقط روی تصویر پاک، محدود به مالک فعلی و سیاست ثابت Profile عمل می‌کند و معادل `documents.quarantine.release` نیست؛
- `documents.quarantine.release` از Upload و Classification جدا است؛
- `projects.bootstrap.create` به‌تنهایی اجازه کپی اعضا یا Activation نمی‌دهد؛
- `collaboration.record.convert` علاوه بر Permission Chat، Permission Domain مقصد را نیز لازم دارد؛
- `reporting.template.publish` از اجرای گزارش جدا است؛
- Stage 1 Agent هیچ Permission نوشتن Domain ندارد.

## ۴. ارزیابی Project Bootstrap

برای انتقال هر عضو، Engine باید هر دو محدوده را کنترل کند:

1. Actor حق خواندن Membership/Role مبدأ را داشته باشد؛
2. Actor حق اعطای Role/Scope متناظر در مقصد را داشته باشد.

دسترسی‌ای که Actor حق اعطای آن را ندارد در Preview با `Blocked` نمایش داده می‌شود و اجرای کل یا آن ردیف طبق Policy صریح Fail می‌شود؛ Silent escalation ممنوع است.

## ۵. Gate تست

هر Permission جدید باید دست‌کم Allow، Deny، No Context، Cross Tenant، Cross Project، Suspended Actor، Revoked Membership و Agent-inherited cases داشته باشد. UI پنهان‌شدن کنترل را به‌عنوان شواهد Permission نمی‌پذیرد؛ API رفتار نهایی را تعیین می‌کند.
