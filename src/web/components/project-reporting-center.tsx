"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { BrandMark } from "@/components/brand-mark";
import { PmcsSessionBoundary, SessionBadge } from "@/components/pmcs-session";
import { formatPersianDateTime } from "@/lib/persian-date";
import { loadProjectReportingCenter, type ProjectReportingCenterView } from "@/lib/reporting-center";

const statusLabels: Record<string, string> = {
  Queued: "در صف", Processing: "در حال تهیه", Succeeded: "آماده", Failed: "ناموفق",
  Cancelled: "لغوشده", Pending: "در انتظار داده", Available: "داده در دسترس", NoData: "بدون داده",
  InsufficientData: "داده ناکافی", NotConfigured: "پیکربندی‌نشده",
};

export function ProjectReportingCenter({ projectId }: { readonly projectId: string }) {
  return <PmcsSessionBoundary><ReportingContent projectId={projectId} /></PmcsSessionBoundary>;
}

function ReportingContent({ projectId }: { readonly projectId: string }) {
  const [view, setView] = useState<ProjectReportingCenterView | null>(null);
  const [failure, setFailure] = useState("");
  const [refresh, setRefresh] = useState(0);

  useEffect(() => {
    let active = true;
    void loadProjectReportingCenter("/api/pmcs", projectId).then((result) => {
      if (!active) return;
      setView(result);
      setFailure("");
    }).catch(() => {
      if (active) {
        setView(null);
        setFailure("دریافت گزارش‌های مجاز کامل نشد؛ اتصال را بررسی و دوباره تلاش کنید.");
      }
    });
    return () => { active = false; };
  }, [projectId, refresh]);

  return <main className="app-shell reporting-shell">
    <aside className="sidebar" aria-label="ناوبری اصلی">
      <BrandMark />
      <nav>
        <Link className="nav-item" href={`/projects/${projectId}`}>مرکز فرمان پروژه</Link>
        <Link className="nav-item" href={`/projects/${projectId}/collaboration`}>گفت‌وگوی پروژه</Link>
        <span className="nav-item active" aria-current="page">مرکز گزارش‌ها</span>
        <Link className="nav-item" href="/portfolio">سبد پروژه‌ها</Link>
      </nav>
      <SessionBadge />
    </aside>
    <section className="workspace reporting-workspace" aria-labelledby="reporting-title">
      <header className="topbar">
        <div><p className="eyebrow">خروجی‌های معتبر در محدودهٔ همین پروژه</p>
          <h1 id="reporting-title">مرکز گزارش‌های پروژه</h1>
          <p className="muted">هر گزارش از داده و مجوز همان پروژه ساخته می‌شود؛ نبود داده به‌صورت مستقل نمایش داده می‌شود.</p>
        </div>
        <button className="secondary-button" type="button" onClick={() => setRefresh((value) => value + 1)}>
          تازه‌سازی
        </button>
      </header>
      {failure ? <section className="reporting-state" role="alert"><h2>دریافت گزارش‌ها کامل نشد</h2>
        <p>{failure}</p><button type="button" onClick={() => setRefresh((value) => value + 1)}>تلاش دوباره</button></section>
        : !view ? <p className="reporting-state" role="status">در حال دریافت مرکز گزارش‌ها…</p>
        : view.kind === "unavailable" ? <section className="reporting-state" role="status">
          <h2>گزارش‌گیری در این پروژه در دسترس نیست</h2>
          <p>پس از فعال‌سازی مجاز، تعریف‌ها و سابقهٔ همین پروژه در این بخش نمایش داده می‌شوند.</p>
        </section>
        : view.kind === "forbidden" ? <section className="reporting-state" role="alert">
          <h2>دسترسی به گزارش‌ها ندارید</h2><p>مجوز پروژه و منبع گزارش را با مدیر بررسی کنید.</p>
        </section>
        : <div className="reporting-sections">
          <section className="reporting-section" aria-labelledby="report-definitions">
            <div className="reporting-section-heading"><h2 id="report-definitions">گزارش‌های مجاز</h2>
              <span className="count-badge">{view.definitions.length.toLocaleString("fa-IR")}</span></div>
            {view.definitions.length ? <ul className="reporting-grid">{view.definitions.map((definition) =>
              <li className="reporting-card" key={definition.code}>
                <p className="eyebrow">قالب {definition.templateVersion}</p><h3>{definition.title}</h3>
                <p>{definition.description}</p>
                <small>قالب‌های خروجی: {definition.supportedFormats.join("، ")}</small>
              </li>)}</ul> : <p className="reporting-state">گزارش مجازی برای این نقش و پروژه در دسترس نیست.</p>}
          </section>
          <section className="reporting-section" aria-labelledby="report-runs">
            <div className="reporting-section-heading"><h2 id="report-runs">سابقهٔ درخواست‌ها</h2>
              <span className="count-badge">{view.runs.length.toLocaleString("fa-IR")}</span></div>
            {view.runs.length ? <ol className="reporting-runs">{view.runs.map((run) =>
              <li className="reporting-card" key={run.id}>
                <div className="reporting-run-heading"><strong>{view.definitions.find((item) => item.code === run.definitionCode)?.title ?? "گزارش پروژه"}</strong>
                  <span className="status-pill">{statusLabels[run.status] ?? "وضعیت نامشخص"}</span></div>
                <time dateTime={run.createdAt}>{formatPersianDateTime(run.createdAt)}</time>
                <p>{run.dataStatus ? statusLabels[run.dataStatus] ?? "وضعیت داده نامشخص" : "وضعیت داده هنوز مشخص نیست"}</p>
                <small>{run.outputs.length ? `${run.outputs.length.toLocaleString("fa-IR")} خروجی ثبت‌شده` : "خروجی ثبت نشده است"}</small>
              </li>)}</ol> : <p className="reporting-state">هنوز درخواستی برای این پروژه ثبت نشده است.</p>}
          </section>
        </div>}
    </section>
  </main>;
}
