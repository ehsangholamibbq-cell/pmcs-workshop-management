"use client";

import { createContext, type ReactNode, useContext, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { authClient } from "@/lib/auth-client";
import { clearLocalIdentityScope, setLocalIdentityScope } from "@/lib/field-database";

export interface PmcsSession {
  readonly userId: string;
  readonly tenantId: string;
  readonly deviceId: string | null;
  readonly displayName: string;
  readonly email: string;
  readonly tenantRole: "Member" | "PortfolioViewer" | "TenantAdministrator";
  readonly tenantName: string;
  readonly authentication: "development-adapter" | "oidc-access-token";
}

const SessionContext = createContext<PmcsSession | null>(null);

export function PmcsSessionBoundary({ children }: { readonly children: ReactNode }) {
  const router = useRouter();
  const [session, setSession] = useState<PmcsSession | null>(null);
  const [message, setMessage] = useState("در حال بررسی نشست امن…");
  const [retry, setRetry] = useState(0);

  useEffect(() => {
    let active = true;
    void fetch("/api/pmcs/api/v1/session", { cache: "no-store" })
      .then(async (response) => {
        if (response.status === 401) {
          await clearLocalIdentityScope(false);
          router.replace("/login");
          return null;
        }
        if (response.status === 403) {
          await clearLocalIdentityScope(false);
          throw new Error("این حساب در سامانه فعال نیست؛ با مدیر سامانه تماس بگیرید.");
        }
        if (!response.ok) {
          throw new Error("بررسی نشست انجام نشد؛ اتصال سرویس را بررسی کنید.");
        }
        return response.json() as Promise<PmcsSession>;
      })
      .then((loaded) => {
        if (!active || !loaded) return;
        setLocalIdentityScope(loaded.tenantId, loaded.userId);
        setSession(loaded);
      })
      .catch((error: unknown) => {
        if (!active) return;
        setMessage(error instanceof Error ? error.message : "بررسی نشست ناموفق بود.");
      });
    return () => {
      active = false;
    };
  }, [retry, router]);

  if (!session) {
    return (
      <main className="auth-state" aria-live="polite">
        <div className="auth-state-card">
          <span className="auth-state-mark">پ</span>
          <h1>ورود امن به سامانه</h1>
          <p>{message}</p>
          {message !== "در حال بررسی نشست امن…" && (
            <button type="button" onClick={() => {
              setMessage("در حال بررسی نشست امن…");
              setRetry((value) => value + 1);
            }}>
              تلاش دوباره
            </button>
          )}
        </div>
      </main>
    );
  }

  return <SessionContext.Provider value={session}>{children}</SessionContext.Provider>;
}

export function usePmcsSession(): PmcsSession {
  const session = useContext(SessionContext);
  if (!session) {
    throw new Error("نشست سامانه در این بخش در دسترس نیست.");
  }
  return session;
}

export function SessionBadge() {
  const session = usePmcsSession();
  const [message, setMessage] = useState("");
  const [isSigningOut, setIsSigningOut] = useState(false);

  async function signOut(): Promise<void> {
    setIsSigningOut(true);
    setMessage("");
    try {
      await clearLocalIdentityScope(true);
      await authClient.signOut({ callbackURL: "/login" });
    } catch {
      setMessage("خروج امن انجام نشد؛ زبانه‌های دیگر سامانه را ببندید و دوباره تلاش کنید.");
      setIsSigningOut(false);
    }
  }

  return (
    <div className="session-badge">
      <div>
        <strong>{session.displayName}</strong>
        <span>{session.tenantName}</span>
      </div>
      <button type="button" disabled={isSigningOut} onClick={() => void signOut()}>
        {isSigningOut ? "در حال خروج…" : "خروج امن"}
      </button>
      {message && <small role="alert">{message}</small>}
    </div>
  );
}
