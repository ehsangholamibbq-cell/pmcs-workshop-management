"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { BrandMark } from "@/components/brand-mark";
import { PmcsSessionBoundary, SessionBadge, usePmcsSession } from "@/components/pmcs-session";
import { PersianDateInput } from "@/components/persian-date-input";
import { listDailyReports, type DailyReportSummary } from "@/lib/daily-reports";
import { formatPersianDate, formatPersianDateTime } from "@/lib/persian-date";
import { scopedStorageKey } from "@/lib/field-database";
import {
  downloadProjectReportOutput, loadProjectReportingCenter, ReportOutputAccessError,
  type ProjectReportingCenterView, type ReportDefinitionView, type ReportOutputView, type ReportRunView,
} from "@/lib/reporting-center";
import {
  isValidProjectPeriodStart, ReportRequestAccessError, requestProjectReport, type ProjectReportRequest,
} from "@/lib/reporting-run-request";

const statusLabels: Record<string, string> = {
  Queued: "در صف", Processing: "در حال تهیه", Succeeded: "آماده", Failed: "ناموفق",
  Cancelled: "لغوشده", Pending: "در انتظار داده", Available: "داده در دسترس", NoData: "بدون داده",
  InsufficientData: "داده ناکافی", NotConfigured: "پیکربندی‌نشده",
};

function pendingRequest(projectId: string): ProjectReportRequest | null {
  try {
    const stored = localStorage.getItem(scopedStorageKey(`pmcs-report-request:${projectId}`));
    if (!stored) return null;
    const request = JSON.parse(stored) as ProjectReportRequest;
    return request.projectId === projectId && request.clientGeneratedId && request.definitionCode
      ? request : null;
  } catch { return null; }
}

export function ProjectReportingCenter({ projectId }: { readonly projectId: string }) {
  return <PmcsSessionBoundary><ReportingContent projectId={projectId} /></PmcsSessionBoundary>;
}

function ReportingContent({ projectId }: { readonly projectId: string }) {
  const session = usePmcsSession();
  const [view, setView] = useState<ProjectReportingCenterView | null>(null);
  const [failure, setFailure] = useState("");
  const [refresh, setRefresh] = useState(0);
  const [formats, setFormats] = useState<Record<string, "Pdf" | "Xlsx">>({});
  const [pendingRun, setPendingRun] = useState<ProjectReportRequest | null>(() => pendingRequest(projectId));
  const [busyCode, setBusyCode] = useState("");
  const [runNotice, setRunNotice] = useState("");
  const [downloadBusyId, setDownloadBusyId] = useState("");
  const [downloadNotice, setDownloadNotice] = useState("");
  const [dailyReports, setDailyReports] = useState<readonly DailyReportSummary[]>([]);
  const [dailyError, setDailyError] = useState("");
  const [dailyReportId, setDailyReportId] = useState("");
  const [includeRevisionChain, setIncludeRevisionChain] = useState(true);
  const [periodKind, setPeriodKind] = useState<"Weekly" | "Monthly">("Weekly");
  const [periodStartLocalDate, setPeriodStartLocalDate] = useState("");
  const hasDailyDefinition = view?.kind === "ready" &&
    view.definitions.some((item) => item.code === "daily-report-certified");
  const validPeriodStart = isValidProjectPeriodStart(periodKind, periodStartLocalDate);

  const remember = useCallback((request: ProjectReportRequest | null) => {
    try {
      const key = scopedStorageKey(`pmcs-report-request:${projectId}`);
      if (request) localStorage.setItem(key, JSON.stringify(request));
      else localStorage.removeItem(key);
    } catch { /* The in-memory request still retains its identity in this session. */ }
    setPendingRun(request);
  }, [projectId]);

  useEffect(() => {
    let active = true;
    void loadProjectReportingCenter("/api/pmcs", projectId).then((result) => {
      if (!active) return;
      if (result.kind !== "ready") remember(null);
      setView(result);
      setFailure("");
    }).catch(() => {
      if (active) {
        setView(null);
        setFailure("دریافت گزارش‌های مجاز کامل نشد؛ اتصال را بررسی و دوباره تلاش کنید.");
      }
    });
    return () => { active = false; };
  }, [projectId, refresh, remember]);

  useEffect(() => {
    if (!hasDailyDefinition) {
      return undefined;
    }
    let active = true;
    void listDailyReports("/api/pmcs", { tenantId: session.tenantId, userId: session.userId }, projectId)
      .then((reports) => {
        if (!active) return;
        if (!Array.isArray(reports) || reports.length > 500 ||
            reports.some((report) => report.projectId?.toLowerCase() !== projectId.toLowerCase())) {
          throw new Error("سابقهٔ گزارش روزانه با پروژه سازگار نیست.");
        }
        setDailyReports(reports.filter((report) => report.status === "Approved" &&
          !report.supersededByReportId));
        setDailyError("");
      }).catch(() => {
        if (active) { setDailyReports([]); setDailyError("گزارش‌های روزانهٔ مجاز دریافت نشدند."); }
      });
    return () => { active = false; };
  }, [hasDailyDefinition, projectId, session.tenantId, session.userId]);

  async function createRun(definition: ReportDefinitionView) {
    if (view?.kind !== "ready" || busyCode || pendingRun && pendingRun.definitionCode !== definition.code) return;
    const allowed = definition.supportedFormats.filter((format): format is "Pdf" | "Xlsx" =>
      format === "Pdf" || format === "Xlsx");
    const format = formats[definition.code] ?? allowed[0];
    if (!format) return;
    const parameters = definition.code === "daily-report-certified"
      ? { dailyReportId, includeRevisionChain }
      : definition.code === "project-periodic-certified"
        ? { periodKind, periodStartLocalDate }
        : undefined;
    const request = pendingRun ?? { projectId, clientGeneratedId: crypto.randomUUID(),
      definitionCode: definition.code, templateVersion: definition.templateVersion, format, parameters };
    remember(request);
    setBusyCode(definition.code);
    setRunNotice("");
    try {
      await requestProjectReport("/api/pmcs", request);
      remember(null);
      setRunNotice("درخواست گزارش پذیرفته شد؛ وضعیت آن در سابقه نمایش داده می‌شود.");
      setRefresh((value) => value + 1);
    } catch (error) {
      if (error instanceof ReportRequestAccessError) {
        setView(error.status === 403 ? { kind: "forbidden" } : { kind: "unavailable" });
        remember(null);
      } else {
        setRunNotice(error instanceof Error ? error.message : "درخواست گزارش کامل نشد؛ دوباره تلاش کنید.");
      }
    } finally {
      setBusyCode("");
    }
  }

  async function downloadOutput(run: ReportRunView, output: ReportOutputView) {
    if (downloadBusyId || view?.kind !== "ready") return;
    setDownloadBusyId(output.id);
    setDownloadNotice("");
    try {
      const blob = await downloadProjectReportOutput("/api/pmcs", projectId, run, output);
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement("a");
      anchor.href = url;
      anchor.download = output.fileName;
      document.body.appendChild(anchor);
      anchor.click();
      anchor.remove();
      window.setTimeout(() => URL.revokeObjectURL(url), 30_000);
      setDownloadNotice("خروجی تأیید و دریافت شد.");
    } catch (error) {
      if (error instanceof ReportOutputAccessError && error.status === 403) {
        remember(null);
        setView({ kind: "forbidden" });
      } else {
        setDownloadNotice(error instanceof Error ? error.message : "دریافت خروجی کامل نشد.");
      }
    } finally {
      setDownloadBusyId("");
    }
  }

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
          {runNotice && <p role="status" className="reporting-notice">{runNotice}</p>}
          {downloadNotice && <p role="status" className="reporting-notice">{downloadNotice}</p>}
          {pendingRun && <div className="reporting-notice reporting-pending" role="status">
            <span>یک درخواست نیمه‌تمام محفوظ است. پس از بررسی سابقه، همان درخواست را دوباره بفرستید.</span>
            <button className="secondary-button" type="button" onClick={() => remember(null)}>
              کنارگذاشتن تلاش نیمه‌تمام
            </button>
          </div>}
          <section className="reporting-section" aria-labelledby="report-definitions">
            <div className="reporting-section-heading"><h2 id="report-definitions">گزارش‌های مجاز</h2>
              <span className="count-badge">{view.definitions.length.toLocaleString("fa-IR")}</span></div>
            {view.definitions.length ? <ul className="reporting-grid">{view.definitions.map((definition) =>
              <li className="reporting-card" key={definition.code}>
                <p className="eyebrow">قالب {definition.templateVersion}</p><h3>{definition.title}</h3>
                <p>{definition.description}</p>
                <small>قالب‌های خروجی: {definition.supportedFormats.join("، ")}</small>
                <div className="reporting-request">
                    {definition.code === "daily-report-certified" && <>
                      <label htmlFor="report-daily-select">گزارش روزانهٔ تأییدشده</label>
                      <select id="report-daily-select" value={dailyReportId} disabled={Boolean(pendingRun) || Boolean(busyCode)}
                        onChange={(event) => setDailyReportId(event.target.value)}>
                        <option value="">انتخاب گزارش روزانه</option>
                        {dailyReports.map((report) => <option key={report.id} value={report.id}>
                          {formatPersianDate(report.reportDate)} · نسخه {report.versionNumber.toLocaleString("fa-IR")}
                        </option>)}
                      </select>
                      {dailyError && <small role="status">{dailyError}</small>}
                      <label className="reporting-checkbox"><input type="checkbox" checked={includeRevisionChain}
                        disabled={Boolean(pendingRun) || Boolean(busyCode)}
                        onChange={(event) => setIncludeRevisionChain(event.target.checked)} />زنجیرهٔ اصلاحات را نیز بیاور</label>
                    </>}
                    {definition.code === "project-periodic-certified" && <>
                      <label htmlFor="report-period-kind">دورهٔ گزارش</label>
                      <select id="report-period-kind" value={periodKind} disabled={Boolean(pendingRun) || Boolean(busyCode)}
                        onChange={(event) => setPeriodKind(event.target.value as "Weekly" | "Monthly")}>
                        <option value="Weekly">هفتگی</option><option value="Monthly">ماهانه</option>
                      </select>
                      <label htmlFor="report-period-start">شروع دوره</label>
                      <PersianDateInput id="report-period-start" ariaLabel="شروع دوره شمسی"
                        value={periodStartLocalDate} onChange={setPeriodStartLocalDate}
                        disabled={Boolean(pendingRun) || Boolean(busyCode)} required />
                      <small>هفته از شنبه و ماه از روز نخست ماه شمسی آغاز می‌شود.</small>
                    </>}
                    <label htmlFor={`report-format-${definition.code}`}>قالب درخواستی</label>
                    <select id={`report-format-${definition.code}`} value={formats[definition.code] ??
                      definition.supportedFormats.find((format) => format === "Pdf" || format === "Xlsx") ?? ""}
                      disabled={Boolean(pendingRun) || Boolean(busyCode)}
                      onChange={(event) => setFormats((current) => ({ ...current,
                        [definition.code]: event.target.value as "Pdf" | "Xlsx" }))}>
                      {definition.supportedFormats.filter((format) => format === "Pdf" || format === "Xlsx")
                        .map((format) => <option key={format} value={format}>{format}</option>)}
                    </select>
                    <button type="button" disabled={Boolean(busyCode) || Boolean(pendingRun &&
                      pendingRun.definitionCode !== definition.code) ||
                      (!pendingRun && definition.code === "daily-report-certified" && !dailyReportId) ||
                      (!pendingRun && definition.code === "project-periodic-certified" && !validPeriodStart) ||
                      !definition.supportedFormats.some((format) => format === "Pdf" || format === "Xlsx")}
                      onClick={() => void createRun(definition)}>
                      {busyCode === definition.code ? "در حال ثبت…" : pendingRun?.definitionCode === definition.code
                        ? "تلاش دوباره با همان درخواست" : "درخواست گزارش"}
                    </button>
                  </div>
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
                {run.status === "Succeeded" && run.outputs.length > 0 && <div className="reporting-request">
                  {run.outputs.map((output) => <button className="secondary-button" type="button"
                    key={output.id} disabled={Boolean(downloadBusyId)}
                    onClick={() => void downloadOutput(run, output)}>
                    {downloadBusyId === output.id ? "در حال دریافت…" :
                      `دریافت خروجی ${output.format === "Pdf" ? "PDF" : "Excel"}`}
                  </button>)}
                </div>}
              </li>)}</ol> : <p className="reporting-state">هنوز درخواستی برای این پروژه ثبت نشده است.</p>}
          </section>
        </div>}
    </section>
  </main>;
}
