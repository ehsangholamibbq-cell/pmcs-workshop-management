"use client";

import { useEffect, useState, useSyncExternalStore } from "react";
import { useRouter } from "next/navigation";
import { authClient } from "@/lib/auth-client";
import { safeApplicationReturnPath } from "@/lib/return-path";

export function LoginPanel({ initialError = false }: { readonly initialError?: boolean }) {
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
    <main className="login-shell">
      <section className="login-brand" aria-label="معرفی سامانه">
        <div className="login-monogram">پ</div>
        <p className="eyebrow">سامانه کنترل مدیریت پروژه</p>
        <h1>تصمیم مدیریتی، بر پایه واقعیت قابل ردیابی</h1>
        <p>
          وضعیت عملیات، مالی و قراردادها مستقل می‌ماند و هر تحلیل به داده رسمی پروژه ارجاع دارد.
        </p>
        <ul>
          <li>ورود دومرحله‌ای و نشست امن</li>
          <li>دسترسی فقط در محدوده سازمان و پروژه مجاز</li>
          <li>ثبت آفلاین با صف اختصاصی هر کاربر</li>
        </ul>
      </section>
      <section className="login-card" aria-labelledby="login-title">
        <div>
          <p className="eyebrow">درگاه امن</p>
          <h2 id="login-title">ورود به حساب کاربری</h2>
          <p>برای ادامه به سرویس هویت سازمان هدایت می‌شوید.</p>
        </div>
        <button type="button" disabled={!isHydrated || isPending || isStarting || Boolean(session)} onClick={() => void startSignIn()}>
          {!isHydrated || isPending || isStarting || session ? "در حال بررسی…" : "ورود امن"}
        </button>
        {message && <p className="login-error" role="alert">{message}</p>}
        <small>رمز عبور و کد دومرحله‌ای در این برنامه دریافت یا ذخیره نمی‌شود.</small>
      </section>
    </main>
  );
}

function safeReturnTo(): string {
  if (typeof window === "undefined") return "/portfolio";
  return safeApplicationReturnPath(new URLSearchParams(window.location.search).get("returnTo"));
}

function subscribeToHydration(): () => void {
  return () => undefined;
}
