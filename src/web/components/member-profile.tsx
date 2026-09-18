"use client";

import Image from "next/image";
import Link from "next/link";
import { type ChangeEvent, type FormEvent, useCallback, useEffect, useMemo, useState } from "react";
import {
  enqueueDocumentUpload,
  syncPendingDocumentUploads,
} from "@/lib/document-upload-queue";
import {
  loadDocumentState,
  loadMyProfile,
  profileAssetUrl,
  releaseOwnProfileImage,
  updateMyProfile,
  type MemberProfileModel,
  type ProfileAvatarCrop,
} from "@/lib/member-profile";
import { toUserMessage } from "@/lib/localization";
import { SessionBadge, usePmcsSession } from "@/components/pmcs-session";

const maximumAvatarBytes = 5 * 1024 * 1024;

export function MemberProfileEditor() {
  const session = usePmcsSession();
  const [profile, setProfile] = useState<MemberProfileModel | null>(null);
  const [displayName, setDisplayName] = useState("");
  const [jobTitle, setJobTitle] = useState("");
  const [workPhone, setWorkPhone] = useState("");
  const [avatarDocumentId, setAvatarDocumentId] = useState<string | null>(null);
  const [cropX, setCropX] = useState(0);
  const [cropY, setCropY] = useState(0);
  const [message, setMessage] = useState("در حال دریافت پروفایل…");
  const [isBusy, setIsBusy] = useState(false);

  const applyProfile = useCallback((loaded: MemberProfileModel): void => {
    setProfile(loaded);
    setDisplayName(loaded.displayName);
    setJobTitle(loaded.jobTitle ?? "");
    setWorkPhone(loaded.workPhone ?? "");
    setAvatarDocumentId(loaded.avatarDocumentId);
    setCropX(Math.round((loaded.avatarCrop?.x ?? 0) * 100));
    setCropY(Math.round((loaded.avatarCrop?.y ?? 0) * 100));
  }, []);

  useEffect(() => {
    let active = true;
    void loadMyProfile()
      .then((loaded) => {
        if (!active) return;
        applyProfile(loaded);
        setMessage("");
      })
      .catch((error: unknown) => {
        if (active) setMessage(toUserMessage(error, "دریافت پروفایل انجام نشد."));
      });
    return () => { active = false; };
  }, [applyProfile]);

  const avatarUrl = useMemo(() => profileAssetUrl(profile?.avatarUrl ?? null), [profile?.avatarUrl]);
  const crop = profile?.avatarCrop;

  async function save(event: FormEvent<HTMLFormElement>): Promise<void> {
    event.preventDefault();
    if (!profile) return;
    setIsBusy(true);
    setMessage("در حال ذخیره تغییرات…");
    try {
      const loaded = await updateMyProfile(buildUpdate(profile, avatarDocumentId));
      applyProfile(loaded);
      setMessage("پروفایل با موفقیت به‌روزرسانی شد.");
    } catch (error) {
      setMessage(toUserMessage(error, "ذخیره پروفایل انجام نشد."));
    } finally {
      setIsBusy(false);
    }
  }

  async function selectAvatar(event: ChangeEvent<HTMLInputElement>): Promise<void> {
    const file = event.target.files?.[0];
    event.target.value = "";
    if (!file || !profile) return;
    if (!file.type.startsWith("image/") || file.size <= 0 || file.size > maximumAvatarBytes) {
      setMessage("تصویر باید یکی از فرمت‌های تصویری مجاز و حداکثر ۵ مگابایت باشد.");
      return;
    }

    setIsBusy(true);
    setMessage("در حال اعتبارسنجی، اسکن امنیتی و ثبت تصویر…");
    try {
      const queued = await enqueueDocumentUpload({
        tenantId: session.tenantId,
        userId: session.userId,
        projectId: null,
        ownerType: "MemberProfile",
        ownerId: session.userId,
        file,
        classification: "Confidential",
        retentionPolicy: "Standard",
      });
      const summary = await syncPendingDocumentUploads("/api/pmcs", null);
      if (summary.rejected > 0 || summary.deferred > 0) {
        throw new Error("تصویر در کنترل امنیتی پذیرفته نشد یا ارسال آن کامل نشد.");
      }

      let document = await loadDocumentState(queued.assetId);
      if (document.status === "Quarantined") {
        document = await releaseOwnProfileImage(document);
      }
      if (document.status !== "Released") {
        throw new Error("تصویر هنوز به وضعیت امن و قابل استفاده نرسیده است.");
      }

      const loaded = await updateMyProfile(buildUpdate(profile, document.id, true));
      applyProfile(loaded);
      setMessage("تصویر پروفایل پس از کنترل امنیتی ثبت شد.");
    } catch (error) {
      setMessage(toUserMessage(error, "ثبت تصویر پروفایل انجام نشد."));
    } finally {
      setIsBusy(false);
    }
  }

  async function removeAvatar(): Promise<void> {
    if (!profile) return;
    setIsBusy(true);
    setMessage("در حال حذف ارتباط تصویر…");
    try {
      const loaded = await updateMyProfile(buildUpdate(profile, null));
      applyProfile(loaded);
      setMessage("تصویر پروفایل برداشته شد؛ فایل قبلی طبق سیاست نگهداری حفظ می‌شود.");
    } catch (error) {
      setMessage(toUserMessage(error, "حذف تصویر انجام نشد."));
    } finally {
      setIsBusy(false);
    }
  }

  function currentCrop(documentId: string | null, reset = false): ProfileAvatarCrop | null {
    if (!documentId) return null;
    if (reset) return { x: 0, y: 0, width: 1, height: 1 };
    if (cropX === 0 && cropY === 0 && crop?.width === 1 && crop?.height === 1) {
      return { x: 0, y: 0, width: 1, height: 1 };
    }
    return { x: cropX / 100, y: cropY / 100, width: 0.8, height: 0.8 };
  }

  function buildUpdate(
    current: MemberProfileModel,
    documentId: string | null,
    resetCrop = false,
  ) {
    return {
      baseUserRevision: current.userRevision,
      baseProfileRevision: current.profileRevision,
      displayName,
      jobTitle: jobTitle.trim() || null,
      workPhone: workPhone.trim() || null,
      avatarDocumentId: documentId,
      avatarCrop: currentCrop(documentId, resetCrop),
    };
  }

  return (
    <main className="profile-page">
      <header className="profile-topbar">
        <div>
          <Link href="/portfolio">بازگشت به سبد پروژه‌ها</Link>
          <span>هویت شخصی در تمام پروژه‌ها یکسان می‌ماند</span>
        </div>
        <SessionBadge />
      </header>
      <section className="profile-hero">
        <div className="profile-avatar-frame">
          {avatarUrl ? (
            <Image
              key={avatarUrl}
              src={avatarUrl}
              alt={`تصویر ${profile?.displayName ?? "کاربر"}`}
              fill
              unoptimized
              sizes="176px"
              style={{ objectPosition: `${50 + cropX * 2}% ${50 + cropY * 2}%` }}
            />
          ) : (
            <span>{initials(displayName || session.displayName)}</span>
          )}
        </div>
        <div>
          <p className="eyebrow">پروفایل شخصی</p>
          <h1>{displayName || session.displayName}</h1>
          <p>{profile?.organizationUnit ?? "واحد سازمانی هنوز توسط مدیر ثبت نشده است"}</p>
        </div>
      </section>
      <section className="profile-workspace">
        <form onSubmit={(event) => void save(event)}>
          <div className="section-title">
            <div>
              <p className="eyebrow">اطلاعات مجاز</p>
              <h2>مشخصات کاری من</h2>
            </div>
            <span className="profile-privacy">نمایش فقط در محدوده‌های مجاز</span>
          </div>
          <div className="profile-form-grid">
            <label>
              نام نمایشی
              <input value={displayName} maxLength={200} required onChange={(event) => setDisplayName(event.target.value)} />
            </label>
            <label>
              ایمیل سازمانی
              <input value={profile?.email ?? session.email} disabled readOnly />
            </label>
            <label>
              عنوان شغلی
              <input value={jobTitle} maxLength={160} onChange={(event) => setJobTitle(event.target.value)} />
            </label>
            <label>
              تلفن کاری
              <input value={workPhone} maxLength={40} inputMode="tel" onChange={(event) => setWorkPhone(event.target.value)} />
            </label>
            <label>
              واحد سازمانی
              <input value={profile?.organizationUnit ?? ""} disabled readOnly placeholder="توسط مدیر سازمان تعیین می‌شود" />
            </label>
          </div>
          <div className="profile-photo-controls">
            <div>
              <strong>تصویر پروفایل</strong>
              <small>فقط تصویر امن، حداکثر ۵ مگابایت؛ فایل از اسکن و قرنطینه عبور می‌کند.</small>
            </div>
            <label className="file-button" aria-disabled={isBusy}>
              انتخاب تصویر
              <input
                type="file"
                accept="image/jpeg,image/png,image/webp,image/heic,image/heif"
                disabled={isBusy}
                onChange={(event) => void selectAvatar(event)}
              />
            </label>
            {avatarDocumentId && (
              <button className="secondary-button" type="button" disabled={isBusy} onClick={() => void removeAvatar()}>
                برداشتن تصویر
              </button>
            )}
          </div>
          {avatarDocumentId && (
            <div className="profile-crop-controls">
              <label>تنظیم افقی قاب <input type="range" min="0" max="20" value={cropX} onChange={(event) => setCropX(Number(event.target.value))} /></label>
              <label>تنظیم عمودی قاب <input type="range" min="0" max="20" value={cropY} onChange={(event) => setCropY(Number(event.target.value))} /></label>
            </div>
          )}
          <div className="profile-form-actions">
            <button type="submit" disabled={!profile || isBusy}>{isBusy ? "در حال انجام…" : "ذخیره پروفایل"}</button>
            <output aria-live="polite">{message}</output>
          </div>
        </form>
        <aside>
          <p className="eyebrow">مرز هویت</p>
          <h2>یک پروفایل، چند پروژه</h2>
          <p>تصویر و مشخصات شخصی شما میان پروژه‌ها تکرار نمی‌شود. نقش، عضویت و سطح دسترسی هر پروژه مستقل است.</p>
          <dl>
            <div><dt>شناسه عضو</dt><dd>{shortId(session.userId)}</dd></div>
            <div><dt>نقش سازمانی</dt><dd>{tenantRoleLabel(session.tenantRole)}</dd></div>
            <div><dt>آخرین ویرایش</dt><dd>{profile ? new Date(profile.updatedAt).toLocaleString("fa-IR") : "—"}</dd></div>
          </dl>
        </aside>
      </section>
    </main>
  );
}

function initials(value: string): string {
  return value.trim().split(/\s+/u).slice(0, 2).map((part) => part[0] ?? "").join("");
}

function shortId(value: string): string {
  return `${value.slice(0, 8)}…${value.slice(-4)}`;
}

function tenantRoleLabel(role: string): string {
  return ({
    TenantAdministrator: "مدیر سازمان",
    PortfolioViewer: "مشاهده‌گر سبد",
    Member: "عضو سازمان",
  } as Record<string, string>)[role] ?? role;
}
