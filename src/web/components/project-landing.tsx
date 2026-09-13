"use client";

import { type FormEvent, useCallback, useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { PmcsSessionBoundary, SessionBadge, usePmcsSession } from "@/components/pmcs-session";
import {
  activateProject,
  createProject,
  listProjects,
  type CapabilityMode,
  type ContractModel,
  type CreateProjectInput,
  type PlanningMode,
  type ProjectModel,
  type ProjectStatus,
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
  financeMode: "Active",
  procurementMode: "SetupRequired",
  baseCurrencyCode: "IRR",
  timeZone: "Asia/Tehran",
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
  const [draft, setDraft] = useState<CreateProjectInput>(initialProject);
  const [message, setMessage] = useState("در حال دریافت پروژه‌های مجاز…");
  const [isLoading, setIsLoading] = useState(true);
  const [isCreating, setIsCreating] = useState(false);
  const [activatingId, setActivatingId] = useState<string | null>(null);

  const load = useCallback(async () => {
    setIsLoading(true);
    try {
      const loaded = await listProjects(apiBaseUrl, identity);
      setProjects(loaded);
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
    setMessage("در حال ایجاد پیش‌نویس پروژه…");
    try {
      const created = await createProject(apiBaseUrl, identity, draft);
      setProjects((current) => [...current, created].sort((left, right) => left.code.localeCompare(right.code)));
      setDraft(initialProject);
      setMessage("پروژه در وضعیت پیش‌نویس ایجاد شد؛ پس از کنترل تنظیمات آن را فعال کنید.");
    } catch (error) {
      setMessage(toUserMessage(error, "ایجاد پروژه انجام نشد."));
    } finally {
      setIsCreating(false);
    }
  }

  async function activate(project: ProjectModel) {
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
        <SessionBadge />
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
              <div className="project-card-actions">
                {project.status === "Active" ? (
                  <Link className="primary-link" href={`/projects/${project.id}`}>ورود به مرکز فرمان</Link>
                ) : (
                  <span className="muted">ورود عملیاتی پس از فعال‌سازی ممکن است.</span>
                )}
                {session.tenantRole === "TenantAdministrator" && project.status === "Draft" && (
                  <button
                    type="button"
                    disabled={activatingId !== null || project.contractModel === "NotConfigured"}
                    title={project.contractModel === "NotConfigured" ? "ابتدا پروژه را با مدل قراردادی پایه معتبر ایجاد کنید." : undefined}
                    onClick={() => void activate(project)}
                  >
                    {activatingId === project.id ? "در حال فعال‌سازی…" : "فعال‌سازی پروژه"}
                  </button>
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
            <h2 id="project-create-title">ایجاد پروژه</h2>
            <p className="muted">پروژه ابتدا در وضعیت پیش‌نویس ساخته می‌شود تا تنظیمات پایه قبل از ورود عملیات کنترل شود.</p>
          </div>
          <form className="project-create-form" onSubmit={submit}>
            <label>
              نام پروژه
              <input required maxLength={200} value={draft.name} onChange={(event) => setDraft((current) => ({ ...current, name: event.target.value }))} />
            </label>
            <label>
              کد پروژه
              <input required minLength={2} maxLength={32} dir="ltr" value={draft.code} onChange={(event) => setDraft((current) => ({ ...current, code: event.target.value }))} placeholder="مثال: پروژه-۰۱" />
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
            <div className="project-create-actions">
              <button type="submit" disabled={isCreating}>{isCreating ? "در حال ایجاد…" : "ایجاد پیش‌نویس پروژه"}</button>
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
