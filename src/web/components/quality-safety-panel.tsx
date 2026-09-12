"use client";

import { type FormEvent, useCallback, useEffect, useMemo, useState } from "react";
import { PersianDateInput } from "@/components/persian-date-input";
import { toUserMessage } from "@/lib/localization";
import { enqueueOfflineQualitySafetyIntake, syncOfflineQualitySafetyIntakes } from "@/lib/quality-safety-offline";
import {
  beginIntakeTriage,
  captureQualitySafetyIntake,
  configureQualitySafety,
  convertIntake,
  createChecklistTemplate,
  createInspectionTestPlan,
  createPermit,
  createRiskMatrix,
  getQualitySafetyState,
  recordToolboxTalk,
  recordExposureHours,
  requestInspection,
  resolveIntake,
  type ControlArea,
  type InitialSeverity,
  type IntakeKind,
  type IntakeModel,
  type QualitySafetyStateModel,
} from "@/lib/quality-safety";

interface QualitySafetyPanelProps {
  readonly apiBaseUrl: string; readonly tenantId: string; readonly userId: string;
  readonly projectId: string; readonly isOnline: boolean; readonly refreshToken: number;
  readonly onChanged?: () => void;
}

export function QualitySafetyPanel(props: QualitySafetyPanelProps) {
  const identity = useMemo(() => ({ tenantId: props.tenantId, userId: props.userId }), [props.tenantId, props.userId]);
  const [state, setState] = useState<QualitySafetyStateModel | null>(null);
  const [message, setMessage] = useState("در حال دریافت وضعیت مستقل کیفیت و ایمنی…");
  const [busy, setBusy] = useState<string | null>(null);
  const [kind, setKind] = useState<IntakeKind>("QualityObservation");
  const [location, setLocation] = useState("");
  const [facts, setFacts] = useState("");
  const [severity, setSeverity] = useState<InitialSeverity>("Unassessed");
  const [immediateAction, setImmediateAction] = useState("");
  const [evidence, setEvidence] = useState("");
  const [inspectionType, setInspectionType] = useState("");
  const [inspectionLocation, setInspectionLocation] = useState("");
  const [criteria, setCriteria] = useState("");
  const [permitWork, setPermitWork] = useState("");
  const [permitLocation, setPermitLocation] = useState("");
  const [hazards, setHazards] = useState("");
  const [controls, setControls] = useState("");
  const [toolboxTopic, setToolboxTopic] = useState("");
  const [toolboxAttendees, setToolboxAttendees] = useState("");
  const [exposureStart, setExposureStart] = useState("");
  const [exposureEnd, setExposureEnd] = useState("");
  const [exposureHours, setExposureHours] = useState("");
  const [exposureSource, setExposureSource] = useState("");

  const load = useCallback(async () => {
    if (!props.isOnline) { setState(null); setMessage("اطلاعات محرمانه ایمنی روی حافظه مرورگر نگهداری نمی‌شود؛ برای مشاهده به سرور متصل شوید."); return; }
    try {
      let applied = 0;
      let syncFailed = false;
      try { applied = await syncOfflineQualitySafetyIntakes(props.apiBaseUrl, props.projectId); }
      catch { syncFailed = true; }
      const model = await getQualitySafetyState(props.apiBaseUrl, identity, props.projectId);
      setState(model); setMessage(syncFailed
        ? "وضعیت رسمی دریافت شد، اما ثبت‌های محلی هنوز روی دستگاه مانده‌اند و بعداً دوباره همگام می‌شوند."
        : applied > 0
          ? `${applied.toLocaleString("fa-IR")} ثبت اولیه محلی توسط سرور پذیرفته و رسمی شد.`
          : "وضعیت از سوابق رسمی و مجاز سرور دریافت شد.");
    } catch (error) { setState(null); setMessage(toUserMessage(error, "دریافت وضعیت کیفیت و ایمنی ممکن نشد.")); }
  }, [identity, props.apiBaseUrl, props.isOnline, props.projectId]);

  useEffect(() => { const timer = window.setTimeout(() => void load(), 0); return () => window.clearTimeout(timer); }, [load, props.refreshToken]);

  async function run(key: string, action: () => Promise<unknown>, success: string) {
    if (!props.isOnline) { setMessage("این عملیات رسمی فقط هنگام اتصال به سرور انجام می‌شود."); return false; }
    setBusy(key);
    try { await action(); setMessage(success); await load(); props.onChanged?.(); return true; }
    catch (error) { setMessage(toUserMessage(error, "ثبت عملیات ناموفق بود.")); return false; }
    finally { setBusy(null); }
  }

  async function initialize() {
    if (!state) return;
    const qualityEnabled = state.qualityState !== "NotEnabled" && state.qualityState !== "Hidden";
    const hseEnabled = state.hseState !== "NotEnabled" && state.hseState !== "Hidden";
    let qualityMatrixId = state.configuration?.qualityMatrixVersionId ?? null;
    let hseMatrixId = state.configuration?.hseMatrixVersionId ?? null;
    await run("setup", async () => {
      if (qualityEnabled && !qualityMatrixId) qualityMatrixId = (await createRiskMatrix(props.apiBaseUrl, identity, props.projectId,
        { area: "Quality", version: nextVersion(state, "Quality"), title: "ماتریس پایه شدت کیفیت", definitionJson: defaultMatrix(), effectiveFrom: new Date().toISOString() })).id;
      if (hseEnabled && !hseMatrixId) hseMatrixId = (await createRiskMatrix(props.apiBaseUrl, identity, props.projectId,
        { area: "Hse", version: nextVersion(state, "Hse"), title: "ماتریس پایه ریسک ایمنی", definitionJson: defaultMatrix(), effectiveFrom: new Date().toISOString() })).id;
      if (qualityEnabled && state.inspectionTestPlans.length === 0) await createInspectionTestPlan(props.apiBaseUrl, identity, props.projectId,
        { code: "BASE-PLAN", version: 1, title: "برنامه پایه بازرسی و آزمون", stages: ["درخواست", "کنترل آمادگی", "بازرسی", "ثبت نتیجه"], acceptanceCriteria: "معیار اختصاصی هر درخواست پیش از بازرسی ثبت می‌شود", inspectorRole: "مسئول کنترل کیفیت", pointType: "Review", effectiveFrom: new Date().toISOString() });
      if (qualityEnabled && state.checklistTemplates.length === 0) await createChecklistTemplate(props.apiBaseUrl, identity, props.projectId,
        { code: "BASE-CHECKLIST", version: 1, title: "چک‌لیست پایه کنترل کیفیت", items: ["تطابق با آخرین مدرک مصوب", "ثبت نتیجه هر ردیف", "پیوست مدرک راستی‌آزمایی"], effectiveFrom: new Date().toISOString() });
      await configureQualitySafety(props.apiBaseUrl, identity, props.projectId, {
        qualityMode: qualityEnabled ? "FullV1" : "Disabled", hseMode: hseEnabled ? "FullV1" : "Disabled",
        qualityOwnerUserId: qualityEnabled ? props.userId : null, hseOwnerUserId: hseEnabled ? props.userId : null,
        qualityMatrixVersionId: qualityMatrixId, hseMatrixVersionId: hseMatrixId,
        workflowAndSlaDefined: true, templatesDefined: true, evidenceAndClosureRulesDefined: true,
        revision: state.configuration?.revision ?? 0,
      });
    }, "مبنای نسخه‌دار، مسئول و قواعد بستن پرونده ثبت شدند.");
  }

  async function submitIntake(event: FormEvent) {
    event.preventDefault();
    if (!location.trim() || !facts.trim()) { setMessage("محل و شرح واقعیت مشاهده‌شده الزامی است."); return; }
    const safety = !["QualityObservation", "Defect"].includes(kind);
    const payload = {
      kind, observedAt: new Date().toISOString(), location, facts, initialSeverity: severity,
      immediateAction, evidenceReferences: lines(evidence), classification: safety ? "ConfidentialHse" : "RestrictedQuality",
    } as const;
    if (!props.isOnline) {
      try {
        await enqueueOfflineQualitySafetyIntake(props.projectId, payload);
        setMessage("ثبت اولیه روی همین دستگاه ذخیره شد؛ هنوز رسمی نیست و هیچ اعلان بحرانی ارسال نشده است.");
        setFacts(""); setImmediateAction(""); setEvidence(""); props.onChanged?.();
      } catch (error) { setMessage(toUserMessage(error, "ذخیره محلی ثبت اولیه ممکن نشد.")); }
      return;
    }
    const succeeded = await run("intake", () => captureQualitySafetyIntake(props.apiBaseUrl, identity, props.projectId, payload),
      "ثبت سریع انجام شد؛ این رکورد هنوز پرونده رسمی عدم انطباق یا حادثه نیست.");
    if (succeeded) { setFacts(""); setImmediateAction(""); setEvidence(""); }
  }

  async function triage(item: IntakeModel) {
    if (item.status === "Captured") { await run(`triage-${item.id}`, () => beginIntakeTriage(props.apiBaseUrl, identity, props.projectId, item), "بررسی اولیه آغاز شد؛ هنوز تبدیل رسمی انجام نشده است."); return; }
    const conversion = conversionFor(item.kind);
    await run(`convert-${item.id}`, () => convertIntake(props.apiBaseUrl, identity, props.projectId, item,
      conversion, "پس از بررسی انسانی به سابقه رسمی تبدیل شد", item.facts, "نیازمندی مرتبط باید در بررسی رسمی تکمیل شود"),
    "ثبت سریع به پرونده رسمی تبدیل و پیوند آن حفظ شد.");
  }

  async function submitInspection(event: FormEvent) {
    event.preventDefault();
    if (!inspectionType.trim() || !inspectionLocation.trim() || !criteria.trim()) { setMessage("نوع، محل و معیار پذیرش بازرسی الزامی است."); return; }
    const succeeded = await run("inspection", () => requestInspection(props.apiBaseUrl, identity, props.projectId,
      { inspectionType, location: inspectionLocation, acceptanceCriteria: criteria, requestedFor: new Date().toISOString() }),
    "درخواست بازرسی ثبت شد؛ آمادگی و نتیجه باید جداگانه ثبت شوند.");
    if (succeeded) { setInspectionType(""); setInspectionLocation(""); setCriteria(""); }
  }

  async function submitPermit(event: FormEvent) {
    event.preventDefault(); const now = new Date(); const end = new Date(now.getTime() + 8 * 60 * 60 * 1000);
    if (!permitWork.trim() || !permitLocation.trim() || lines(hazards).length === 0 || lines(controls).length === 0) { setMessage("شرح کار، محل، خطرها و کنترل‌ها الزامی است."); return; }
    const succeeded = await run("permit", () => createPermit(props.apiBaseUrl, identity, props.projectId,
      { workDescription: permitWork, location: permitLocation, validFrom: now.toISOString(), validTo: end.toISOString(), hazards: lines(hazards), controls: lines(controls) }),
    "پیش‌نویس مجوز کار ثبت شد؛ تا تأیید و فعال‌سازی، مجوز اجرا نیست.");
    if (succeeded) { setPermitWork(""); setHazards(""); setControls(""); }
  }

  async function submitToolbox(event: FormEvent) {
    event.preventDefault(); if (!toolboxTopic.trim() || !permitLocation.trim() || lines(toolboxAttendees).length === 0 || lines(evidence).length === 0) { setMessage("موضوع، محل، حاضران و مدرک جلسه الزامی است."); return; }
    const succeeded = await run("toolbox", () => recordToolboxTalk(props.apiBaseUrl, identity, props.projectId,
      { topic: toolboxTopic, heldAt: new Date().toISOString(), location: permitLocation, attendees: lines(toolboxAttendees), evidenceReferences: lines(evidence) }),
    "جلسه توجیهی ایمنی همراه فهرست حضور و مدرک ثبت شد.");
    if (succeeded) { setToolboxTopic(""); setToolboxAttendees(""); setEvidence(""); }
  }

  async function submitExposure(event: FormEvent) {
    event.preventDefault(); const hours = Number(exposureHours);
    if (!exposureStart || !exposureEnd || !Number.isFinite(hours) || hours <= 0 || !exposureSource.trim() || lines(evidence).length === 0) { setMessage("دوره، ساعات مثبت، منبع و مدرک ساعات مواجهه الزامی است."); return; }
    const succeeded = await run("exposure", () => recordExposureHours(props.apiBaseUrl, identity, props.projectId,
      { periodStart: exposureStart, periodEnd: exposureEnd, hours, sourceReference: exposureSource, evidenceReferences: lines(evidence) }),
    "ساعات مواجهه با منبع و تأیید ثبت شد؛ نرخ حادثه اکنون مبنای محاسبه دارد.");
    if (succeeded) { setExposureHours(""); setExposureSource(""); setEvidence(""); }
  }

  return (
    <section className="section-block quality-safety-panel" id="quality-safety">
      <div className="section-title"><div><p className="eyebrow">دو کنترل مستقل</p><h2>کیفیت و ایمنی، بهداشت و محیط‌زیست (HSE)</h2></div><span className="section-note">نبود داده ≠ وضعیت سبز</span></div>
      <p className="calculation-note" aria-live="polite">{message}</p>
      <div className="quality-safety-status-grid">
        <StatusCard title="کنترل کیفیت" state={state?.qualityState} count={state?.openQualityCount} unit="پرونده باز" />
        <StatusCard title="ایمنی، بهداشت و محیط‌زیست" state={state?.hseState} count={state?.openHseCount} unit="حادثه قابل مشاهده" />
        <article><span>اقدام اصلاحی سررسیدگذشته</span><strong>{state ? state.overdueCorrectiveActionCount.toLocaleString("fa-IR") : "—"}</strong><small>تکمیل، تأیید یا بسته‌شدن نیست</small></article>
        <article><span>فراوانی رخداد گزارش‌شده</span><strong>{state?.reportedIncidentFrequencyPerTwoHundredThousandHours?.toLocaleString("fa-IR", { maximumFractionDigits: 4 }) ?? "محاسبه نشده"}</strong><small>در هر ۲۰۰ هزار نفر-ساعت؛ فقط با ساعات مواجهه معتبر</small></article>
      </div>
      {state && (state.qualityState === "SetupRequired" || state.hseState === "SetupRequired") && <button type="button" className="primary-button" disabled={busy !== null} onClick={() => void initialize()}>{busy === "setup" ? "در حال راه‌اندازی…" : "راه‌اندازی کنترل‌شده نسخه اول"}</button>}
      {state?.incidentRateUnavailableReason && <p className="muted">{state.incidentRateUnavailableReason} سامانه عدد صفر یا نرخ ساختگی نمایش نمی‌دهد.</p>}

      <div className="quality-safety-workspace">
        <form onSubmit={submitIntake}><h3>ثبت سریع مشاهده</h3><label><span>نوع مشاهده</span><select value={kind} onChange={(event) => setKind(event.target.value as IntakeKind)}>{intakeOptions.map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></label><label><span>محل</span><input value={location} onChange={(event) => setLocation(event.target.value)} /></label><label><span>واقعیت مشاهده‌شده</span><textarea value={facts} onChange={(event) => setFacts(event.target.value)} /></label><label><span>شدت اولیه</span><select value={severity} onChange={(event) => setSeverity(event.target.value as InitialSeverity)}><option value="Unassessed">ارزیابی نشده</option><option value="Low">کم</option><option value="Medium">متوسط</option><option value="High">زیاد</option><option value="Critical">بحرانی</option></select></label><label><span>اقدام فوری</span><input value={immediateAction} onChange={(event) => setImmediateAction(event.target.value)} /></label><label><span>ارجاع مدرک؛ هر خط یک مورد</span><textarea value={evidence} onChange={(event) => setEvidence(event.target.value)} /></label><button type="submit" disabled={busy !== null}>ثبت اولیه غیررسمی</button></form>
        <form onSubmit={submitInspection}><h3>درخواست بازرسی کیفیت</h3><label><span>نوع بازرسی</span><input value={inspectionType} onChange={(event) => setInspectionType(event.target.value)} /></label><label><span>محل</span><input value={inspectionLocation} onChange={(event) => setInspectionLocation(event.target.value)} /></label><label><span>معیار پذیرش</span><textarea value={criteria} onChange={(event) => setCriteria(event.target.value)} /></label><button type="submit" disabled={busy !== null || !isReadyState(state?.qualityState)}>ثبت درخواست مستقل از نتیجه</button></form>
        <form onSubmit={submitPermit}><h3>پیش‌نویس مجوز کار</h3><label><span>شرح کار</span><input value={permitWork} onChange={(event) => setPermitWork(event.target.value)} /></label><label><span>محل کار</span><input value={permitLocation} onChange={(event) => setPermitLocation(event.target.value)} /></label><label><span>خطرها؛ هر خط یک مورد</span><textarea value={hazards} onChange={(event) => setHazards(event.target.value)} /></label><label><span>کنترل‌ها؛ هر خط یک مورد</span><textarea value={controls} onChange={(event) => setControls(event.target.value)} /></label><button type="submit" disabled={busy !== null || !isReadyState(state?.hseState)}>ثبت پیش‌نویس هشت‌ساعته</button></form>
        <form onSubmit={submitToolbox}><h3>جلسه توجیهی ایمنی</h3><label><span>موضوع جلسه</span><input value={toolboxTopic} onChange={(event) => setToolboxTopic(event.target.value)} /></label><label><span>حاضران؛ هر خط یک مورد</span><textarea value={toolboxAttendees} onChange={(event) => setToolboxAttendees(event.target.value)} /></label><p className="muted">محل کار و ارجاع مدرک از فرم‌های همین بخش استفاده می‌شوند.</p><button type="submit" disabled={busy !== null || !isReadyState(state?.hseState)}>ثبت جلسه و حضور</button></form>
        <form onSubmit={submitExposure}><h3>مبنای ساعات مواجهه</h3><label><span>شروع دوره</span><PersianDateInput value={exposureStart} onChange={setExposureStart} required ariaLabel="شروع شمسی دوره مواجهه" /></label><label><span>پایان دوره</span><PersianDateInput value={exposureEnd} onChange={setExposureEnd} required ariaLabel="پایان شمسی دوره مواجهه" /></label><label><span>نفر-ساعت تأییدشده</span><input inputMode="decimal" value={exposureHours} onChange={(event) => setExposureHours(event.target.value)} /></label><label><span>منبع محاسبه</span><input value={exposureSource} onChange={(event) => setExposureSource(event.target.value)} /></label><p className="muted">ارجاع مدرک از فرم ثبت سریع همین بخش خوانده می‌شود.</p><button type="submit" disabled={busy !== null || !isReadyState(state?.hseState)}>ثبت و تأیید مبنا</button></form>
      </div>

      <div className="quality-safety-records"><article><h3>صف بررسی ثبت‌های سریع</h3>{state?.intakes.filter((item) => item.status === "Captured" || item.status === "UnderTriage").length ? state.intakes.filter((item) => item.status === "Captured" || item.status === "UnderTriage").map((item) => <div className="quality-safety-row" key={item.id}><div><strong>{intakeLabel(item.kind)} · {item.location}</strong><small>{item.facts}</small></div><button type="button" disabled={busy !== null} onClick={() => void triage(item)}>{item.status === "Captured" ? "آغاز بررسی" : "تبدیل به پرونده رسمی"}</button><button type="button" disabled={busy !== null || item.status !== "UnderTriage"} onClick={() => void run(`dismiss-${item.id}`, () => resolveIntake(props.apiBaseUrl, identity, props.projectId, item, false, "پس از بررسی انسانی، نیازمند اقدام رسمی نیست"), "ثبت سریع با دلیل تعیین تکلیف شد.")}>رد با دلیل</button></div>) : <p className="muted">موردی در صف بررسی نیست.</p>}</article><article><h3>پرونده‌های رسمی باز</h3><p>عدم انطباق: {(state?.nonConformances.filter((item) => item.status !== "Closed").length ?? 0).toLocaleString("fa-IR")}</p><p>نقص: {(state?.defects.filter((item) => item.status !== "Closed").length ?? 0).toLocaleString("fa-IR")}</p><p>حادثه محرمانه: {state?.openHseCount === null || state?.openHseCount === undefined ? "خارج از سطح دسترسی یا بدون مبنای کافی" : state.openHseCount.toLocaleString("fa-IR")}</p><p>بازرسی در جریان: {(state?.inspections.filter((item) => item.status !== "ResultRecorded").length ?? 0).toLocaleString("fa-IR")}</p></article><article><h3>کنترل‌های ایمنی سبک</h3><p>مجوز کار باز: {(state?.permits.filter((item) => !["Closed", "Cancelled"].includes(item.status)).length ?? 0).toLocaleString("fa-IR")}</p><p>جلسه توجیهی ثبت‌شده: {(state?.toolboxTalks.length ?? 0).toLocaleString("fa-IR")}</p><small>پیش‌نویس آفلاین، مجوز فعال یا اعلان بحرانی محسوب نمی‌شود.</small></article></div>
    </section>
  );
}

function StatusCard({ title, state, count, unit }: { readonly title: string; readonly state?: string; readonly count?: number | null; readonly unit: string }) { return <article><span>{title}</span><strong>{stateLabel(state)}</strong><small>{state === "Available" && count !== null && count !== undefined ? `${count.toLocaleString("fa-IR")} ${unit}` : "هیچ نتیجه سلامت از این وضعیت استنتاج نمی‌شود"}</small></article>; }
function stateLabel(value?: string) { return ({ Hidden: "خارج از سطح دسترسی", NotEnabled: "فعال نشده", Suspended: "تعلیق‌شده", SetupRequired: "نیازمند راه‌اندازی", NoData: "فعال؛ هنوز بدون داده", Available: "فعال و دارای سابقه" } as Record<string, string>)[value ?? ""] ?? "در حال دریافت"; }
function isReadyState(value?: string) { return value === "Available" || value === "NoData"; }
function lines(value: string) { return value.split(/\r?\n/u).map((item) => item.trim()).filter(Boolean); }
function nextVersion(state: QualitySafetyStateModel, area: ControlArea) { return Math.max(0, ...state.matrices.filter((item) => item.area === area).map((item) => item.version)) + 1; }
function defaultMatrix() { return JSON.stringify({ scale: ["کم", "متوسط", "زیاد", "بحرانی"], versionPolicy: "سوابق تاریخی به نسخه زمان ثبت متصل می‌مانند" }); }
function conversionFor(kind: IntakeKind): "QualityObservation" | "Defect" | "NonConformance" | "HseObservation" | "Incident" { if (kind === "Defect") return "Defect"; if (kind === "QualityObservation") return "NonConformance"; if (kind === "IncidentIntake") return "Incident"; return "HseObservation"; }
function intakeLabel(kind: IntakeKind) { return Object.fromEntries(intakeOptions)[kind] ?? "مشاهده"; }
const intakeOptions: readonly (readonly [IntakeKind, string])[] = [["QualityObservation", "مشاهده کیفیت"], ["Defect", "نقص اجرایی"], ["UnsafeCondition", "شرایط ناایمن"], ["UnsafeAct", "رفتار ناایمن"], ["NearMiss", "رویداد نزدیک به حادثه"], ["IncidentIntake", "ثبت اولیه حادثه"], ["EnvironmentalObservation", "مشاهده محیط‌زیستی"], ["SafeObservation", "مشاهده ایمن"], ["PositiveIntervention", "مداخله مثبت"]];
