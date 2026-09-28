"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { BrandMark } from "@/components/brand-mark";
import { PmcsSessionBoundary, SessionBadge } from "@/components/pmcs-session";
import { formatPersianDateTime } from "@/lib/persian-date";
import { loadPortfolioReportingCenter, type PortfolioReportingCenterView } from
  "@/lib/portfolio-reporting-center";

const statusLabels: Record<string, string> = {
  Queued: "در صف", Processing: "در حال تهیه", Succeeded: "آماده", Failed: "ناموفق",
  Cancelled: "لغوشده", Available: "داده در دسترس", NoData: "بدون داده",
  InsufficientData: "داده ناکافی",
};

export function PortfolioReportingCenter() {
  return <PmcsSessionBoundary><PortfolioReportingContent /></PmcsSessionBoundary>;
}

function PortfolioReportingContent() {
  const [view, setView] = useState<PortfolioReportingCenterView | null>(null);
  const [failure, setFailure] = useState("");
  const [refresh, setRefresh] = useState(0);

  useEffect(() => {
    let active = true;
    void loadPortfolioReportingCenter("/api/pmcs").then((result) => {
      if (!active) return;
      setView(result);
      setFailure("");
    }).catch(() => {
      if (!active) return;
      setView(null);
      setFailure("دریافت گزارش‌های مجاز سبد کامل نشد؛ اتصال را بررسی و دوباره تلاش کنید.");
    });
    return () => { active = false; };
  }, [refresh]);

  return <main className="app-shell reporting-shell">
    <aside className="sidebar" aria-label="ناوبری اصلی">
      <BrandMark />
      <nav>
        <Link className="nav-item" href="/portfolio">مرکز فرمان سبد پروژه‌ها</Link>
        <span className="nav-item active" aria-current="page">گزارش‌های سبد</span>
        <Link className="nav-item" href="/">مرکز فرمان پروژه</Link>
      </nav>
      <SessionBadge />
    </aside>
    <section className="workspace reporting-workspace" aria-labelledby="portfolio-reporting-title">
      <header className="topbar">
        <div><p className="eyebrow">گزارش معتبر در محدودهٔ سازمان</p>
          <h1 id="portfolio-reporting-title">مرکز گزارش‌های سبد پروژه‌ها</h1>
          <p className="muted">فقط مجموعهٔ پروژه‌های مجاز و سنجش‌پذیر در گزارش سبد حضور دارند.</p>
        </div>
        <button className="secondary-button" type="button" onClick={() => setRefresh((value) => value + 1)}>
          تازه‌سازی
        </button>
      </header>
      {failure ? <section className="reporting-state" role="alert"><h2>دریافت گزارش‌ها کامل نشد</h2>
        <p>{failure}</p><button type="button" onClick={() => setRefresh((value) => value + 1)}>تلاش دوباره</button>
      </section>
        : !view ? <p className="reporting-state" role="status">در حال دریافت مرکز گزارش‌های سبد…</p>
        : view.kind === "unavailable" ? <section className="reporting-state" role="status">
          <h2>گزارش‌های سبد در دسترس نیستند</h2>
          <p>پس از فعال‌سازی مجاز، تعریف و سابقهٔ سبد در این بخش نمایش داده می‌شوند.</p>
        </section>
        : view.kind === "forbidden" ? <section className="reporting-state" role="alert">
          <h2>دسترسی به گزارش‌های سبد ندارید</h2>
          <p>مجوز سازمانی گزارش و دسترسی به سبد را با مدیر بررسی کنید.</p>
        </section>
        : <div className="reporting-sections">
          <section className="reporting-section" aria-labelledby="portfolio-report-definitions">
            <div className="reporting-section-heading"><h2 id="portfolio-report-definitions">گزارش‌های مجاز</h2>
              <span className="count-badge">{view.definitions.length.toLocaleString("fa-IR")}</span></div>
            {view.definitions.length ? <ul className="reporting-grid">{view.definitions.map((definition) =>
              <li className="reporting-card" key={definition.code}>
                <p className="eyebrow">قالب {definition.templateVersion}</p><h3>{definition.title}</h3>
                <p>{definition.description}</p>
                <small>قالب‌های خروجی: {definition.supportedFormats.join("، ")}</small>
              </li>)}</ul> : <p className="reporting-state">گزارش سبد برای این نقش در دسترس نیست.</p>}
          </section>
          <section className="reporting-section" aria-labelledby="portfolio-report-runs">
            <div className="reporting-section-heading"><h2 id="portfolio-report-runs">سابقهٔ درخواست‌ها</h2>
              <span className="count-badge">{view.runs.length.toLocaleString("fa-IR")}</span></div>
            {view.runs.length ? <ol className="reporting-runs">{view.runs.map((run) =>
              <li className="reporting-card" key={run.id}>
                <div className="reporting-run-heading"><strong>گزارش سبد پروژه‌ها</strong>
                  <span className="status-pill">{statusLabels[run.status] ?? "وضعیت نامشخص"}</span></div>
                <time dateTime={run.createdAt}>{formatPersianDateTime(run.createdAt)}</time>
                <p>{run.dataStatus ? statusLabels[run.dataStatus] ?? "وضعیت داده نامشخص" :
                  "وضعیت داده هنوز مشخص نیست"}</p>
                <small>{run.outputs.length ? `${run.outputs.length.toLocaleString("fa-IR")} خروجی ثبت‌شده` :
                  "خروجی ثبت نشده است"}</small>
              </li>)}</ol> : <p className="reporting-state">هنوز درخواستی برای سبد ثبت نشده است.</p>}
          </section>
        </div>}
    </section>
  </main>;
}
