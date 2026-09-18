"use client";

import Link from "next/link";
import { type ChangeEvent, type FormEvent, useCallback, useEffect, useState } from "react";
import {
  enqueueDocumentUpload,
  syncPendingDocumentUploads,
} from "@/lib/document-upload-queue";
import {
  activateLoginExperience,
  createLoginExperience,
  listLoginExperiences,
  type LoginExperienceVersion,
} from "@/lib/login-experience-administration";
import type {
  LoginAccentPalette,
  LoginCompositionVariant,
  LoginMotionPolicy,
  LoginSurfaceTone,
} from "@/lib/login-experience";
import { loadDocumentState, releaseOwnProfileImage } from "@/lib/member-profile";
import { toUserMessage } from "@/lib/localization";
import { SessionBadge, usePmcsSession } from "@/components/pmcs-session";

export function LoginExperienceAdministration() {
  const session = usePmcsSession();
  const [versions, setVersions] = useState<readonly LoginExperienceVersion[]>([]);
  const [compositionVariant, setCompositionVariant] = useState<LoginCompositionVariant>("BlueprintSplit");
  const [surfaceTone, setSurfaceTone] = useState<LoginSurfaceTone>("WarmStone");
  const [accentPalette, setAccentPalette] = useState<LoginAccentPalette>("CorporateNavyGreen");
  const [motionPolicy, setMotionPolicy] = useState<LoginMotionPolicy>("Balanced");
  const [eyebrow, setEyebrow] = useState("سامانه جامع مدیریت پروژه");
  const [headline, setHeadline] = useState("ساختن، فراتر از امروز");
  const [supportingText, setSupportingText] = useState("مرکز فرمان یکپارچه برای تصمیم‌های دقیق، قابل ردیابی و مبتنی بر واقعیت پروژه.");
  const [logo, setLogo] = useState<File | null>(null);
  const [hero, setHero] = useState<File | null>(null);
  const [message, setMessage] = useState("در حال دریافت نسخه‌ها…");
  const [isBusy, setIsBusy] = useState(false);

  const refresh = useCallback(async (): Promise<void> => {
    try {
      setVersions(await listLoginExperiences());
      setMessage("");
    } catch (error) {
      setMessage(toUserMessage(error, "دریافت نسخه‌های صفحه ورود انجام نشد."));
    }
  }, []);

  useEffect(() => {
    const timeoutId = window.setTimeout(() => { void refresh(); }, 0);
    return () => window.clearTimeout(timeoutId);
  }, [refresh]);

  async function createDraft(event: FormEvent<HTMLFormElement>): Promise<void> {
    event.preventDefault();
    setIsBusy(true);
    setMessage("در حال ساخت نسخه جدید و کنترل امنیتی دارایی‌های تصویری…");
    const descriptorId = crypto.randomUUID();
    try {
      const logoDocumentId = await uploadAsset(descriptorId, logo);
      const heroDocumentId = await uploadAsset(descriptorId, hero);
      if (logo || hero) {
        const summary = await syncPendingDocumentUploads("/api/pmcs", null);
        if (summary.rejected > 0 || summary.deferred > 0) {
          throw new Error("یک یا چند دارایی تصویری در کنترل امنیتی پذیرفته نشد یا ارسال آن کامل نشد.");
        }
      }

      for (const documentId of [logoDocumentId, heroDocumentId]) {
        if (!documentId) continue;
        let document = await loadDocumentState(documentId);
        if (document.status === "Quarantined") document = await releaseOwnProfileImage(document);
        if (document.status !== "Released") throw new Error("دارایی تصویری صفحه ورود هنوز قابل انتشار نیست.");
      }

      await createLoginExperience({
        clientGeneratedId: descriptorId,
        compositionVariant,
        surfaceTone,
        accentPalette,
        motionPolicy,
        eyebrow,
        headline,
        supportingText,
        logoDocumentId,
        heroDocumentId,
      });
      setLogo(null);
      setHero(null);
      await refresh();
      setMessage("نسخه پیش‌نویس ساخته شد؛ پس از بازبینی می‌توانید آن را منتشر کنید.");
    } catch (error) {
      setMessage(toUserMessage(error, "ساخت نسخه صفحه ورود انجام نشد."));
    } finally {
      setIsBusy(false);
    }
  }

  async function uploadAsset(ownerId: string, file: File | null): Promise<string | null> {
    if (!file) return null;
    if (!file.type.startsWith("image/") || file.size <= 0 || file.size > 10 * 1024 * 1024) {
      throw new Error("دارایی باید تصویر امن و حداکثر ۱۰ مگابایت باشد.");
    }
    const queued = await enqueueDocumentUpload({
      tenantId: session.tenantId,
      userId: session.userId,
      ownerType: "LoginExperience",
      ownerId,
      projectId: null,
      file,
      classification: "Internal",
      retentionPolicy: "Standard",
    });
    return queued.assetId;
  }

  async function activate(version: LoginExperienceVersion): Promise<void> {
    setIsBusy(true);
    setMessage(version.status === "Draft" ? "در حال انتشار نسخه…" : "در حال بازگشت کنترل‌شده…");
    try {
      await activateLoginExperience(version);
      await refresh();
      setMessage(version.status === "Draft" ? "نسخه جدید منتشر شد." : "نسخه انتخاب‌شده دوباره فعال شد.");
    } catch (error) {
      setMessage(toUserMessage(error, "فعال‌سازی نسخه انجام نشد."));
    } finally {
      setIsBusy(false);
    }
  }

  function selectFile(setter: (file: File | null) => void, event: ChangeEvent<HTMLInputElement>): void {
    setter(event.target.files?.[0] ?? null);
  }

  if (session.tenantRole !== "TenantAdministrator") {
    return <main className="auth-state"><div className="auth-state-card"><h1>دسترسی محدود</h1><p>مدیریت ظاهر ورود فقط برای مدیر سازمان مجاز است.</p><Link href="/portfolio">بازگشت</Link></div></main>;
  }

  return (
    <main className="login-admin-page">
      <header className="login-admin-header">
        <div><Link href="/portfolio">بازگشت به سبد پروژه‌ها</Link><h1>مدیریت ظاهر صفحه ورود</h1><p>تنظیمات فقط از نشانه‌های طراحی و ترکیب‌های امن و ازپیش‌تأییدشده انتخاب می‌شوند.</p></div>
        <SessionBadge />
      </header>
      <section className="login-admin-grid">
        <form onSubmit={(event) => void createDraft(event)}>
          <div className="section-title"><div><p className="eyebrow">نسخه جدید</p><h2>ساخت پیش‌نویس</h2></div><span className="profile-privacy">بدون کد اجرایی دلخواه</span></div>
          <div className="login-admin-fields">
            <label>ترکیب صفحه<select value={compositionVariant} onChange={(event) => setCompositionVariant(event.target.value as LoginCompositionVariant)}><option value="BlueprintSplit">معماری دو بخشی</option><option value="MonolithFocus">تمرکز یکپارچه</option><option value="WarmMinimal">مینیمال گرم</option></select></label>
            <label>سطح رنگ<select value={surfaceTone} onChange={(event) => setSurfaceTone(event.target.value as LoginSurfaceTone)}><option value="WarmStone">سنگ گرم</option><option value="WarmIvory">عاج گرم</option><option value="DeepNavy">سرمه‌ای عمیق</option></select></label>
            <label>رنگ تأکیدی<select value={accentPalette} onChange={(event) => setAccentPalette(event.target.value as LoginAccentPalette)}><option value="CorporateNavyGreen">سرمه‌ای و سبز برند</option><option value="NavySilver">سرمه‌ای و نقره‌ای</option><option value="GreenStone">سبز و سنگی</option></select></label>
            <label>شدت حرکت<select value={motionPolicy} onChange={(event) => setMotionPolicy(event.target.value as LoginMotionPolicy)}><option value="Calm">آرام</option><option value="Balanced">متعادل</option><option value="Expressive">نمایان</option></select></label>
            <label>عنوان کوتاه<input value={eyebrow} maxLength={80} required onChange={(event) => setEyebrow(event.target.value)} /></label>
            <label>تیتر اصلی<input value={headline} maxLength={140} required onChange={(event) => setHeadline(event.target.value)} /></label>
            <label className="wide">متن همراه<textarea value={supportingText} maxLength={320} required onChange={(event) => setSupportingText(event.target.value)} /></label>
            <label>لوگو اختیاری<input type="file" accept="image/jpeg,image/png,image/webp,image/heic,image/heif" onChange={(event) => selectFile(setLogo, event)} /></label>
            <label>تصویر زمینه اختیاری<input type="file" accept="image/jpeg,image/png,image/webp,image/heic,image/heif" onChange={(event) => selectFile(setHero, event)} /></label>
          </div>
          <div className="login-admin-preview" data-tone={surfaceTone} data-accent={accentPalette}>
            <span>{eyebrow}</span><strong>{headline}</strong><p>{supportingText}</p><small>{compositionLabel(compositionVariant)} · موشن {motionLabel(motionPolicy)}</small>
          </div>
          <button type="submit" disabled={isBusy}>{isBusy ? "در حال انجام…" : "ساخت نسخه پیش‌نویس"}</button>
        </form>
        <section className="login-version-panel">
          <div className="section-title"><div><p className="eyebrow">تاریخچه</p><h2>نسخه‌های قابل بازگشت</h2></div></div>
          <div className="login-version-list">
            {versions.map((version) => (
              <article key={version.id}>
                <div><strong>نسخه {version.versionNumber.toLocaleString("fa-IR")}</strong><span data-status={version.status}>{statusLabel(version.status)}</span></div>
                <h3>{version.headline}</h3>
                <p>{compositionLabel(version.compositionVariant)} · {motionLabel(version.motionPolicy)}</p>
                <small>{new Date(version.createdAt).toLocaleString("fa-IR")}</small>
                {version.status !== "Published" && <button type="button" className="secondary-button" disabled={isBusy} onClick={() => void activate(version)}>{version.status === "Draft" ? "انتشار" : "بازگشت به این نسخه"}</button>}
              </article>
            ))}
            {versions.length === 0 && <p className="empty-state">هنوز نسخه‌ای ساخته نشده و طرح جایگزین داخلی امن فعال است.</p>}
          </div>
          <output aria-live="polite">{message}</output>
        </section>
      </section>
    </main>
  );
}

function compositionLabel(value: string): string {
  return ({ BlueprintSplit: "معماری دو بخشی", MonolithFocus: "تمرکز یکپارچه", WarmMinimal: "مینیمال گرم" } as Record<string, string>)[value] ?? value;
}

function motionLabel(value: string): string {
  return ({ Calm: "آرام", Balanced: "متعادل", Expressive: "نمایان" } as Record<string, string>)[value] ?? value;
}

function statusLabel(value: string): string {
  return ({ Draft: "پیش‌نویس", Published: "فعال", Superseded: "نسخه پیشین" } as Record<string, string>)[value] ?? value;
}
