"use client";

import { type FormEvent, useCallback, useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { PmcsSessionBoundary, SessionBadge, usePmcsSession } from "@/components/pmcs-session";
import { PersianDateInput } from "@/components/persian-date-input";
import {
  activateProject,
  configureProjectSetup,
  createProject,
  getProjectReadiness,
  listProjects,
  type CapabilityMode,
  type ContractModel,
  type CreateProjectInput,
  type PlanningMode,
  type ProjectExecutionPhase,
  type ProjectModel,
  type ProjectReadinessModel,
  type ProjectStatus,
  type ProjectType,
} from "@/lib/projects";
import { toUserMessage } from "@/lib/localization";

const apiBaseUrl = "/api/pmcs";
const initialProject: CreateProjectInput = {
  code: "",
  name: "",
  contractModel: "GeneralContracting",
  planningMode: "SimpleWorkList",
  budgetMode: "SetupRequired",
  qualityMode: "SetupRequired",
  hseMode: "NotEnabled",
  financeMode: "SetupRequired",
  procurementMode: "SetupRequired",
  baseCurrencyCode: "IRR",
  timeZone: "Asia/Tehran",
  projectType: "Building",
  executionPhase: "PreConstruction",
  countryCode: "IR",
  region: "",
  startDate: "",
  plannedFinishDate: "",
  shortDescription: "",
  unitSystem: "Metric",
  dailyCutoffLocalTime: "18:00",
  reportingFrequency: "WorkingDays",
  dailyReportWorkflow: "OneStepApproval",
  offlinePolicyAccepted: false,
  calendarMode: "WorkingWeek",
  workingDays: ["Saturday", "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday"],
};

export function ProjectLanding() {
  return (
    <PmcsSessionBoundary>
      <ProjectLandingContent />
    </PmcsSessionBoundary>
  );
}

function ProjectLandingContent() {
  const session = usePmcsSession();
  const identity = useMemo(
    () => ({ tenantId: session.tenantId, userId: session.userId }),
    [session.tenantId, session.userId],
  );
  const [projects, setProjects] = useState<readonly ProjectModel[]>([]);
  const [readiness, setReadiness] = useState<Record<string, ProjectReadinessModel>>({});
  const [draft, setDraft] = useState<CreateProjectInput>(initialProject);
  const [message, setMessage] = useState("در حال دریافت پروژه‌های مجاز…");
  const [isLoading, setIsLoading] = useState(true);
  const [isCreating, setIsCreating] = useState(false);
  const [editingProject, setEditingProject] = useState<ProjectModel | null>(null);
  const [activatingId, setActivatingId] = useState<string | null>(null);

  const load = useCallback(async () => {
    setIsLoading(true);
    try {
      const loaded = await listProjects(apiBaseUrl, identity);
      setProjects(loaded);
      const readinessEntries = await Promise.all(loaded
        .filter((project) => project.status === "Draft")
        .map(async (project) => {
          try {
            return [project.id, await getProjectReadiness(apiBaseUrl, identity, project.id)] as const;
          } catch {
            return null;
          }
        }));
      setReadiness(Object.fromEntries(readinessEntries.filter((entry): entry is readonly [string, ProjectReadinessModel] => entry !== null)));
      setMessage(loaded.length > 0
        ? `${loaded.length.toLocaleString("fa-IR")} پروژه در محدوده دسترسی شما قرار دارد.`
        : "هنوز پروژه‌ای در محدوده دسترسی شما وجود ندارد.");
    } catch (error) {
      setMessage(toUserMessage(error, "فهرست پروژه‌ها دریافت نشد."));
    } finally {
      setIsLoading(false);
    }
  }, [identity]);

  useEffect(() => {
    const timeoutId = window.setTimeout(() => {
      void load();
    }, 0);
    return () => window.clearTimeout(timeoutId);
  }, [load]);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setIsCreating(true);
    setMessage(editingProject ? "در حال ذخیرهٔ نسخه جدید تنظیمات…" : "در حال ایجاد پیش‌نویس پروژه…");
    try {
      const saved = editingProject
        ? await configureProjectSetup(apiBaseUrl, identity, editingProject.id, editingProject.revision, draft)
        : await createProject(apiBaseUrl, identity, draft);
      setProjects((current) => editingProject
        ? current.map((item) => item.id === saved.id ? saved : item)
        : [...current, saved].sort((left, right) => left.code.localeCompare(right.code)));
      const savedReadiness = await getProjectReadiness(apiBaseUrl, identity, saved.id);
      setReadiness((current) => ({ ...current, [saved.id]: savedReadiness }));
      setDraft(initialProject);
      setEditingProject(null);
      setMessage(editingProject
        ? "نسخه جدید تنظیمات ثبت و چک‌لیست آمادگی دوباره محاسبه شد."
        : "پروژه در وضعیت پیش‌نویس ایجاد شد؛ مدیر پروژه را تعیین و سپس چک‌لیست را کنترل کنید.");
    } catch (error) {
      setMessage(toUserMessage(error, "ایجاد پروژه انجام نشد."));
    } finally {
      setIsCreating(false);
    }
  }

  function edit(project: ProjectModel) {
    setEditingProject(project);
    setDraft(projectToInput(project));
    setMessage(`تنظیمات ذخیره‌شدهٔ پروژه ${project.name} برای ادامه راه‌اندازی باز شد.`);
    window.setTimeout(() => document.getElementById("project-create-title")?.scrollIntoView({ behavior: "smooth" }), 0);
  }

  async function activate(project: ProjectModel) {
    if (!readiness[project.id]?.isReady) {
      setMessage("پروژه هنوز آماده فعال‌سازی نیست؛ موارد مسدودکنندهٔ چک‌لیست را تکمیل کنید.");
      return;
    }
    setActivatingId(project.id);
    setMessage(`در حال فعال‌سازی پروژه ${project.name}…`);
    try {
      const activated = await activateProject(apiBaseUrl, identity, project.id, project.revision);
      setProjects((current) => current.map((item) => item.id === activated.id ? activated : item));
      setMessage(`پروژه ${activated.name} فعال شد و آماده ورود است.`);
    } catch (error) {
      setMessage(toUserMessage(error, "فعال‌سازی پروژه انجام نشد؛ فهرست را تازه‌سازی کنید."));
    } finally {
      setActivatingId(null);
    }
  }

  return (
    <main className="project-landing">
      <header className="project-landing-header">
        <div>
          <span className="auth-state-mark">پ</span>
          <p className="eyebrow">سامانه کنترل مدیریت پروژه</p>
          <h1>پروژه‌های در دسترس</h1>
          <p className="muted">پروژه را آگاهانه انتخاب کنید؛ هیچ داده یا شناسهٔ نمونه‌ای به‌صورت پیش‌فرض باز نمی‌شود.</p>
        </div>
        <div className="project-landing-account">
          <Link href="/profile">پروفایل من</Link>
          {session.tenantRole === "TenantAdministrator" && <Link href="/admin/login-experience">ظاهر صفحه ورود</Link>}
          <SessionBadge />
        </div>
      </header>

      <section className="project-list-section" aria-labelledby="project-list-title">
        <div className="section-title">
          <div>
            <h2 id="project-list-title">فهرست پروژه‌ها</h2>
            <p className="muted" aria-live="polite">{message}</p>
          </div>
          <button type="button" className="secondary-button" disabled={isLoading} onClick={() => void load()}>
            {isLoading ? "در حال دریافت…" : "تازه‌سازی"}
          </button>
        </div>

        {!isLoading && projects.length === 0 && (
          <div className="empty-project-state">
            <h3>پروژه‌ای برای نمایش وجود ندارد</h3>
            <p>مدیر سامانه باید پروژه ایجاد کند یا عضویت شما را به یک پروژهٔ فعال بیفزاید.</p>
          </div>
        )}

        <div className="project-card-grid">
          {projects.map((project) => (
            <article className="project-card" key={project.id}>
              <div className="project-card-heading">
                <div>
                  <span className="project-code">{project.code}</span>
                  <h3>{project.name}</h3>
                </div>
                <span className={`project-status project-status-${project.status.toLowerCase()}`}>
                  {statusLabel(project.status)}
                </span>
              </div>
              <dl>
                <div><dt>مدل برنامه‌ریزی</dt><dd>{planningLabel(project.planningMode)}</dd></div>
                <div><dt>منطقه زمانی</dt><dd>{project.timeZone}</dd></div>
                <div><dt>مالی</dt><dd>{capabilityLabel(project.financeMode)}</dd></div>
                <div><dt>ایمنی</dt><dd>{capabilityLabel(project.hseMode)}</dd></div>
              </dl>
              {project.status === "Draft" && readiness[project.id] && (
                <div className="project-readiness" aria-label="چک‌لیست آمادگی فعال‌سازی">
                  <div className="project-readiness-summary">
                    <strong>آمادگی {readiness[project.id].completionPercent.toLocaleString("fa-IR")}٪</strong>
                    <span>{readiness[project.id].isReady ? "آمادهٔ فعال‌سازی" : "نیازمند تکمیل"}</span>
                  </div>
                  <ul>
                    {readiness[project.id].items
                      .filter((item) => item.status !== "Passed")
                      .map((item) => (
                        <li key={item.code} className={`readiness-${item.status.toLowerCase()}`}>
                          <strong>{item.title}</strong><span>{item.detail}</span>
                        </li>
                      ))}
                  </ul>
                  {readiness[project.id].items.some((item) => item.code === "project-manager" && item.status === "Blocked") && (
                    <Link className="secondary-button" href="/admin/users">تعیین مدیر پروژه</Link>
                  )}
                </div>
              )}
              <div className="project-card-actions">
                {project.status === "Active" ? (
                  <Link className="primary-link" href={`/projects/${project.id}`}>ورود به مرکز فرمان</Link>
                ) : (
                  <span className="muted">ورود عملیاتی پس از فعال‌سازی ممکن است.</span>
                )}
                {session.tenantRole === "TenantAdministrator" && project.status === "Draft" && (
                  <>
                    <button className="secondary-button" type="button" disabled={activatingId !== null} onClick={() => edit(project)}>تکمیل تنظیمات</button>
                    <button
                      type="button"
                      disabled={activatingId !== null || !readiness[project.id]?.isReady}
                      title={!readiness[project.id]?.isReady ? "ابتدا تمام موارد مسدودکنندهٔ آمادگی را تکمیل کنید." : undefined}
                      onClick={() => void activate(project)}
                    >
                      {activatingId === project.id ? "در حال فعال‌سازی…" : "فعال‌سازی پروژه"}
                    </button>
                  </>
                )}
              </div>
            </article>
          ))}
        </div>
      </section>

      {session.tenantRole === "TenantAdministrator" && (
        <section className="project-create-section" aria-labelledby="project-create-title">
          <div>
            <p className="eyebrow">راه‌اندازی کنترل‌شده</p>
            <h2 id="project-create-title">{editingProject ? "ادامه راه‌اندازی پروژه" : "ایجاد پروژه"}</h2>
            <p className="muted">{editingProject ? "فرم از نسخه ذخیره‌شده بازیابی شده است؛ ذخیره، نسخه تنظیمات و آمادگی را به‌روزرسانی می‌کند." : "پروژه ابتدا در وضعیت پیش‌نویس ساخته می‌شود تا تنظیمات پایه قبل از ورود عملیات کنترل شود."}</p>
          </div>
          <form className="project-create-form" onSubmit={submit}>
            <label>
              نام پروژه
              <input required readOnly={Boolean(editingProject)} maxLength={200} value={draft.name} onChange={(event) => setDraft((current) => ({ ...current, name: event.target.value }))} />
            </label>
            <label>
              کد پروژه
              <input required readOnly={Boolean(editingProject)} minLength={2} maxLength={32} dir="ltr" value={draft.code} onChange={(event) => setDraft((current) => ({ ...current, code: event.target.value }))} placeholder="مثال: پروژه-۰۱" />
            </label>
            <label>
              نوع پروژه
              <select value={draft.projectType} onChange={(event) => setDraft((current) => ({ ...current, projectType: event.target.value as ProjectType }))}>
                <option value="Building">ساختمانی</option><option value="Industrial">صنعتی</option>
                <option value="Infrastructure">زیرساختی</option><option value="Renovation">بازسازی</option>
                <option value="Landscaping">محوطه‌سازی</option><option value="Mixed">ترکیبی</option>
              </select>
            </label>
            <label>
              مرحله اجرا
              <select value={draft.executionPhase} onChange={(event) => setDraft((current) => ({ ...current, executionPhase: event.target.value as ProjectExecutionPhase }))}>
                <option value="PreConstruction">پیش از اجرا</option><option value="ActiveExecution">در حال اجرا</option>
                <option value="OnHold">متوقف</option><option value="Closing">در حال خاتمه</option>
              </select>
            </label>
            <label>
              کشور
              <input required minLength={2} maxLength={2} dir="ltr" value={draft.countryCode} onChange={(event) => setDraft((current) => ({ ...current, countryCode: event.target.value.toUpperCase() }))} />
            </label>
            <label>
              استان یا منطقه
              <input required maxLength={200} value={draft.region} onChange={(event) => setDraft((current) => ({ ...current, region: event.target.value }))} />
            </label>
            <label>
              تاریخ شروع (شمسی)
              <PersianDateInput required value={draft.startDate} onChange={(startDate) => setDraft((current) => ({ ...current, startDate }))} ariaLabel="تاریخ شروع شمسی پروژه" />
            </label>
            <label>
              تاریخ پایان برنامه‌ای (شمسی)
              <PersianDateInput required value={draft.plannedFinishDate} onChange={(plannedFinishDate) => setDraft((current) => ({ ...current, plannedFinishDate }))} ariaLabel="تاریخ پایان برنامه‌ای شمسی پروژه" />
            </label>
            <label className="project-description-field">
              شرح کوتاه
              <textarea required maxLength={1000} value={draft.shortDescription} onChange={(event) => setDraft((current) => ({ ...current, shortDescription: event.target.value }))} />
            </label>
            <label>
              مدل قراردادی
              <select value={draft.contractModel} onChange={(event) => setDraft((current) => ({ ...current, contractModel: event.target.value as ContractModel }))}>
                <option value="GeneralContracting">پیمانکاری عمومی</option>
                <option value="ConstructionManagement">مدیریت پیمان</option>
                <option value="LaborOnly">دستمزدی</option>
                <option value="Hybrid">ترکیبی</option>
              </select>
            </label>
            <label>
              روش برنامه‌ریزی
              <select value={draft.planningMode} onChange={(event) => setDraft((current) => ({ ...current, planningMode: event.target.value as PlanningMode }))}>
                <option value="None">بدون برنامه مبنا</option>
                <option value="SimpleWorkList">فهرست کار ساده</option>
                <option value="Milestones">نقاط عطف</option>
                <option value="WbsBaseline">ساختار شکست کار و مبنا</option>
                <option value="ExternalSchedule">برنامه بیرونی</option>
              </select>
            </label>
            <CapabilitySelect label="بودجه" value={draft.budgetMode} onChange={(budgetMode) => setDraft((current) => ({ ...current, budgetMode }))} />
            <CapabilitySelect label="مالی" value={draft.financeMode} onChange={(financeMode) => setDraft((current) => ({ ...current, financeMode }))} />
            <CapabilitySelect label="تدارکات" value={draft.procurementMode} onChange={(procurementMode) => setDraft((current) => ({ ...current, procurementMode }))} />
            <CapabilitySelect label="کیفیت" value={draft.qualityMode} onChange={(qualityMode) => setDraft((current) => ({ ...current, qualityMode }))} />
            <CapabilitySelect label="ایمنی" value={draft.hseMode} onChange={(hseMode) => setDraft((current) => ({ ...current, hseMode }))} />
            <label>
              منطقه زمانی
              <input required maxLength={100} dir="ltr" value={draft.timeZone} onChange={(event) => setDraft((current) => ({ ...current, timeZone: event.target.value }))} />
            </label>
            <label>
              ارز پایه
              <input required minLength={3} maxLength={3} dir="ltr" value={draft.baseCurrencyCode} onChange={(event) => setDraft((current) => ({ ...current, baseCurrencyCode: event.target.value.toUpperCase() }))} />
            </label>
            <label>
              زمان قطع گزارش روزانه
              <input required type="time" value={draft.dailyCutoffLocalTime} onChange={(event) => setDraft((current) => ({ ...current, dailyCutoffLocalTime: event.target.value }))} />
            </label>
            <label>
              بسامد گزارش روزانه
              <select value={draft.reportingFrequency} onChange={(event) => setDraft((current) => ({ ...current, reportingFrequency: event.target.value as CreateProjectInput["reportingFrequency"] }))}>
                <option value="Daily">هر روز</option><option value="WorkingDays">روزهای کاری</option><option value="Weekly">هفتگی</option>
              </select>
            </label>
            <fieldset className="project-working-days">
              <legend>روزهای کاری</legend>
              {(["Saturday", "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday"] as const).map((day) => (
                <label key={day}><input type="checkbox" checked={draft.workingDays.includes(day)} onChange={(event) => setDraft((current) => ({
                  ...current,
                  workingDays: event.target.checked ? [...current.workingDays, day] : current.workingDays.filter((item) => item !== day),
                }))} />{weekdayLabel(day)}</label>
              ))}
            </fieldset>
            <label className="project-policy-acceptance">
              <input type="checkbox" required checked={draft.offlinePolicyAccepted} onChange={(event) => setDraft((current) => ({ ...current, offlinePolicyAccepted: event.target.checked }))} />
              سیاست ثبت آفلاین، همگام‌سازی و رسیدگی به تعارض را می‌پذیرم.
            </label>
            <div className="project-create-actions">
              <button type="submit" disabled={isCreating}>{isCreating ? "در حال ذخیره…" : editingProject ? "ذخیره نسخه تنظیمات" : "ایجاد پیش‌نویس پروژه"}</button>
              {editingProject && <button className="secondary-button" type="button" disabled={isCreating} onClick={() => { setEditingProject(null); setDraft(initialProject); }}>انصراف</button>}
            </div>
          </form>
        </section>
      )}
    </main>
  );
}

function CapabilitySelect(props: {
  readonly label: string;
  readonly value: CapabilityMode;
  readonly onChange: (value: CapabilityMode) => void;
}) {
  return (
    <label>
      {props.label}
      <select value={props.value} onChange={(event) => props.onChange(event.target.value as CapabilityMode)}>
        <option value="NotEnabled">غیرفعال</option>
        <option value="SetupRequired">نیازمند راه‌اندازی</option>
        <option value="Active">فعال</option>
      </select>
    </label>
  );
}

function statusLabel(status: ProjectStatus): string {
  return ({ Draft: "پیش‌نویس", Active: "فعال", OnHold: "متوقف", Closing: "در حال خاتمه", Closed: "بسته" })[status];
}

function planningLabel(mode: PlanningMode): string {
  return ({
    None: "بدون مبنا",
    SimpleWorkList: "فهرست کار ساده",
    Milestones: "نقاط عطف",
    WbsBaseline: "ساختار شکست کار",
    ExternalSchedule: "برنامه بیرونی",
  })[mode];
}

function capabilityLabel(mode: CapabilityMode): string {
  return ({ NotEnabled: "غیرفعال", SetupRequired: "نیازمند راه‌اندازی", Active: "فعال", Suspended: "تعلیق‌شده" })[mode];
}

function weekdayLabel(day: CreateProjectInput["workingDays"][number]): string {
  return ({ Saturday: "شنبه", Sunday: "یکشنبه", Monday: "دوشنبه", Tuesday: "سه‌شنبه", Wednesday: "چهارشنبه", Thursday: "پنجشنبه", Friday: "جمعه" })[day];
}

function projectToInput(project: ProjectModel): CreateProjectInput {
  return {
    code: project.code,
    name: project.name,
    contractModel: project.contractModel,
    planningMode: project.planningMode,
    budgetMode: project.budgetMode,
    qualityMode: project.qualityMode,
    hseMode: project.hseMode,
    financeMode: project.financeMode,
    procurementMode: project.procurementMode,
    baseCurrencyCode: project.baseCurrencyCode,
    timeZone: project.timeZone,
    projectType: project.projectType,
    executionPhase: project.executionPhase,
    countryCode: project.countryCode,
    region: project.region,
    startDate: project.startDate ?? "",
    plannedFinishDate: project.plannedFinishDate ?? "",
    shortDescription: project.shortDescription,
    unitSystem: project.unitSystem,
    dailyCutoffLocalTime: project.dailyCutoffLocalTime?.slice(0, 5) ?? "",
    reportingFrequency: project.reportingFrequency,
    dailyReportWorkflow: project.dailyReportWorkflow,
    offlinePolicyAccepted: project.offlinePolicyAccepted,
    calendarMode: project.calendarMode,
    workingDays: project.workingDays,
  };
}
