"use client";

import Link from "next/link";
import { type FormEvent, useEffect, useMemo, useState } from "react";
import { PersianDateInput } from "@/components/persian-date-input";
import { SessionBadge, usePmcsSession } from "@/components/pmcs-session";
import { getIdentityDirectory, type UserDirectoryModel } from "@/lib/identity-administration";
import { toUserMessage } from "@/lib/localization";
import {
  activateProjectBootstrap,
  createProjectBootstrap,
  executeProjectBootstrap,
  listProjects,
  refreshProjectBootstrapPreview,
  type ProjectBootstrapCategory,
  type ProjectBootstrapConflictPolicy,
  type ProjectBootstrapMemberSelectionInput,
  type ProjectBootstrapPreviewModel,
  type ProjectBootstrapResultModel,
  type ProjectBootstrapTargetInput,
  type ProjectExecutionPhase,
  type ProjectModel,
  type ProjectType,
} from "@/lib/projects";

const apiBaseUrl = "/api/pmcs";
const defaultCategories: readonly ProjectBootstrapCategory[] = [
  "BaseSettings",
  "Calendar",
  "Locations",
  "RoleTemplates",
  "WorkflowTemplates",
  "FormTemplates",
  "ReportTemplates",
  "Lookups",
  "Members",
  "NotificationDefaults",
  "GroupDefaults",
];
const initialTarget: ProjectBootstrapTargetInput = {
  code: "",
  name: "",
  projectType: "Building",
  executionPhase: "PreConstruction",
  countryCode: "IR",
  region: "",
  startDate: "",
  plannedFinishDate: "",
  shortDescription: "",
  timeZone: "Asia/Tehran",
  baseCurrencyCode: "IRR",
  unitSystem: "Metric",
  offlinePolicyAccepted: false,
};

type WizardStep = "identity" | "scope" | "members" | "preview" | "result";

export function ProjectBootstrapWizard() {
  const session = usePmcsSession();
  const identity = useMemo(
    () => ({ tenantId: session.tenantId, userId: session.userId }),
    [session.tenantId, session.userId],
  );
  const storageKey = `pmcs-project-bootstrap-draft:${session.tenantId}:${session.userId}`;
  const [projects, setProjects] = useState<readonly ProjectModel[]>([]);
  const [users, setUsers] = useState<readonly UserDirectoryModel[]>([]);
  const [roles, setRoles] = useState<readonly string[]>([]);
  const [sourceProjectId, setSourceProjectId] = useState("");
  const [target, setTarget] = useState<ProjectBootstrapTargetInput>(initialTarget);
  const [categories, setCategories] = useState<readonly ProjectBootstrapCategory[]>(defaultCategories);
  const [members, setMembers] = useState<Record<string, ProjectBootstrapMemberSelectionInput>>({});
  const [conflictPolicy, setConflictPolicy] = useState<ProjectBootstrapConflictPolicy>("FailOnConflict");
  const [step, setStep] = useState<WizardStep>("identity");
  const [preview, setPreview] = useState<ProjectBootstrapPreviewModel | null>(null);
  const [result, setResult] = useState<ProjectBootstrapResultModel | null>(null);
  const [confirmed, setConfirmed] = useState(false);
  const [isBusy, setIsBusy] = useState(false);
  const [isOnline, setIsOnline] = useState(true);
  const [message, setMessage] = useState("در حال دریافت پروژه‌ها و اعضای مجاز…");

  useEffect(() => {
    const update = () => setIsOnline(window.navigator.onLine);
    const timeoutId = window.setTimeout(update, 0);
    window.addEventListener("online", update);
    window.addEventListener("offline", update);
    return () => {
      window.clearTimeout(timeoutId);
      window.removeEventListener("online", update);
      window.removeEventListener("offline", update);
    };
  }, []);

  useEffect(() => {
    try {
      const saved = window.localStorage.getItem(storageKey);
      if (!saved) return;
      const draft = JSON.parse(saved) as {
        sourceProjectId?: string;
        target?: ProjectBootstrapTargetInput;
        categories?: readonly ProjectBootstrapCategory[];
        conflictPolicy?: ProjectBootstrapConflictPolicy;
      };
      const timeoutId = window.setTimeout(() => {
        if (draft.sourceProjectId) setSourceProjectId(draft.sourceProjectId);
        if (draft.target) setTarget(draft.target);
        if (draft.categories?.length) setCategories(draft.categories);
        if (draft.conflictPolicy) setConflictPolicy(draft.conflictPolicy);
      }, 0);
      return () => window.clearTimeout(timeoutId);
    } catch {
      window.localStorage.removeItem(storageKey);
    }
    return undefined;
  }, [storageKey]);

  useEffect(() => {
    if (preview || result) return;
    window.localStorage.setItem(storageKey, JSON.stringify({ sourceProjectId, target, categories, conflictPolicy }));
  }, [categories, conflictPolicy, preview, result, sourceProjectId, storageKey, target]);

  useEffect(() => {
    let active = true;
    void Promise.allSettled([listProjects(apiBaseUrl, identity), getIdentityDirectory(apiBaseUrl)])
      .then(([projectResult, identityResult]) => {
        if (!active) return;
        if (projectResult.status === "fulfilled") {
          setProjects(projectResult.value);
          setSourceProjectId((current) => current || projectResult.value.find((item) => item.status === "Active")?.id || projectResult.value[0]?.id || "");
        }
        if (identityResult.status === "fulfilled") {
          setUsers(identityResult.value.users);
          setRoles(identityResult.value.projectRoles);
        }
        if (projectResult.status === "rejected") {
          setMessage(toUserMessage(projectResult.reason, "فهرست پروژه‌ها دریافت نشد."));
        } else if (identityResult.status === "rejected") {
          setMessage("پروژه‌ها دریافت شدند؛ فهرست اعضا در دسترس نیست و می‌توانید بدون انتقال عضو ادامه دهید.");
        } else {
          setMessage("مبدأ، هویت مستقل مقصد و اقلام مجاز را انتخاب کنید.");
        }
      });
    return () => { active = false; };
  }, [identity]);

  const memberCandidates = useMemo(() => users.flatMap((user) => {
    const membership = user.memberships.find((item) => item.projectId === sourceProjectId);
    return membership ? [{ user, membership }] : [];
  }), [sourceProjectId, users]);

  useEffect(() => {
    if (!sourceProjectId || preview) return;
    const selected = Object.fromEntries(memberCandidates
      .filter(({ user, membership }) => user.status === "Active" && membership.status === "Active")
      .map(({ user, membership }) => [user.id, {
        userId: user.id,
        roleCode: membership.roleCode,
        accessScope: "Project" as const,
      }]));
    const timeoutId = window.setTimeout(() => setMembers(selected), 0);
    return () => window.clearTimeout(timeoutId);
  }, [memberCandidates, preview, sourceProjectId]);

  async function createPreview(event: FormEvent) {
    event.preventDefault();
    if (!isOnline) {
      setMessage("انتخاب‌ها محلی حفظ شده‌اند؛ ساخت مقصد و پیش‌نمایش تازه به اتصال نیاز دارد.");
      return;
    }
    setIsBusy(true);
    setMessage("در حال ایجاد مقصد پیش‌نویس و محاسبه ارزیابی آزمایشی نسخه‌دار…");
    try {
      const created = await createProjectBootstrap(apiBaseUrl, identity, {
        sourceProjectId,
        target,
        categories,
        members: categories.includes("Members") ? Object.values(members) : [],
        conflictPolicy,
      });
      setPreview(created);
      setStep("preview");
      setConfirmed(false);
      setMessage("پیش‌نمایش قطعی آماده است؛ موارد افزودنی، ردشده، متعارض و مسدود را پیش از تأیید بررسی کنید.");
      window.localStorage.removeItem(storageKey);
    } catch (error) {
      setMessage(toUserMessage(error, "ساخت مقصد یا محاسبه پیش‌نمایش انجام نشد."));
    } finally {
      setIsBusy(false);
    }
  }

  async function refreshPreview() {
    if (!preview || !isOnline) return;
    setIsBusy(true);
    setMessage("در حال ارزیابی دوباره تصویر وضعیت مبدأ و مقصد…");
    try {
      const refreshed = await refreshProjectBootstrapPreview(
        apiBaseUrl, identity, preview.planId, preview.planRevision,
      );
      setPreview(refreshed);
      setConfirmed(false);
      setMessage("پیش‌نمایش و چکیده تازه شدند؛ تأیید انسانی دوباره لازم است.");
    } catch (error) {
      setMessage(toUserMessage(error, "تازه‌سازی پیش‌نمایش انجام نشد."));
    } finally {
      setIsBusy(false);
    }
  }

  async function execute() {
    if (!preview || !confirmed || !isOnline) return;
    setIsBusy(true);
    setMessage("در حال اجرای مشارکت‌کننده‌های مجاز و اعتبارسنجی نتیجه…");
    try {
      const completed = await executeProjectBootstrap(apiBaseUrl, identity, preview);
      setResult(completed);
      setStep("result");
      setMessage("راه‌اندازی کامل شد؛ پروژه مقصد عمداً در وضعیت پیش‌نویس باقی مانده است.");
    } catch (error) {
      setMessage(toUserMessage(error, "اجرا متوقف شد؛ پیش‌نمایش تازه بگیرید و تعارض را بررسی کنید."));
    } finally {
      setIsBusy(false);
    }
  }

  async function activate() {
    if (!result || !isOnline) return;
    setIsBusy(true);
    setMessage("در حال اجرای دروازه مستقل آمادگی و فعال‌سازی…");
    try {
      const activated = await activateProjectBootstrap(apiBaseUrl, identity, result);
      setResult(activated);
      setMessage("پروژه مقصد پس از عبور از دروازه آمادگی فعال شد.");
    } catch (error) {
      setMessage(toUserMessage(error, "پروژه هنوز آماده فعال‌سازی نیست؛ تنظیمات یا عضویت‌های لازم را تکمیل کنید."));
    } finally {
      setIsBusy(false);
    }
  }

  function downloadResult() {
    if (!result) return;
    const blob = new Blob([JSON.stringify(result, null, 2)], { type: "application/json" });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = `project-bootstrap-${result.planId}.json`;
    anchor.click();
    URL.revokeObjectURL(url);
  }

  const executionBlocked = Boolean(preview && (
    preview.summary.blocked > 0 ||
    (preview.summary.conflicts > 0 && preview.conflictPolicy === "FailOnConflict")
  ));

  return (
    <main className="project-bootstrap-page">
      <header className="project-bootstrap-header">
        <div>
          <p className="eyebrow">راه‌اندازی کنترل‌شده پروژه</p>
          <h1>ساخت از روی پروژهٔ موجود</h1>
          <p className="muted">انتقال انتخابی تنظیمات و عضویت با پیش‌نمایش قطعی؛ دادهٔ عملیاتی، تاریخچه و شناسه‌های مبدأ کپی نمی‌شوند.</p>
        </div>
        <div className="project-bootstrap-account">
          <Link href="/">بازگشت به پروژه‌ها</Link>
          <SessionBadge />
        </div>
      </header>

      <nav className="bootstrap-steps" aria-label="مراحل ساخت پروژه">
        {(["identity", "scope", "members", "preview", "result"] as const).map((item, index) => (
          <button
            type="button"
            key={item}
            className={step === item ? "active" : ""}
            disabled={item === "preview" ? !preview : item === "result" ? !result : Boolean(preview)}
            onClick={() => setStep(item)}
          >
            <span>{(index + 1).toLocaleString("fa-IR")}</span>{stepLabel(item)}
          </button>
        ))}
      </nav>

      <p className={`bootstrap-message ${isOnline ? "" : "offline"}`} aria-live="polite">
        {!isOnline && <strong>آفلاین — </strong>}{message}
      </p>

      {!preview && (
        <form className="bootstrap-workspace" onSubmit={createPreview}>
          {step === "identity" && (
            <section className="bootstrap-panel">
              <PanelHeading eyebrow="مبدأ و مقصد" title="هویت مستقل پروژه جدید" detail="کد، نام، تاریخ‌ها و مقادیر یکتا همیشه برای مقصد تازه وارد می‌شوند." />
              <div className="bootstrap-form-grid">
                <label>پروژه مبدأ<select required value={sourceProjectId} onChange={(event) => setSourceProjectId(event.target.value)}>
                  <option value="">انتخاب پروژه</option>
                  {projects.map((project) => <option key={project.id} value={project.id}>{project.code} — {project.name}</option>)}
                </select></label>
                <label>نام پروژه مقصد<input required maxLength={200} value={target.name} onChange={(event) => setTarget((current) => ({ ...current, name: event.target.value }))} /></label>
                <label>کد مستقل مقصد<input required minLength={2} maxLength={32} dir="ltr" value={target.code} onChange={(event) => setTarget((current) => ({ ...current, code: event.target.value }))} /></label>
                <label>نوع پروژه<select value={target.projectType} onChange={(event) => setTarget((current) => ({ ...current, projectType: event.target.value as ProjectType }))}>
                  <option value="Building">ساختمانی</option><option value="Industrial">صنعتی</option><option value="Infrastructure">زیرساختی</option><option value="Renovation">بازسازی</option><option value="Landscaping">محوطه‌سازی</option><option value="Mixed">ترکیبی</option>
                </select></label>
                <label>مرحله اجرا<select value={target.executionPhase} onChange={(event) => setTarget((current) => ({ ...current, executionPhase: event.target.value as ProjectExecutionPhase }))}>
                  <option value="PreConstruction">پیش از اجرا</option><option value="ActiveExecution">در حال اجرا</option><option value="OnHold">متوقف</option><option value="Closing">در حال خاتمه</option>
                </select></label>
                <label>کشور<input required minLength={2} maxLength={2} dir="ltr" value={target.countryCode} onChange={(event) => setTarget((current) => ({ ...current, countryCode: event.target.value.toUpperCase() }))} /></label>
                <label>استان یا منطقه<input required maxLength={200} value={target.region} onChange={(event) => setTarget((current) => ({ ...current, region: event.target.value }))} /></label>
                <label>تاریخ شروع مقصد<PersianDateInput required value={target.startDate} onChange={(startDate) => setTarget((current) => ({ ...current, startDate }))} /></label>
                <label>پایان برنامه‌ای مقصد<PersianDateInput required value={target.plannedFinishDate} onChange={(plannedFinishDate) => setTarget((current) => ({ ...current, plannedFinishDate }))} /></label>
                <label>منطقه زمانی<input required dir="ltr" maxLength={100} value={target.timeZone} onChange={(event) => setTarget((current) => ({ ...current, timeZone: event.target.value }))} /></label>
                <label>ارز پایه<input required dir="ltr" minLength={3} maxLength={3} value={target.baseCurrencyCode} onChange={(event) => setTarget((current) => ({ ...current, baseCurrencyCode: event.target.value.toUpperCase() }))} /></label>
                <label className="wide">شرح کوتاه<textarea required maxLength={1000} value={target.shortDescription} onChange={(event) => setTarget((current) => ({ ...current, shortDescription: event.target.value }))} /></label>
                <label className="wide checkbox-row"><input type="checkbox" required checked={target.offlinePolicyAccepted} onChange={(event) => setTarget((current) => ({ ...current, offlinePolicyAccepted: event.target.checked }))} />سیاست کار آفلاین، همگام‌سازی و رسیدگی به تعارض مقصد را به‌صورت مستقل می‌پذیرم.</label>
              </div>
            </section>
          )}

          {step === "scope" && (
            <section className="bootstrap-panel">
              <PanelHeading eyebrow="فهرست مجاز" title="چه چیزهایی بررسی و منتقل شوند؟" detail="هر دسته مشارکت‌کننده و نسخهٔ مستقل دارد. موارد فاقد تنظیم اختصاصی در پیش‌نمایش با وضعیت ردشده نمایش داده می‌شوند." />
              <div className="bootstrap-category-grid">
                {defaultCategories.map((category) => (
                  <label key={category} className={categories.includes(category) ? "selected" : ""}>
                    <input type="checkbox" checked={categories.includes(category)} onChange={(event) => setCategories((current) => event.target.checked ? [...current, category] : current.filter((item) => item !== category))} />
                    <strong>{categoryLabel(category)}</strong><span>{categoryDetail(category)}</span>
                  </label>
                ))}
              </div>
              <label className="bootstrap-policy">سیاست تعارض<select value={conflictPolicy} onChange={(event) => setConflictPolicy(event.target.value as ProjectBootstrapConflictPolicy)}>
                <option value="FailOnConflict">توقف اجرا در هر تعارض</option>
                <option value="SkipConflicts">ردکردن ردیف‌های متعارض و ادامه امن</option>
              </select></label>
            </section>
          )}

          {step === "members" && (
            <section className="bootstrap-panel">
              <PanelHeading eyebrow="ارجاع هویت" title="اعضای انتخاب‌شده و نقش مقصد" detail="حساب کاربر کپی نمی‌شود؛ فقط عضویت جدید به همان حساب موجود ساخته می‌شود." />
              {!categories.includes("Members") ? <p className="bootstrap-note">دسته «اعضای پروژه» انتخاب نشده است؛ هیچ دسترسی‌ای منتقل نمی‌شود.</p> : (
                <div className="bootstrap-member-list">
                  {memberCandidates.length === 0 && <p className="bootstrap-note">عضوی در پروژه مبدأ یافت نشد.</p>}
                  {memberCandidates.map(({ user, membership }) => {
                    const selected = members[user.id];
                    return <article key={user.id} className={selected ? "selected" : ""}>
                      <label><input type="checkbox" checked={Boolean(selected)} onChange={(event) => setMembers((current) => {
                        const next = { ...current };
                        if (event.target.checked) next[user.id] = { userId: user.id, roleCode: membership.roleCode, accessScope: "Project" };
                        else delete next[user.id];
                        return next;
                      })} /><span><strong>{user.displayName}</strong><small>{user.status} · {membership.status}</small></span></label>
                      <select disabled={!selected} value={selected?.roleCode ?? membership.roleCode} onChange={(event) => setMembers((current) => ({ ...current, [user.id]: { userId: user.id, roleCode: event.target.value, accessScope: "Project" } }))}>
                        {roles.map((role) => <option value={role} key={role}>{role}</option>)}
                      </select>
                    </article>;
                  })}
                </div>
              )}
            </section>
          )}

          <div className="bootstrap-actions">
            {step !== "identity" && <button type="button" className="secondary-button" onClick={() => setStep(previousStep(step))}>مرحله قبل</button>}
            {step !== "members" ? <button type="button" onClick={() => setStep(nextStep(step))}>مرحله بعد</button> : (
              <button type="submit" disabled={isBusy || !isOnline || !sourceProjectId || categories.length === 0}>{isBusy ? "در حال محاسبه…" : "ایجاد مقصد پیش‌نویس و نمایش پیش‌نمایش"}</button>
            )}
          </div>
        </form>
      )}

      {step === "preview" && preview && (
        <section className="bootstrap-panel bootstrap-preview">
          <PanelHeading eyebrow="ارزیابی آزمایشی قطعی" title="پیش‌نمایش انتقال" detail={`چکیده: ${preview.previewDigest}`} />
          <Summary summary={preview.summary} />
          <div className="bootstrap-preview-table" role="table" aria-label="نتیجه پیش‌نمایش">
            {preview.items.map((item, index) => <article key={`${item.contributorId}-${item.code}-${index}`} className={`disposition-${item.disposition.toLowerCase()}`}>
              <span>{dispositionLabel(item.disposition)}</span><div><strong>{item.title}</strong><small>{categoryLabel(item.category)} · {item.contributorId}</small><p>{item.detail}</p></div>
            </article>)}
          </div>
          <details className="bootstrap-exclusions"><summary>مواردی که همیشه مستثنا هستند</summary><ul>{preview.alwaysExcluded.map((item) => <li key={item}>{item}</li>)}</ul></details>
          <label className="bootstrap-confirm"><input type="checkbox" checked={confirmed} onChange={(event) => setConfirmed(event.target.checked)} />پیش‌نمایش، موارد مستثنا و سیاست تعارض را بررسی و اجرای همین چکیده را تأیید می‌کنم.</label>
          {executionBlocked && <p className="bootstrap-blocked">پیش‌نمایش دارای مانع اجرایی است؛ سیاست تعارض را نمی‌توان پس از ساخت برنامه تغییر داد و برای اصلاح انتخاب‌ها باید برنامه تازه ساخته شود.</p>}
          <div className="bootstrap-actions">
            <button type="button" className="secondary-button" disabled={isBusy || !isOnline} onClick={() => void refreshPreview()}>محاسبه دوباره پیش‌نمایش</button>
            <button type="button" disabled={isBusy || !isOnline || !confirmed || executionBlocked} onClick={() => void execute()}>{isBusy ? "در حال اجرا…" : "تأیید و اجرای کنترل‌شده"}</button>
          </div>
        </section>
      )}

      {step === "result" && result && (
        <section className="bootstrap-panel bootstrap-result">
          <PanelHeading eyebrow="نتیجه قابل استناد" title={result.status === "Activated" ? "پروژه فعال شد" : "راه‌اندازی کامل شد؛ مقصد پیش‌نویس است"} detail={`پروژه مقصد: ${result.targetProject.code} — ${result.targetProject.name}`} />
          <Summary summary={result.summary} />
          <div className="bootstrap-validation">
            {result.validation.map((item) => <article key={item.code} className={item.passed ? "passed" : "failed"}><strong>{item.passed ? "تأیید شد" : "ناموفق"}</strong><span>{item.detail}</span></article>)}
          </div>
          <div className="bootstrap-actions">
            <button type="button" className="secondary-button" onClick={downloadResult}>دانلود فایل نتیجه</button>
            {result.status === "Completed" && <button type="button" disabled={isBusy || !isOnline} onClick={() => void activate()}>{isBusy ? "در حال ارزیابی…" : "ارزیابی آمادگی و فعال‌سازی مستقل"}</button>}
            {result.status === "Activated" && <Link className="primary-link" href={`/projects/${result.targetProjectId}`}>ورود به مرکز فرمان پروژه</Link>}
          </div>
        </section>
      )}
    </main>
  );
}

function PanelHeading(props: { readonly eyebrow: string; readonly title: string; readonly detail: string }) {
  return <div className="bootstrap-panel-heading"><p className="eyebrow">{props.eyebrow}</p><h2>{props.title}</h2><p className="muted">{props.detail}</p></div>;
}

function Summary({ summary }: { readonly summary: ProjectBootstrapPreviewModel["summary"] }) {
  return <div className="bootstrap-summary"><div><strong>{summary.added.toLocaleString("fa-IR")}</strong><span>افزودنی</span></div><div><strong>{summary.skipped.toLocaleString("fa-IR")}</strong><span>ردشده</span></div><div><strong>{summary.conflicts.toLocaleString("fa-IR")}</strong><span>تعارض</span></div><div><strong>{summary.blocked.toLocaleString("fa-IR")}</strong><span>مسدود</span></div></div>;
}

function stepLabel(step: WizardStep): string {
  return { identity: "مبدأ و مقصد", scope: "اقلام مجاز", members: "اعضا", preview: "پیش‌نمایش", result: "نتیجه" }[step];
}

function nextStep(step: WizardStep): WizardStep {
  return step === "identity" ? "scope" : "members";
}

function previousStep(step: WizardStep): WizardStep {
  return step === "members" ? "scope" : "identity";
}

function categoryLabel(category: ProjectBootstrapCategory): string {
  return {
    BaseSettings: "تنظیمات پایه و ماژول‌ها", Calendar: "تقویم کاری", Locations: "ساختار مکان",
    RoleTemplates: "قالب نقش‌ها", WorkflowTemplates: "قالب گردش کار", FormTemplates: "قالب فرم‌ها",
    ReportTemplates: "قالب گزارش", Lookups: "فهرست‌های مرجع مجاز", Members: "اعضای پروژه",
    NotificationDefaults: "پیش‌فرض اعلان", GroupDefaults: "پیش‌فرض گروه",
  }[category];
}

function categoryDetail(category: ProjectBootstrapCategory): string {
  return {
    BaseSettings: "تنظیمات سازگار؛ حالت فعال در مقصد به نیازمند راه‌اندازی تبدیل می‌شود.", Calendar: "روزهای کاری؛ منطقه زمانی مستقل مقصد حفظ می‌شود.",
    Locations: "فقط مکان‌های فعال با شناسه‌های جدید.", RoleTemplates: "فقط تنظیمات اختصاصی پروژه‌ای نسخه‌دار.",
    WorkflowTemplates: "گردش گزارش و زمان قطع به‌عنوان پیش‌فرض.", FormTemplates: "فقط فرم صریحاً قابل انتقال.",
    ReportTemplates: "پیش‌فرض گزارش؛ بدون اجرای گزارش یا خروجی موجود.", Lookups: "فقط فهرست مرجع اعلام‌شده توسط مالک ماژول.",
    Members: "ارجاع به حساب موجود با نقش و دامنه انتخابی.", NotificationDefaults: "پیش‌فرض جدید؛ بدون تاریخچه اعلان.",
    GroupDefaults: "پیش‌فرض جدید؛ بدون گروه، پیام یا مکالمه موجود.",
  }[category];
}

function dispositionLabel(value: ProjectBootstrapPreviewModel["items"][number]["disposition"]): string {
  return { Added: "افزودنی", Skipped: "ردشده", Conflict: "تعارض", Blocked: "مسدود" }[value];
}
