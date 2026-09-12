"use client";

import Link from "next/link";
import { useEffect, useMemo, useState, useSyncExternalStore } from "react";
import type { ProjectFeatureState, ProjectOperationalStatus } from "@/lib/command-center";
import { currencyLabel, formatAmountFa, toUserMessage } from "@/lib/localization";
import { formatPersianDate, formatPersianDateTime } from "@/lib/persian-date";
import {
  getPortfolioCommandCenter,
  type ActionPriority,
  type ContractModel,
  type ManagementActionStatus,
  type PlanningMode,
  type PortfolioActionExceptionModel,
  type PortfolioProjectModel,
  type ProjectLifecycleStatus,
} from "@/lib/portfolio";
import {
  selectPortfolioProjects,
  type PortfolioAttentionFilter,
  type PortfolioLifecycleFilter,
  type PortfolioSort,
} from "@/lib/portfolio-view";
import { PmcsSessionBoundary, SessionBadge, usePmcsSession } from "@/components/pmcs-session";

const apiBaseUrl = "/api/pmcs";

export function PortfolioDashboard() {
  return (
    <PmcsSessionBoundary>
      <PortfolioDashboardContent />
    </PmcsSessionBoundary>
  );
}

function PortfolioDashboardContent() {
  const session = usePmcsSession();
  const [model, setModel] = useState<Awaited<ReturnType<typeof getPortfolioCommandCenter>> | null>(null);
  const [message, setMessage] = useState("در حال ساخت نمای مدیریتی از آخرین وضعیت‌های رسمی…");
  const [isLoading, setIsLoading] = useState(true);
  const [reloadToken, setReloadToken] = useState(0);
  const [query, setQuery] = useState("");
  const [attention, setAttention] = useState<PortfolioAttentionFilter>("All");
  const [lifecycle, setLifecycle] = useState<PortfolioLifecycleFilter>("All");
  const [sort, setSort] = useState<PortfolioSort>("attention");
  const isOnline = useSyncExternalStore(subscribeToOnlineState, readOnlineState, () => true);

  useEffect(() => {
    let active = true;
    const timeoutId = window.setTimeout(() => {
      if (!isOnline) {
        setIsLoading(false);
        setMessage("مرکز فرمان سبد پروژه‌ها برای تجمیع امن داده‌ها به اتصال سرور نیاز دارد.");
        return;
      }

      setIsLoading(true);
      void getPortfolioCommandCenter(apiBaseUrl, {
        tenantId: session.tenantId,
        userId: session.userId,
      })
        .then((result) => {
          if (!active) return;
          setModel(result);
          setMessage("نمای سبد از آخرین تصاویر رسمی و داده‌های مجاز هر پروژه ساخته شد.");
        })
        .catch((error: unknown) => {
          if (!active) return;
          setModel(null);
          setMessage(toUserMessage(error, "نمای سبد پروژه‌ها از سرور دریافت نشد."));
        })
        .finally(() => {
          if (active) setIsLoading(false);
        });
    }, 0);

    return () => {
      active = false;
      window.clearTimeout(timeoutId);
    };
  }, [isOnline, reloadToken, session.tenantId, session.userId]);

  const projects = useMemo(() => selectPortfolioProjects(model?.projects ?? [], {
    query,
    attention,
    lifecycle,
    sort,
  }), [attention, lifecycle, model?.projects, query, sort]);

  return (
    <main className="app-shell portfolio-shell">
      <aside className="sidebar" aria-label="ناوبری اصلی">
        <div className="brand-mark" aria-label="سامانه کنترل مدیریت پروژه"><span>پ</span></div>
        <nav>
          <Link className="nav-item active" href="/portfolio">سبد پروژه‌ها</Link>
          <Link className="nav-item" href="/">مرکز فرمان پروژه</Link>
          <a className="nav-item" href="#exceptions">اقدامات کلیدی</a>
          <a className="nav-item" href="#exposure">نمای مالی</a>
          {session.tenantRole === "TenantAdministrator" && <Link className="nav-item" href="/admin/users">کاربران و دسترسی‌ها</Link>}
        </nav>
        <div className="sidebar-meta">
          <span className={isOnline ? "online-dot" : "offline-dot"} />
          {isOnline ? "متصل به سرور" : "بدون اتصال"}
        </div>
        <SessionBadge />
      </aside>

      <section className="workspace portfolio-workspace">
        <header className="topbar portfolio-topbar">
          <div>
            <p className="eyebrow">دید مدیریتی سازمان</p>
            <h1>مرکز فرمان سبد پروژه‌ها</h1>
            <p className="portfolio-lead">وضعیت‌های مستقل، استثناهای اجرایی و مسئول اقدام؛ بدون امتیاز سلامت ساختگی</p>
          </div>
          <div className="portfolio-refresh">
            {model && <span>آخرین تجمیع: {formatDateTimeFa(model.generatedAt)}</span>}
            <button
              className="secondary-button"
              type="button"
              disabled={!isOnline || isLoading}
              onClick={() => setReloadToken((current) => current + 1)}
            >
              {isLoading ? "در حال دریافت…" : "تازه‌سازی"}
            </button>
          </div>
        </header>

        <p className={`portfolio-system-message ${!isOnline ? "warning" : ""}`} aria-live="polite">
          {message}
        </p>

        {model && (
          <>
            <section className="portfolio-kpis" aria-label="شاخص‌های کلیدی سبد پروژه‌ها">
              <Kpi label="پروژه در دامنه دسترسی" value={model.header.projectCount} hint={`${model.header.activeProjectCount.toLocaleString("fa-IR")} فعال`} />
              <Kpi label="عملیات بحرانی یا پرریسک" value={model.header.criticalProjectCount + model.header.atRiskProjectCount} hint={`${model.header.watchProjectCount.toLocaleString("fa-IR")} نیازمند پایش`} tone="danger" />
              <Kpi label="بدون داده یا داده ناکافی" value={model.header.noDataProjectCount + model.header.insufficientDataProjectCount} hint="به‌عنوان وضعیت خوب محاسبه نشده" tone="unknown" />
              <Kpi label="اقدام سررسیدگذشته" value={model.header.overdueActionCount} hint={`از ${model.header.openActionCount.toLocaleString("fa-IR")} اقدام باز`} tone="warning" />
              <Kpi label="تأیید تجاری معطل" value={model.header.pendingCommercialApprovalCount} hint="قرارداد و درخواست خرید" />
            </section>

            <section className="portfolio-exposure" id="exposure" aria-labelledby="exposure-title">
              <div className="section-title">
                <div>
                  <p className="eyebrow">نمای مالی و تعهدات</p>
                  <h2 id="exposure-title">تجمیع به تفکیک ارز</h2>
                </div>
                <span className="section-note">هیچ تبدیل ارزی پنهانی انجام نشده است</span>
              </div>
              {model.header.currencyExposures.length === 0 ? (
                <p className="empty-state">هنوز وضعیت مالی یا تعهد خریدِ قابل تجمیعی وجود ندارد.</p>
              ) : (
                <div className="currency-grid">
                  {model.header.currencyExposures.map((exposure) => (
                    <article className="currency-card" key={exposure.currencyCode}>
                      <div className="currency-title">
                        <strong>{currencyLabel(exposure.currencyCode)}</strong>
                        <span>{(exposure.financialProjectCount + exposure.commercialProjectCount).toLocaleString("fa-IR")} منبع پروژه‌ای</span>
                      </div>
                      <dl>
                        <div><dt>هزینه شناسایی‌شده</dt><dd>{formatAmountFa(exposure.recognizedSpend, exposure.currencyCode)}</dd></div>
                        <div><dt>خالص جریان نقد بیرونی</dt><dd>{formatAmountFa(exposure.externalNetCash, exposure.currencyCode)}</dd></div>
                        <div><dt>کل تعهد خرید</dt><dd>{formatAmountFa(exposure.totalCommittedAmount, exposure.currencyCode)}</dd></div>
                        <div><dt>تعهد خرید باز</dt><dd>{formatAmountFa(exposure.openCommitmentAmount, exposure.currencyCode)}</dd></div>
                      </dl>
                    </article>
                  ))}
                </div>
              )}
            </section>

            <section className="portfolio-projects" aria-labelledby="projects-title">
              <div className="section-title portfolio-project-title">
                <div>
                  <p className="eyebrow">مقایسه پروژه‌ها</p>
                  <h2 id="projects-title">تصویر مستقل هر پروژه</h2>
                </div>
                <span className="section-note">{projects.length.toLocaleString("fa-IR")} نتیجه</span>
              </div>
              <div className="portfolio-filters" aria-label="فیلتر و مرتب‌سازی پروژه‌ها">
                <label>
                  <span>جست‌وجو</span>
                  <input value={query} onChange={(event) => setQuery(event.target.value)} placeholder="نام، کد یا مدیر پروژه" />
                </label>
                <label>
                  <span>چرخه پروژه</span>
                  <select value={lifecycle} onChange={(event) => setLifecycle(event.target.value as PortfolioLifecycleFilter)}>
                    <option value="All">همه وضعیت‌ها</option>
                    <option value="Draft">پیش‌نویس</option>
                    <option value="Active">فعال</option>
                    <option value="OnHold">متوقف</option>
                    <option value="Closing">در حال خاتمه</option>
                    <option value="Closed">خاتمه‌یافته</option>
                  </select>
                </label>
                <label>
                  <span>نیاز مدیریتی</span>
                  <select value={attention} onChange={(event) => setAttention(event.target.value as PortfolioAttentionFilter)}>
                    <option value="All">همه پروژه‌ها</option>
                    <option value="NeedsAttention">نیازمند رسیدگی</option>
                    <option value="Stale">داده قدیمی</option>
                    <option value="NoData">بدون تصویر وضعیت</option>
                  </select>
                </label>
                <label>
                  <span>مرتب‌سازی</span>
                  <select value={sort} onChange={(event) => setSort(event.target.value as PortfolioSort)}>
                    <option value="attention">اولویت مدیریتی</option>
                    <option value="name">نام پروژه</option>
                    <option value="coverage">پوشش داده</option>
                  </select>
                </label>
              </div>

              {projects.length === 0 ? (
                <p className="empty-state">پروژه‌ای مطابق فیلترهای انتخاب‌شده پیدا نشد.</p>
              ) : (
                <div className="portfolio-card-list">
                  {projects.map((project) => <ProjectCard key={project.projectId} project={project} />)}
                </div>
              )}
            </section>

            <ActionExceptions actions={model.actionExceptions} />
          </>
        )}

        {!model && !isLoading && (
          <section className="portfolio-empty-panel">
            <h2>{isOnline ? "داده سبد در دسترس نیست" : "اتصال سرور برقرار نیست"}</h2>
            <p>{message}</p>
            <button className="secondary-button" type="button" disabled={!isOnline} onClick={() => setReloadToken((current) => current + 1)}>
              تلاش دوباره
            </button>
          </section>
        )}
      </section>
    </main>
  );
}

function Kpi({ label, value, hint, tone = "default" }: {
  readonly label: string;
  readonly value: number;
  readonly hint: string;
  readonly tone?: "default" | "danger" | "warning" | "unknown";
}) {
  return (
    <article className={`portfolio-kpi tone-${tone}`}>
      <span>{label}</span>
      <strong>{value.toLocaleString("fa-IR")}</strong>
      <small>{hint}</small>
    </article>
  );
}

function ProjectCard({ project }: { readonly project: PortfolioProjectModel }) {
  const optionalCapabilities = project.capabilities.filter((capability) =>
    ["planning", "budget", "hse"].includes(capability.key) &&
    (capability.configurationState !== "Active" || capability.metricState !== "Available"));
  return (
    <article className={`portfolio-project-card status-${project.operational.status.toLowerCase()}`}>
      <div className="project-card-heading">
        <div>
          <div className="project-card-meta">
            <span>{project.projectCode}</span>
            <span>{lifecycleLabel(project.lifecycleStatus)}</span>
            <span>{contractLabel(project.contractModel)}</span>
            <span>{planningLabel(project.planningMode)}</span>
          </div>
          <h3>{project.projectName}</h3>
          <p>{project.projectManagers.length > 0 ? `مدیر پروژه: ${project.projectManagers.join("، ")}` : "مدیر پروژه تعیین نشده است"}</p>
        </div>
        <div className="project-card-status">
          <strong>{operationalLabel(project.operational.status)}</strong>
          <span>{operationalDetail(project)}</span>
        </div>
      </div>

      <div className="project-state-columns">
        <section>
          <span className="state-column-label">عملیات</span>
          <strong>{project.operational.coveragePercent === null ? "پوشش نامعلوم" : `پوشش ${project.operational.coveragePercent.toLocaleString("fa-IR")}٪`}</strong>
          <small>{project.operational.asOfDate ? `به تاریخ ${formatDateFa(project.operational.asOfDate)}` : "تصویر رسمی ساخته نشده"}</small>
          <div className="inline-badges">
            {project.operational.isOutdated && <span className="stale-badge">نیازمند به‌روزرسانی</span>}
            {project.operational.freshnessStatus === "Stale" && <span className="stale-badge">داده کهنه</span>}
          </div>
        </section>
        <section>
          <span className="state-column-label">مالی</span>
          {!project.canReadFinance ? (
            <><strong>دسترسی محدود</strong><small>جمع مالی برای شما نمایش داده نمی‌شود</small></>
          ) : (
            <>
              <strong>{financialLabel(project)}</strong>
              <small>{project.financial?.status === "Available" ? `هزینه ${formatAmountFa(project.financial.recognizedSpend, project.financial.currencyCode)}` : budgetLabel(project)}</small>
            </>
          )}
        </section>
        <section>
          <span className="state-column-label">قرارداد و خرید</span>
          {!project.canReadCommercial ? (
            <><strong>دسترسی محدود</strong><small>اطلاعات تجاری برای شما نمایش داده نمی‌شود</small></>
          ) : (
            <>
              <strong>{commercialLabel(project)}</strong>
              <small>{project.commercial?.procurementState === "Available"
                ? `${project.commercial.openCommitmentCount.toLocaleString("fa-IR")} تعهد باز · ${project.commercial.pendingProcurementApprovalCount.toLocaleString("fa-IR")} تأیید معطل`
                : "تعهد خرید قابل محاسبه نیست"}</small>
            </>
          )}
        </section>
        <section>
          <span className="state-column-label">اقدامات</span>
          {!project.actions.isVisible ? (
            <><strong>دسترسی محدود</strong><small>اقدامات پروژه نمایش داده نمی‌شود</small></>
          ) : (
            <>
              <strong>{(project.actions.openCount ?? 0).toLocaleString("fa-IR")} اقدام باز</strong>
              <small>{(project.actions.overdueCount ?? 0) > 0 ? `${project.actions.overdueCount?.toLocaleString("fa-IR")} سررسیدگذشته` : "اقدام سررسیدگذشته ندارد"}</small>
            </>
          )}
        </section>
      </div>

      {(project.operational.topReasons.length > 0 || optionalCapabilities.length > 0) && (
        <div className="project-card-context">
          {project.operational.topReasons.length > 0 && (
            <div>
              <span>دلایل اصلی نیازمند توجه</span>
              <ul>{project.operational.topReasons.map((reason) => <li key={reason}>{reason}</li>)}</ul>
            </div>
          )}
          {optionalCapabilities.length > 0 && (
            <div className="optional-capabilities">
              <span>قابلیت‌های اختیاری</span>
              <div>{optionalCapabilities.map((capability) => (
                <small key={capability.key}>{capabilityName(capability.key)}: {capabilityStateLabel(capability.configurationState)}</small>
              ))}</div>
            </div>
          )}
        </div>
      )}

      <div className="project-card-footer">
        <span>ارز پایه: {currencyLabel(project.baseCurrencyCode)}</span>
        <Link className="project-drill-link" href={`/projects/${project.projectId}`}>ورود به مرکز فرمان پروژه</Link>
      </div>
    </article>
  );
}

function ActionExceptions({ actions }: { readonly actions: readonly PortfolioActionExceptionModel[] }) {
  return (
    <section className="portfolio-actions" id="exceptions" aria-labelledby="exceptions-title">
      <div className="section-title">
        <div>
          <p className="eyebrow">پیگیری بین‌پروژه‌ای</p>
          <h2 id="exceptions-title">اقدامات باز مهم</h2>
        </div>
        <span className="section-note">حداکثر ۵۰ مورد · فقط در دامنه دسترسی</span>
      </div>
      {actions.length === 0 ? (
        <p className="empty-state">اقدام باز قابل نمایشی وجود ندارد.</p>
      ) : (
        <div className="action-exception-list">
          {actions.map((action) => (
            <article className={action.isOverdue ? "overdue" : ""} key={action.actionId}>
              <div>
                <span className={`priority-badge priority-${action.priority.toLowerCase()}`}>{actionPriorityLabel(action.priority)}</span>
                <strong>{action.title}</strong>
                <small>{action.projectName} · {action.projectCode}</small>
              </div>
              <div><span>مسئول</span><strong>{action.assigneeDisplayName}</strong></div>
              <div><span>سررسید</span><strong>{formatDateFa(action.dueDate)}</strong></div>
              <div><span>وضعیت</span><strong>{actionStatusLabel(action.status)}</strong></div>
              <Link href={`/projects/${action.projectId}#actions`}>مشاهده پروژه</Link>
            </article>
          ))}
        </div>
      )}
    </section>
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

function lifecycleLabel(value: ProjectLifecycleStatus): string {
  return ({ Draft: "پیش‌نویس", Active: "فعال", OnHold: "متوقف", Closing: "در حال خاتمه", Closed: "خاتمه‌یافته" } as const)[value];
}

function contractLabel(value: ContractModel): string {
  return ({
    NotConfigured: "مدل قرارداد تعیین نشده",
    GeneralContracting: "پیمانکاری",
    ConstructionManagement: "مدیریت پیمان",
    LaborOnly: "دستمزدی",
    Hybrid: "ترکیبی",
  } as const)[value];
}

function planningLabel(value: PlanningMode): string {
  return ({
    None: "بدون ساختار برنامه‌ریزی",
    SimpleWorkList: "فهرست کار ساده",
    Milestones: "نقاط عطف",
    WbsBaseline: "ساختار شکست و خط مبنا",
    ExternalSchedule: "برنامه زمان‌بندی بیرونی",
  } as const)[value];
}

function operationalLabel(value: ProjectOperationalStatus): string {
  return ({
    NoData: "بدون داده",
    InsufficientData: "داده ناکافی",
    Stable: "پایدار",
    Watch: "نیازمند پایش",
    AtRisk: "در معرض ریسک",
    Critical: "بحرانی",
  } as const)[value];
}

function operationalDetail(project: PortfolioProjectModel): string {
  if (project.operational.needsCalculation) return "واقعیت تأییدشده وجود دارد؛ محاسبه لازم است";
  if (!project.operational.hasSnapshot) return "هنوز تصویر رسمی ساخته نشده است";
  if (project.operational.isOutdated) return "تصویر رسمی با داده جدید همگام نیست";
  return `${project.operational.attentionCount.toLocaleString("fa-IR")} مشاهده نیازمند توجه`;
}

function financialLabel(project: PortfolioProjectModel): string {
  if (!project.financial || project.financial.status === "NoData") return "بدون سند مالی قطعی";
  if (project.financial.status === "NotConfigured") return "کنترل مالی فعال نیست";
  return project.financial.dataQualityStatus === "NeedsAttention" ? "داده مالی نیازمند بررسی" : "داده مالی در دسترس";
}

function budgetLabel(project: PortfolioProjectModel): string {
  const state = project.financial?.budgetComparisonState ?? "NotConfigured";
  return ({
    NotConfigured: "بودجه اولیه پیکربندی نشده",
    SetupRequired: "بودجه اولیه ثبت نشده",
    NoData: "بودجه مبنای مصوب وجود ندارد",
    Available: "مقایسه بودجه در دسترس است",
    Suspended: "کنترل بودجه تعلیق شده",
  } as const)[state];
}

function commercialLabel(project: PortfolioProjectModel): string {
  const commercial = project.commercial;
  if (!commercial || commercial.contractState === "NoData" && commercial.procurementState === "NoData") return "بدون داده تجاری";
  if (commercial.contractState === "NotConfigured" && commercial.procurementState === "NotConfigured") return "قابلیت تجاری فعال نیست";
  if (commercial.expiredActiveContractCount > 0 || commercial.overdueCommitmentCount > 0) return "نیازمند رسیدگی";
  return "داده تجاری در دسترس";
}

function capabilityName(key: string): string {
  return ({ planning: "برنامه‌ریزی و ساختار شکست کار", budget: "بودجه", hse: "ایمنی، بهداشت و محیط‌زیست" } as Readonly<Record<string, string>>)[key] ?? "قابلیت پروژه";
}

function capabilityStateLabel(state: ProjectFeatureState): string {
  return ({
    NotConfigured: "پیکربندی نشده",
    NotEnabled: "فعال نیست",
    SetupRequired: "نیازمند راه‌اندازی",
    Active: "فعال ولی بدون داده",
    Suspended: "تعلیق‌شده",
  } as const)[state];
}

function actionPriorityLabel(value: ActionPriority): string {
  return ({ Low: "کم", Medium: "متوسط", High: "زیاد", Critical: "بحرانی" } as const)[value];
}

function actionStatusLabel(value: ManagementActionStatus): string {
  return ({ Open: "باز", InProgress: "در حال انجام", Blocked: "مسدود" } as const)[value];
}

function formatDateFa(value: string): string {
  return formatPersianDate(value);
}

function formatDateTimeFa(value: string): string {
  return formatPersianDateTime(value);
}
