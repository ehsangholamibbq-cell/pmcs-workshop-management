"use client";

import { useEffect, useState, useSyncExternalStore } from "react";
import Image from "next/image";
import { useRouter } from "next/navigation";
import { authClient } from "@/lib/auth-client";
import type { LoginExperienceDescriptor } from "@/lib/login-experience";
import { safeApplicationReturnPath } from "@/lib/return-path";

interface LoginPanelProps {
  readonly experience: LoginExperienceDescriptor;
  readonly initialError?: boolean;
}

export function LoginPanel({ experience, initialError = false }: LoginPanelProps) {
  const router = useRouter();
  const { data: session, isPending } = authClient.useSession();
  const isHydrated = useSyncExternalStore(subscribeToHydration, () => true, () => false);
  const [isStarting, setIsStarting] = useState(false);
  const [message, setMessage] = useState(
    initialError ? "ورود کامل نشد؛ اطلاعات حساب یا اتصال سرویس هویت را بررسی کنید." : "",
  );


  useEffect(() => {
    if (session) {
      router.replace(safeReturnTo());
    }
  }, [router, session]);

  async function startSignIn(): Promise<void> {
    setIsStarting(true);
    setMessage("");
    const result = await authClient.signIn.social({
      provider: "keycloak",
      callbackURL: safeReturnTo(),
      errorCallbackURL: "/login?error=identity",
      disableRedirect: true,
    });
    if (result.error || !result.data?.url) {
      setMessage("اتصال به سرویس ورود برقرار نشد؛ کمی بعد دوباره تلاش کنید.");
      setIsStarting(false);
      return;
    }

    window.location.assign(result.data.url);
  }

  return (
    <main
      className="login-shell"
      data-composition={experience.compositionVariant}
      data-surface={experience.surfaceTone}
      data-accent={experience.accentPalette}
      data-motion={experience.motionPolicy}
    >
      <section className="login-brand" aria-label="معرفی سامانه">
        <div className="login-hero-media" aria-hidden={experience.heroUrl ? undefined : true}>
          {experience.heroUrl && (
            <Image src={experience.heroUrl} alt="" fill priority unoptimized sizes="(max-width: 980px) 100vw, 65vw" />
          )}
        </div>
        <BlueprintMotion />
        <div className="login-brand-content">
          <div className="login-brand-lockup">
            {experience.logoUrl ? (
              <Image
                className="login-brand-logo"
                src={experience.logoUrl}
                alt="نشان سازمان"
                width={176}
                height={88}
                priority
                unoptimized
              />
            ) : (
              <div className="login-monogram" aria-label="بتن بسپار قزوین">ب‌ق</div>
            )}
            <span>بتن بسپار قزوین</span>
          </div>
          <p className="eyebrow">{experience.eyebrow}</p>
          <h1>{experience.headline}</h1>
          <p>{experience.supportingText}</p>
          <div className="login-trust-strip" aria-label="ویژگی‌های امنیتی ورود">
            <span>نشست امن سازمانی</span>
            <span>دسترسی محدوده‌محور</span>
            <span>ردیابی کامل تصمیم‌ها</span>
          </div>
        </div>
      </section>
      <section className="login-card" aria-labelledby="login-title">
        <div className="login-card-mark" aria-hidden="true"><span /></div>
        <div>
          <p className="eyebrow">درگاه یکپارچه سازمان</p>
          <h2 id="login-title">ورود به حساب کاربری</h2>
          <p>برای ادامه، احراز هویت امن در سرویس هویت سازمان انجام می‌شود.</p>
        </div>
        <button type="button" disabled={!isHydrated || isPending || isStarting || Boolean(session)} onClick={() => void startSignIn()}>
          {!isHydrated || isPending || isStarting || session ? "در حال بررسی…" : "ورود امن"}
        </button>
        {message && <p className="login-error" role="alert">{message}</p>}
        <small>رمز عبور و کد دومرحله‌ای در این برنامه دریافت یا ذخیره نمی‌شود.</small>
        <footer>
          <span>سامانه جامع پروژه</span>
          <span>نسخه ارائه {experience.version === 0 ? "پایه امن" : experience.version.toLocaleString("fa-IR")}</span>
        </footer>
      </section>
    </main>
  );
}

function BlueprintMotion() {
  return (
    <svg className="login-blueprint" viewBox="0 0 1200 900" aria-hidden="true" focusable="false">
      <g className="blueprint-grid">
        <path d="M80 750L560 270L1080 790" />
        <path d="M160 790L160 610L370 500L370 790" />
        <path d="M370 790L370 420L600 300L600 790" />
        <path d="M600 790L600 510L830 390L830 790" />
        <path d="M830 790L830 620L1040 510L1040 790" />
        <path d="M160 610L370 680L600 560L830 650L1040 560" />
        <path d="M270 555L270 790M485 360L485 790M715 450L715 790M935 565L935 790" />
      </g>
      <g className="blueprint-guides">
        <path d="M80 750H1080M160 610H1040M370 420H830M600 120V840" />
        <circle cx="600" cy="300" r="8" />
        <circle cx="370" cy="500" r="6" />
        <circle cx="830" cy="390" r="6" />
      </g>
      <path className="blueprint-glow" d="M80 750L560 270L1080 790" />
    </svg>
  );
}

function safeReturnTo(): string {
  if (typeof window === "undefined") return "/portfolio";
  return safeApplicationReturnPath(new URLSearchParams(window.location.search).get("returnTo"));
}

function subscribeToHydration(): () => void {
  return () => undefined;
}
