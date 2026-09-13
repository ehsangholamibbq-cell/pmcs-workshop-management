"use client";

import { useCallback, useEffect, useMemo, useState, useSyncExternalStore } from "react";
import Link from "next/link";
import {
  getCommandCenter,
  recalculateProjectState,
  type CommandCenterModel,
  type ProjectAttentionPriority,
  type ProjectOperationalStatus,
} from "@/lib/command-center";
import { capabilityLabel, initialCapabilities, toCapabilityView } from "@/lib/project-state";
import { EvidenceCapture } from "@/components/evidence-capture";
import { AttentionTriageControls } from "@/components/attention-triage-controls";
import { MyWorkCenter } from "@/components/my-work-center";
import { DailyReportHistory } from "@/components/daily-report-history";
import { RealityCaptureForm } from "@/components/reality-capture-form";
import { SyncIssuesPanel } from "@/components/sync-issues-panel";
import { TodayReportWorkflow } from "@/components/today-report-workflow";
import { FinanceControl } from "@/components/finance-control";
import { ProjectCalendarSettings } from "@/components/project-calendar-settings";
import { ProjectLocationSettings } from "@/components/project-location-settings";
import { CommercialControl } from "@/components/commercial-control";
import { AdvisoryInsights } from "@/components/advisory-insights";
import { PlanningProgressPanel } from "@/components/planning-progress-panel";
import { TechnicalOfficePanel } from "@/components/technical-office-panel";
import { SupplyRealityPanel } from "@/components/supply-reality-panel";
import { QualitySafetyPanel } from "@/components/quality-safety-panel";
import { GovernancePanel } from "@/components/governance-panel";
import type { MeasurementItemModel } from "@/lib/planning";
import { formatPersianDate, todayIsoInProjectTimeZone } from "@/lib/persian-date";
import {
  countPendingOperations,
  recoverInterruptedOperations,
  syncPendingOperations,
} from "@/lib/operation-store";
import {
  countPendingAttachments,
  recoverInterruptedAttachments,
  syncPendingAttachments,
} from "@/lib/attachment-store";
import { formatAmountFa, toUserMessage } from "@/lib/localization";
import { scopedStorageKey } from "@/lib/field-database";
import { listProjectLocations, type ProjectLocationModel } from "@/lib/projects";
import { PmcsSessionBoundary, SessionBadge, usePmcsSession } from "@/components/pmcs-session";

const apiBaseUrl = "/api/pmcs";

interface FoundationDashboardProps {
  readonly projectId: string;
}

export function FoundationDashboard({ projectId }: FoundationDashboardProps) {
  return (
    <PmcsSessionBoundary>
      <FoundationDashboardContent projectId={projectId} />
    </PmcsSessionBoundary>
  );
}

function FoundationDashboardContent({ projectId }: Required<FoundationDashboardProps>) {
  const session = usePmcsSession();
  const tenantId = session.tenantId;
  const userId = session.userId;
  const [pendingCount, setPendingCount] = useState(0);
  const [attachmentCount, setAttachmentCount] = useState(0);
  const [lastFactId, setLastFactId] = useState<string | null>(null);
  const [isSyncing, setIsSyncing] = useState(false);
  const [storageMessage, setStorageMessage] = useState("صف محلی آماده است");
  const [refreshToken, setRefreshToken] = useState(0);
  const [commandCenter, setCommandCenter] = useState<CommandCenterModel | null>(null);
  const [commandMessage, setCommandMessage] = useState("در حال دریافت آخرین تصویر رسمی وضعیت…");
  const [isCalculating, setIsCalculating] = useState(false);
  const [measurementItems, setMeasurementItems] = useState<readonly MeasurementItemModel[]>([]);
  const [projectLocations, setProjectLocations] = useState<readonly ProjectLocationModel[]>([]);
  const isOnline = useSyncExternalStore(subscribeToOnlineState, readOnlineState, () => true);
  const commandCenterCacheKey = useMemo(
    () => scopedStorageKey(`pmcs-command-center:${projectId}`),
    [projectId],
  );
  const locationCacheKey = useMemo(
    () => scopedStorageKey(`pmcs-project-locations:${projectId}`),
    [projectId],
  );
  const today = useMemo(
    () => formatPersianDate(todayIsoInProjectTimeZone(), "full"),
    [],
  );

  const refreshPendingCount = useCallback(async () => {
    const [operations, attachments] = await Promise.all([
      countPendingOperations(),
      countPendingAttachments(),
    ]);
    setPendingCount(operations);
    setAttachmentCount(attachments);
  }, []);

  const synchronize = useCallback(async (automatic = false) => {
    if (typeof navigator !== "undefined" && !navigator.onLine) {
      setStorageMessage("اینترنت در دسترس نیست؛ عملیات روی دستگاه باقی ماند");
      return;
    }

    setIsSyncing(true);
    if (!automatic) {
      setStorageMessage("در حال همگام‌سازی و اعتبارسنجی سرور…");
    }

    try {
      const result = await syncPendingOperations(apiBaseUrl, projectId);
      const evidenceResult = await syncPendingAttachments(apiBaseUrl);
      await refreshPendingCount();
      if (result.sent === 0 && evidenceResult.sent === 0) {
        setStorageMessage("عملیات جدیدی برای همگام‌سازی وجود ندارد");
      } else if (result.conflicts > 0 || result.rejected > 0 || evidenceResult.rejected > 0 || evidenceResult.deferred > 0) {
        setStorageMessage(
          `${result.applied.toLocaleString("fa-IR")} عملیات و ${evidenceResult.uploaded.toLocaleString("fa-IR")} مدرک پذیرفته شد؛ موارد باقیمانده نیازمند تلاش مجدد یا بررسی است`,
        );
      } else {
        setStorageMessage(`${result.applied.toLocaleString("fa-IR")} عملیات و ${evidenceResult.uploaded.toLocaleString("fa-IR")} مدرک توسط سرور پذیرفته شد`);
      }
    } catch {
      await refreshPendingCount().catch(() => undefined);
      setStorageMessage("ارتباط با سرور برقرار نشد؛ داده محلی محفوظ است");
    } finally {
      setIsSyncing(false);
      setRefreshToken((current) => current + 1);
    }
  }, [projectId, refreshPendingCount]);

  useEffect(() => {
    void Promise.all([recoverInterruptedOperations(), recoverInterruptedAttachments()])
      .then(refreshPendingCount)
      .then(() => {
        if (navigator.onLine) {
          return synchronize(true);
        }

        return undefined;
      })
      .catch(() => setStorageMessage("فضای محلی هنوز در این مرورگر آماده نشده است"));

    const handleOnline = () => {
      void synchronize(true);
    };
    window.addEventListener("online", handleOnline);
    return () => {
      window.removeEventListener("online", handleOnline);
    };
  }, [refreshPendingCount, synchronize]);

  const handleQueued = useCallback(async (factId: string) => {
    setLastFactId(factId);
    await refreshPendingCount();
    setStorageMessage("روی این دستگاه ذخیره شد؛ پس از پذیرش سرور رسمی می‌شود");
  }, [refreshPendingCount]);

  const handleAttachmentQueued = useCallback(async () => {
    await refreshPendingCount();
    setStorageMessage("مدرک روی دستگاه ذخیره شد؛ بارگذاری آن مستقل و قابل تلاش مجدد است");
  }, [refreshPendingCount]);

  const loadCommandCenter = useCallback(async () => {
    const cached = readCachedCommandCenter(commandCenterCacheKey);
    if (!isOnline) {
      if (cached) {
        setCommandCenter(cached);
        setCommandMessage("نسخه ذخیره‌شده روی دستگاه نمایش داده می‌شود؛ برای به‌روزرسانی به سرور متصل شوید.");
      } else {
        setCommandMessage("بدون اتصال، تصویر رسمی ذخیره‌شده‌ای روی این دستگاه وجود ندارد.");
      }
      return;
    }

    try {
      const model = await getCommandCenter(
        apiBaseUrl,
        { tenantId, userId },
        projectId,
      );
      setCommandCenter(model);
      localStorage.setItem(commandCenterCacheKey, JSON.stringify(model));
      setCommandMessage(model.isOutdated
        ? "داده تأییدشده جدیدتر از تصویر رسمی وضعیت است؛ محاسبه مجدد لازم است."
        : "تصویر رسمی و قابل ردیابی وضعیت از سرور دریافت شد.");
    } catch (error) {
      if (cached) {
        setCommandCenter(cached);
        setCommandMessage("سرور در دسترس نبود؛ آخرین تصویر رسمی ذخیره‌شده نمایش داده می‌شود.");
      } else {
        setCommandMessage(toUserMessage(error, "وضعیت پروژه هنوز از سرور دریافت نشده است."));
      }
    }
  }, [commandCenterCacheKey, isOnline, projectId, tenantId, userId]);

  useEffect(() => {
    const timeoutId = window.setTimeout(() => {
      void loadCommandCenter();
    }, 0);
    return () => window.clearTimeout(timeoutId);
  }, [loadCommandCenter, refreshToken]);

  const loadProjectLocations = useCallback(async () => {
    const cached = readCachedProjectLocations(locationCacheKey);
    if (!isOnline) {
      setProjectLocations(cached);
      return;
    }

    try {
      const locations = await listProjectLocations(
        apiBaseUrl,
        { tenantId, userId },
        projectId,
      );
      setProjectLocations(locations);
      localStorage.setItem(locationCacheKey, JSON.stringify(locations));
    } catch {
      setProjectLocations(cached);
    }
  }, [isOnline, locationCacheKey, projectId, tenantId, userId]);

  useEffect(() => {
    const timeoutId = window.setTimeout(() => {
      void loadProjectLocations();
    }, 0);
    return () => window.clearTimeout(timeoutId);
  }, [loadProjectLocations, refreshToken]);

  async function recalculate() {
    if (!isOnline) {
      setCommandMessage("محاسبه رسمی فقط هنگام اتصال به سرور انجام می‌شود.");
      return;
    }

    setIsCalculating(true);
    setCommandMessage("موتور قطعی در حال محاسبه از واقعیت‌های تأییدشده است…");
    try {
      await recalculateProjectState(
        apiBaseUrl,
        { tenantId, userId },
        projectId,
      );
      await loadCommandCenter();
    } catch (error) {
      setCommandMessage(toUserMessage(error, "محاسبه وضعیت پروژه ناموفق بود."));
    } finally {
      setIsCalculating(false);
    }
  }

  const snapshot = commandCenter?.snapshot ?? null;
  const capabilityViews = commandCenter
    ? commandCenter.capabilities.map(toCapabilityView)
    : initialCapabilities;
  const attentionItems = snapshot?.attentionItems ?? [];
  const untriagedAttentionItems = attentionItems.filter((item) => item.disposition === "NeedsTriage");

  return (
    <main className="app-shell">
      <aside className="sidebar" aria-label="ناوبری اصلی">
        <div className="brand-mark" aria-label="سامانه کنترل مدیریت پروژه"><span>پ</span></div>
        <nav>
          <Link className="nav-item" href="/">پروژه‌ها</Link>
          {(session.tenantRole === "TenantAdministrator" || session.tenantRole === "PortfolioViewer") && (
            <a className="nav-item" href="/portfolio">نمای سبد مدیریتی</a>
          )}
          <a className="nav-item active" href="#pulse">مرکز فرمان</a>
          <a className="nav-item" href="#today">امروز کارگاه</a>
          <a className="nav-item" href="#progress">برنامه‌ریزی و پیشرفت</a>
          <a className="nav-item" href="#technical-office">دفتر فنی و اسناد</a>
          <a className="nav-item" href="#actions">کارهای من و اعلان‌ها</a>
          <a className="nav-item" href="#finance">مالی</a>
          <a className="nav-item" href="#commercial">قرارداد و خرید</a>
          <a className="nav-item" href="#supply">تدارکات و موجودی</a>
          <a className="nav-item" href="#quality-safety">کیفیت و ایمنی</a>
          <a className="nav-item" href="#governance">ریسک و تصمیم</a>
          <a className="nav-item" href="#advisory">تحلیل مشورتی</a>
          <a className="nav-item" href="#setup">تنظیمات پروژه</a>
          {session.tenantRole === "TenantAdministrator" && <a className="nav-item" href="/admin/users">کاربران و دسترسی‌ها</a>}
        </nav>
        <div className="sidebar-meta">
          <span className={isOnline ? "online-dot" : "offline-dot"} />
          {isOnline ? "آنلاین" : "آفلاین"}
        </div>
        <SessionBadge />
      </aside>

      <section className="workspace">
        <header className="topbar">
          <div>
            <p className="eyebrow">
              {commandCenter
                ? `${commandCenter.projectName} · ${commandCenter.projectCode}`
                : "در حال دریافت مشخصات پروژه…"}
            </p>
            <h1>مرکز فرمان پروژه</h1>
          </div>
          <div className="topbar-meta">
            <span>{today}</span>
            <button
              className="sync-pill"
              type="button"
              disabled={isSyncing}
              onClick={() => void synchronize()}
            >
              {isSyncing
                ? "در حال همگام‌سازی…"
                : `${pendingCount.toLocaleString("fa-IR")} عملیات · ${attachmentCount.toLocaleString("fa-IR")} مدرک`}
            </button>
          </div>
        </header>

        <section className="hero-grid" id="pulse">
          <article className={`pulse-card state-${(snapshot?.operationalStatus ?? "NoData").toLowerCase()}`}>
            <div className="card-heading">
              <div>
                <p className="eyebrow">وضعیت پروژه</p>
                <h2>{operationalStatusLabel(snapshot?.operationalStatus)}</h2>
              </div>
              <span className="state-orb">{operationalStatusSymbol(snapshot?.operationalStatus)}</span>
            </div>
            <p className="muted">{operationalStatusDescription(snapshot, commandMessage)}</p>
            <div className="state-controls">
              <span className="scope-badge">ارزیابی عملیاتی · محدود</span>
              {commandCenter?.isOutdated && <span className="stale-badge">تصویر وضعیت قدیمی است</span>}
              {commandCenter?.canRecalculate && (
                <button
                  className="secondary-button"
                  type="button"
                  disabled={!isOnline || isCalculating}
                  onClick={() => void recalculate()}
                >
                  {isCalculating ? "در حال محاسبه…" : "محاسبه وضعیت رسمی"}
                </button>
              )}
            </div>
            <div className="metric-row">
              <div>
                <strong>{coverageLabel(snapshot)}</strong>
                <span>{coverageBasisLabel(snapshot)}</span>
              </div>
              <div>
                <strong>{snapshot ? snapshot.approvedReportDays.toLocaleString("fa-IR") : "—"}</strong>
                <span>روز گزارش تأییدشده</span>
              </div>
              <div>
                <strong>{snapshot ? untriagedAttentionItems.length.toLocaleString("fa-IR") : "—"}</strong>
                <span>مورد نیازمند بررسی</span>
              </div>
            </div>
            <p className="calculation-note" aria-live="polite">{commandMessage}</p>
          </article>

          <article className="capture-card" id="today">
            <div>
              <p className="eyebrow">ثبت واقعیت پروژه</p>
              <h2>واقعیت امروز را ثبت کن</h2>
              <p className="muted">تحلیل لازم نیست؛ فقط چیزی را که واقعاً دیده‌ای بنویس.</p>
            </div>
            <RealityCaptureForm
              tenantId={tenantId}
              userId={userId}
              projectId={projectId}
              statusMessage={storageMessage}
              onStatus={setStorageMessage}
              onQueued={handleQueued}
              measurementItems={measurementItems}
              locations={projectLocations}
            />
            <EvidenceCapture
              tenantId={tenantId}
              userId={userId}
              projectId={projectId}
              lastFactId={lastFactId}
              onQueued={handleAttachmentQueued}
            />
            <TodayReportWorkflow
              apiBaseUrl={apiBaseUrl}
              tenantId={tenantId}
              userId={userId}
              projectId={projectId}
              pendingCount={pendingCount + attachmentCount}
              isOnline={isOnline}
              refreshToken={refreshToken}
              onChanged={() => setRefreshToken((current) => current + 1)}
            />
          </article>
        </section>

        <PlanningProgressPanel
          apiBaseUrl={apiBaseUrl}
          tenantId={tenantId}
          userId={userId}
          projectId={projectId}
          isOnline={isOnline}
          refreshToken={refreshToken}
          onItemsChanged={setMeasurementItems}
          onChanged={() => setRefreshToken((current) => current + 1)}
        />

        <TechnicalOfficePanel
          apiBaseUrl={apiBaseUrl}
          tenantId={tenantId}
          userId={userId}
          projectId={projectId}
          isOnline={isOnline}
          refreshToken={refreshToken}
          onChanged={() => setRefreshToken((current) => current + 1)}
        />

        <QualitySafetyPanel
          apiBaseUrl={apiBaseUrl}
          tenantId={tenantId}
          userId={userId}
          projectId={projectId}
          isOnline={isOnline}
          refreshToken={refreshToken}
          onChanged={() => {
            setRefreshToken((current) => current + 1);
            void refreshPendingCount();
          }}
        />

        <GovernancePanel
          apiBaseUrl={apiBaseUrl}
          tenantId={tenantId}
          userId={userId}
          projectId={projectId}
          isOnline={isOnline}
          refreshToken={refreshToken}
          onChanged={() => setRefreshToken((current) => current + 1)}
        />

        {commandCenter?.canReadFinance && (
          <section className="financial-summary" aria-label="خلاصه وضعیت مالی مستقل">
            <div>
              <p className="eyebrow">وضعیت مالی مستقل</p>
              <h2>{financialStateTitle(commandCenter.financialState)}</h2>
              <p className="muted">این بخش در رنگ وضعیت عملیاتی بالا ادغام نمی‌شود.</p>
            </div>
            <div className="financial-summary-metrics">
              <span>
                <strong>{formatFinancialAmount(commandCenter.financialState?.totalReceipts, commandCenter.financialState?.currencyCode)}</strong>
                دریافت قطعی
              </span>
              <span>
                <strong>{formatFinancialAmount(commandCenter.financialState?.recognizedSpend, commandCenter.financialState?.currencyCode)}</strong>
                هزینه شناسایی‌شده
              </span>
              <span>
                <strong>{budgetComparisonLabel(commandCenter.financialState?.budgetComparisonState)}</strong>
                مقایسه بودجه
              </span>
            </div>
          </section>
        )}

        {commandCenter?.canReadCommercial && (
          <section className="financial-summary commercial-summary" aria-label="خلاصه مستقل قرارداد و تدارکات">
            <div>
              <p className="eyebrow">وضعیت مستقل قرارداد و خرید</p>
              <h2>{commercialStateTitle(commandCenter.commercialState)}</h2>
              <p className="muted">این بخش در رنگ وضعیت عملیاتی یا مالی ادغام نمی‌شود.</p>
            </div>
            <div className="financial-summary-metrics">
              <span>
                <strong>{commandCenter.commercialState?.activeContractCount.toLocaleString("fa-IR") ?? "—"}</strong>
                قرارداد فعال
              </span>
              <span>
                <strong>{formatCommercialCeiling(commandCenter.commercialState)}</strong>
                سقف مصوبِ معلوم
              </span>
              <span>
                <strong>{commandCenter.commercialState?.openCommitmentCount.toLocaleString("fa-IR") ?? "—"}</strong>
                تعهد خرید باز
              </span>
            </div>
          </section>
        )}

        <AdvisoryInsights
          apiBaseUrl={apiBaseUrl}
          tenantId={tenantId}
          userId={userId}
          projectId={projectId}
          isOnline={isOnline}
          refreshToken={refreshToken}
        />

        <section className="operational-grid" id="actions">
          <MyWorkCenter
            apiBaseUrl={apiBaseUrl}
            tenantId={tenantId}
            userId={userId}
            projectId={projectId}
            isOnline={isOnline}
            refreshToken={refreshToken}
            onChanged={() => setRefreshToken((current) => current + 1)}
          />
          <DailyReportHistory
            apiBaseUrl={apiBaseUrl}
            tenantId={tenantId}
            userId={userId}
            projectId={projectId}
            isOnline={isOnline}
            refreshToken={refreshToken}
            locations={projectLocations}
            measurementItems={measurementItems}
            onChanged={() => setRefreshToken((current) => current + 1)}
          />
          <SyncIssuesPanel apiBaseUrl={apiBaseUrl} projectId={projectId} refreshToken={refreshToken} />
          {commandCenter?.canReadCommercial && (
            <>
              <CommercialControl
                apiBaseUrl={apiBaseUrl}
                tenantId={tenantId}
                userId={userId}
                projectId={projectId}
                isOnline={isOnline}
                refreshToken={refreshToken}
                onChanged={() => setRefreshToken((current) => current + 1)}
              />
              <SupplyRealityPanel
                apiBaseUrl={apiBaseUrl}
                tenantId={tenantId}
                userId={userId}
                projectId={projectId}
                isOnline={isOnline}
                refreshToken={refreshToken}
                onChanged={() => setRefreshToken((current) => current + 1)}
              />
            </>
          )}
          <FinanceControl
            apiBaseUrl={apiBaseUrl}
            tenantId={tenantId}
            userId={userId}
            projectId={projectId}
            isOnline={isOnline}
            refreshToken={refreshToken}
            onChanged={() => setRefreshToken((current) => current + 1)}
          />
        </section>

        <section className="section-block" id="setup">
          <div className="section-title">
            <div>
              <p className="eyebrow">وضعیت قابلیت‌ها</p>
              <h2>پیکربندی پروژه</h2>
            </div>
            <span className="section-note">عدم پیکربندی ≠ وضعیت نامناسب</span>
          </div>
          <ProjectCalendarSettings
            apiBaseUrl={apiBaseUrl}
            tenantId={tenantId}
            userId={userId}
            projectId={projectId}
            isOnline={isOnline}
            refreshToken={refreshToken}
            onChanged={() => setRefreshToken((current) => current + 1)}
          />
          <ProjectLocationSettings
            apiBaseUrl={apiBaseUrl}
            tenantId={tenantId}
            userId={userId}
            projectId={projectId}
            isOnline={isOnline}
            locations={projectLocations}
            onChanged={() => setRefreshToken((current) => current + 1)}
          />
          <div className="capability-grid">
            {capabilityViews.map((capability) => (
              <article className={`capability-card ${capability.status}`} key={capability.key}>
                <div className="capability-title">
                  <h3>{capability.label}</h3>
                  <span>{capabilityLabel(capability.status)}</span>
                </div>
                <p>{capability.detail}</p>
              </article>
            ))}
          </div>
        </section>

        <section className="section-block split" id="attention">
          <article>
            <p className="eyebrow">نیازمند رسیدگی</p>
            <h2>{attentionItems.length > 0 ? "مشاهدات تأییدشده و تعیین تکلیف مدیریتی" : "مورد رسمی نیازمند بررسی ثبت نشده است"}</h2>
            {attentionItems.length > 0 ? (
              <div className="command-attention-list">
                {attentionItems.slice(0, 6).map((item) => (
                  <div className="command-attention-item" key={item.sourceFactId}>
                    <div>
                      <span className={`priority-badge priority-${item.priority.toLowerCase()}`}>
                        {priorityLabel(item.priority)}
                      </span>
                      <strong>{item.kind === "Stoppage" ? "توقف" : "مشکل"} · {item.description}</strong>
                      <small>
                        {item.locationName ?? "محل ثبت نشده"} · سن مشاهده {item.ageDays.toLocaleString("fa-IR")} روز
                      </small>
                    </div>
                    {item.referenceCode && <code>{item.referenceCode}</code>}
                    {(commandCenter?.canTriage || item.disposition !== "NeedsTriage") && (
                      <AttentionTriageControls
                        apiBaseUrl={apiBaseUrl}
                        tenantId={tenantId}
                        userId={userId}
                        projectId={projectId}
                        item={item}
                        isOnline={isOnline}
                        onChanged={() => setRefreshToken((current) => current + 1)}
                      />
                    )}
                  </div>
                ))}
              </div>
            ) : (
              <p className="muted">
                نبود مورد در این بخش فقط درباره پنجره ۳۰روزه واقعیت‌های تأییدشده است و به‌معنای سلامت کامل مالی، زمانی یا ایمنی، بهداشت و محیط‌زیست (HSE) نیست.
              </p>
            )}
            {commandCenter && commandCenter.trend.length > 0 && (
              <div className="trend-strip" aria-label="روند تصاویر اخیر وضعیت">
                {commandCenter.trend.slice().reverse().map((point) => (
                  <span
                    className={`trend-point trend-${point.operationalStatus.toLowerCase()}`}
                    key={point.snapshotId}
                    title={`تاریخ ${formatStateDate(point.asOfDate)} · پوشش ${point.coveragePercent.toLocaleString("fa-IR")}٪`}
                  />
                ))}
              </div>
            )}
          </article>
          <article className="architecture-note">
            <span>اصل معماری</span>
            <strong>واقعیت ← شاخص ← وضعیت ← تحلیل مدیریتی</strong>
            <p>هوش مصنوعی هیچ واقعیت یا وضعیت رسمی را تغییر نمی‌دهد.</p>
          </article>
        </section>
      </section>
    </main>
  );
}

function subscribeToOnlineState(callback: () => void): () => void {
  window.addEventListener("online", callback);
  window.addEventListener("offline", callback);
  return () => {
    window.removeEventListener("online", callback);
    window.removeEventListener("offline", callback);
  };
}

function readOnlineState(): boolean {
  return navigator.onLine;
}

function readCachedCommandCenter(commandCenterCacheKey: string): CommandCenterModel | null {
  try {
    const value = localStorage.getItem(commandCenterCacheKey);
    return value ? JSON.parse(value) as CommandCenterModel : null;
  } catch {
    return null;
  }
}

function readCachedProjectLocations(locationCacheKey: string): readonly ProjectLocationModel[] {
  try {
    const value = localStorage.getItem(locationCacheKey);
    if (!value) return [];
    const parsed: unknown = JSON.parse(value);
    if (!Array.isArray(parsed)) return [];
    return parsed.filter((item): item is ProjectLocationModel =>
      typeof item === "object" && item !== null &&
      typeof (item as { id?: unknown }).id === "string" &&
      typeof (item as { name?: unknown }).name === "string" &&
      ((item as { status?: unknown }).status === "Active" || (item as { status?: unknown }).status === "Retired"));
  } catch {
    return [];
  }
}

function operationalStatusLabel(status?: ProjectOperationalStatus): string {
  return ({
    NoData: "هنوز داده تأییدشده وجود ندارد",
    InsufficientData: "داده برای ارزیابی رسمی کافی نیست",
    Stable: "عملیات در محدوده پایدار است",
    Watch: "عملیات نیازمند پایش است",
    AtRisk: "عملیات در معرض ریسک است",
    Critical: "وضعیت عملیاتی بحرانی است",
  } as Record<ProjectOperationalStatus, string>)[status ?? "NoData"];
}

function operationalStatusSymbol(status?: ProjectOperationalStatus): string {
  return ({
    NoData: "—",
    InsufficientData: "…",
    Stable: "✓",
    Watch: "!",
    AtRisk: "↑",
    Critical: "!!",
  } as Record<ProjectOperationalStatus, string>)[status ?? "NoData"];
}

function operationalStatusDescription(
  snapshot: CommandCenterModel["snapshot"],
  fallback: string,
): string {
  if (!snapshot) {
    return "هیچ تصویر رسمی وضعیتی ساخته نشده است؛ تا محاسبه موتور، سیستم ارزیابی تولید نمی‌کند.";
  }
  if (snapshot.operationalStatus === "NoData") {
    return "هیچ گزارش تأییدشده‌ای در دسترس نیست؛ پیش‌نویس و گزارش ارسال‌شده وارد وضعیت پروژه نشده‌اند.";
  }
  if (snapshot.operationalStatus === "InsufficientData") {
    return `پوشش یا تازگی داده کافی نیست. این وضعیت «نامعلوم» است، نه خوب و نه بد. ${fallback}`;
  }
  return "این ارزیابی فقط از عملیات روزانه تأییدشده و قواعد نسخه‌دار موتور محاسبات ساخته شده است.";
}

function coverageLabel(snapshot: CommandCenterModel["snapshot"]): string {
  if (!snapshot || snapshot.coverageStatus === "NoData") {
    return "—";
  }
  return `${snapshot.coveragePercent.toLocaleString("fa-IR")}٪`;
}

function coverageBasisLabel(snapshot: CommandCenterModel["snapshot"]): string {
  if (snapshot?.coverageBasis === "ConfiguredWorkingDays") {
    return `پوشش ${snapshot.expectedReportDays.toLocaleString("fa-IR")} روز کاری`;
  }
  return "پوشش هفت روز تقویمی";
}

function priorityLabel(priority: ProjectAttentionPriority): string {
  return ({
    Unassessed: "ارزیابی‌نشده",
    Low: "کم",
    Medium: "متوسط",
    High: "زیاد",
    Critical: "بحرانی",
  } as Record<ProjectAttentionPriority, string>)[priority];
}

function financialStateTitle(state: CommandCenterModel["financialState"]): string {
  if (!state || state.status === "NoData") return "هنوز سند مالی قطعی وجود ندارد";
  if (state.status === "NotConfigured") return "کنترل مالی پایه برای پروژه فعال نیست";
  return "وضعیت مالی از اسناد قطعی در دسترس است";
}

function formatFinancialAmount(value?: number, currency = "IRR"): string {
  return formatAmountFa(value, currency);
}

function budgetComparisonLabel(state?: NonNullable<CommandCenterModel["financialState"]>["budgetComparisonState"]): string {
  return ({
    NotConfigured: "پیکربندی نشده",
    SetupRequired: "بودجه ثبت نشده",
    NoData: "بدون بودجه مبنای مصوب",
    Available: "فعال",
    Suspended: "تعلیق‌شده",
  } as const)[state ?? "NotConfigured"];
}

function commercialStateTitle(state: CommandCenterModel["commercialState"]): string {
  if (!state || state.contractState === "NoData" && state.procurementState === "NoData") {
    return "هنوز داده قراردادی یا خرید وجود ندارد";
  }
  if (state.contractState === "NotConfigured" && state.procurementState === "NotConfigured") {
    return "کنترل قرارداد و خرید برای پروژه فعال نیست";
  }
  if (state.expiredActiveContractCount > 0 || state.overdueCommitmentCount > 0) {
    return "مورد قراردادی یا تعهد سررسیدگذشته وجود دارد";
  }
  return "وضعیت قرارداد و تعهدات قابل ردیابی است";
}

function formatCommercialCeiling(state: CommandCenterModel["commercialState"]): string {
  if (!state || state.approvedContractCeilingAmount === null) return "ثبت نشده";
  return formatAmountFa(state.approvedContractCeilingAmount, state.currencyCode);
}

function formatStateDate(value: string): string {
  return formatPersianDate(value);
}
