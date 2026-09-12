"use client";

import { type FormEvent, useCallback, useEffect, useMemo, useState } from "react";
import {
  acknowledgeEscalation, activateRisk, assessRisk, beginDecision, closeRisk, createDecisionRequest,
  createIssue, createRiskMatrix, createSlaRule, evaluateGovernance, getGovernanceState,
  markDecisionImplementing, materializeRisk, proposeRisk, recordDecision, reviewRisk,
  submitDecisionRequest, transitionIssue,
  type Confidentiality, type DecisionRequestModel, type GovernanceStateModel,
  type IssueModel, type RiskModel, type Severity,
} from "@/lib/governance";
import { toUserMessage } from "@/lib/localization";
import { formatPersianDate, formatPersianDateTime, futureProjectDate } from "@/lib/persian-date";

interface GovernancePanelProps {
  readonly apiBaseUrl: string;
  readonly tenantId: string;
  readonly userId: string;
  readonly projectId: string;
  readonly isOnline: boolean;
  readonly refreshToken: number;
  readonly onChanged: () => void;
}

export function GovernancePanel(props: GovernancePanelProps) {
  const identity = useMemo(() => ({ tenantId: props.tenantId, userId: props.userId }), [props.tenantId, props.userId]);
  const [state, setState] = useState<GovernanceStateModel | null>(null);
  const [message, setMessage] = useState("در حال دریافت دفتر کنترل مدیریتی…");
  const [busy, setBusy] = useState<string | null>(null);
  const [evidence, setEvidence] = useState("");
  const [confidentiality, setConfidentiality] = useState<Confidentiality>("GeneralProject");
  const [riskCause, setRiskCause] = useState("");
  const [riskEvent, setRiskEvent] = useState("");
  const [riskImpact, setRiskImpact] = useState("");
  const [issueTitle, setIssueTitle] = useState("");
  const [issueFact, setIssueFact] = useState("");
  const [severity, setSeverity] = useState<Severity>("Medium");
  const [decisionQuestion, setDecisionQuestion] = useState("");
  const [decisionWhy, setDecisionWhy] = useState("");
  const [decisionFacts, setDecisionFacts] = useState("");
  const [decisionOptions, setDecisionOptions] = useState("");
  const [ownerUserId, setOwnerUserId] = useState(props.userId);
  const [authorityUserId, setAuthorityUserId] = useState(props.userId);
  const [selectedRisk, setSelectedRisk] = useState<RiskModel | null>(null);
  const [materializingRisk, setMaterializingRisk] = useState<RiskModel | null>(null);
  const [riskResponse, setRiskResponse] = useState("");
  const [riskWarning, setRiskWarning] = useState("");
  const [selectedDecision, setSelectedDecision] = useState<DecisionRequestModel | null>(null);
  const [selectedOption, setSelectedOption] = useState("");
  const [decisionRationale, setDecisionRationale] = useState("");
  const [resolutionNote, setResolutionNote] = useState("");
  const [verificationEvidence, setVerificationEvidence] = useState("");
  const futureDate = useMemo(() => futureProjectDate(7), []);

  const load = useCallback(async () => {
    if (!props.isOnline) {
      setMessage("دفتر ریسک و تصمیم شامل اطلاعات حساس است و روی مرورگر ذخیره نمی‌شود؛ برای مشاهده به سرور متصل شوید.");
      return;
    }
    try {
      const next = await getGovernanceState(props.apiBaseUrl, identity, props.projectId);
      setState(next);
      setOwnerUserId((current) => next.assignablePeople.some((item) => item.userId === current)
        ? current : next.assignablePeople[0]?.userId ?? "");
      setAuthorityUserId((current) => next.assignablePeople.some((item) => item.userId === current && item.canDecide)
        ? current : next.assignablePeople.find((item) => item.canDecide)?.userId ?? "");
      setMessage(next.setupState === "SetupRequired"
        ? "برای امتیازدهی و پایش مهلت‌ها، ماتریس ریسک و قواعد توافق سطح خدمت باید نسخه‌گذاری شوند."
        : "دفتر کنترل مدیریتی از سوابق رسمی و قابل ردیابی دریافت شد.");
    } catch (error) {
      setMessage(toUserMessage(error, "دفتر کنترل مدیریتی دریافت نشد."));
    }
  }, [identity, props.apiBaseUrl, props.isOnline, props.projectId]);

  useEffect(() => { const id = window.setTimeout(() => void load(), 0); return () => window.clearTimeout(id); }, [load, props.refreshToken]);

  async function run(key: string, command: () => Promise<unknown>, success: string) {
    if (!props.isOnline) { setMessage("ثبت و تصمیم رسمی فقط هنگام اتصال به سرور مجاز است."); return false; }
    setBusy(key);
    try { await command(); setMessage(success); await load(); props.onChanged(); return true; }
    catch (error) {
      await load().catch(() => undefined);
      setMessage(toUserMessage(error, "عملیات کنترل مدیریتی انجام نشد."));
      return false;
    }
    finally { setBusy(null); }
  }

  async function setup() {
    const createdAt = new Date().toISOString();
    const ok = await run("setup", async () => {
      if (!state?.riskMatrices.some((item) => item.effectiveFrom <= createdAt)) await createRiskMatrix(props.apiBaseUrl, identity, props.projectId,
        { title: "ماتریس پنج‌درپنج پروژه", lowMaximum: 5, moderateMaximum: 10, highMaximum: 15, effectiveFrom: createdAt });
      const activeRules = state?.slaRules.filter((item) => item.effectiveFrom <= createdAt) ?? [];
      if (!activeRules.some((item) => item.entityType === "Issue"))
        await createSlaRule(props.apiBaseUrl, identity, props.projectId, { title: "پاسخ به مسئله مهم", entityType: "Issue", severity: null, duration: 72, durationUnit: "ElapsedHours", warningLeadMinutes: 720, escalationDelayMinutes: 240, escalationRecipientUserId: props.userId, effectiveFrom: createdAt });
      if (!activeRules.some((item) => item.entityType === "Risk"))
        await createSlaRule(props.apiBaseUrl, identity, props.projectId, { title: "بازبینی ریسک", entityType: "Risk", severity: null, duration: 120, durationUnit: "ElapsedHours", warningLeadMinutes: 1440, escalationDelayMinutes: 720, escalationRecipientUserId: props.userId, effectiveFrom: createdAt });
      if (!activeRules.some((item) => item.entityType === "DecisionRequest"))
        await createSlaRule(props.apiBaseUrl, identity, props.projectId, { title: "پاسخ به درخواست تصمیم", entityType: "DecisionRequest", severity: null, duration: 48, durationUnit: "ElapsedHours", warningLeadMinutes: 360, escalationDelayMinutes: 120, escalationRecipientUserId: props.userId, effectiveFrom: createdAt });
    }, "ماتریس و قواعد پایه نسخه‌گذاری شدند؛ تغییر بعدی نسخه تازه می‌سازد و سوابق قبلی را بازنویسی نمی‌کند.");
    if (ok) setEvidence("");
  }

  async function submitRisk(event: FormEvent) {
    event.preventDefault();
    if (![riskCause, riskEvent, riskImpact].every((x) => x.trim()) || lines(evidence).length === 0) { setMessage("علت، رویداد نامطمئن، پیامد و حداقل یک ارجاع مدرک الزامی است."); return; }
    const ok = await run("risk", () => proposeRisk(props.apiBaseUrl, identity, props.projectId, {
      type: "Threat", cause: riskCause, uncertainEvent: riskEvent, impactStatement: riskImpact,
      category: "مدیریت پروژه", ownerUserId, confidentiality, sourceModule: "ManualGovernance",
      sourceEntityType: "VerifiedObservation", sourceSnapshot: `واقعیت ثبت‌شده: ${riskCause}`, evidenceReferences: lines(evidence),
    }), "ریسک به‌صورت پیشنهادی ثبت شد؛ تا ارزیابی انسانی، امتیاز ندارد.");
    if (ok) { setRiskCause(""); setRiskEvent(""); setRiskImpact(""); }
  }

  async function submitIssue(event: FormEvent) {
    event.preventDefault();
    if (!issueTitle.trim() || !issueFact.trim() || !ownerUserId || lines(evidence).length === 0) { setMessage("عنوان، واقعیت رخ‌داده، مالک و حداقل یک ارجاع مدرک الزامی است."); return; }
    const ok = await run("issue", () => createIssue(props.apiBaseUrl, identity, props.projectId, {
      title: issueTitle, observedFact: issueFact, category: "مدیریت پروژه", severity, urgency: severity === "Critical" ? "Immediate" : "Soon",
      ownerUserId, targetResolutionDate: futureDate, sourceModule: "ManualGovernance",
      sourceEntityType: "VerifiedObservation", sourceSnapshot: `واقعیت ثبت‌شده: ${issueFact}`, evidenceReferences: lines(evidence), confidentiality,
    }), "مسئلهٔ واقع‌شده با مالک، تاریخ هدف و مدرک ثبت شد.");
    if (ok) { setIssueTitle(""); setIssueFact(""); }
  }

  async function submitDecision(event: FormEvent) {
    event.preventDefault(); const facts = lines(decisionFacts); const options = lines(decisionOptions);
    if (!decisionQuestion.trim() || !decisionWhy.trim() || !authorityUserId || facts.length === 0 || options.length < 2 || lines(evidence).length === 0) { setMessage("پرسش، فوریت، مرجع مجاز، واقعیت، دست‌کم دو گزینه و مدرک الزامی است."); return; }
    const ok = await run("decision", () => createDecisionRequest(props.apiBaseUrl, identity, props.projectId, {
      question: decisionQuestion, whyNow: decisionWhy, requiredBy: futureDate, authorityUserId,
      knownFacts: facts, assumptions: [], predictions: [], options, constraints: [], evidenceReferences: lines(evidence),
      confidentiality, sourceModule: "ManualGovernance", sourceEntityType: "DecisionContext", sourceSnapshot: `مبنای درخواست: ${facts[0]}`,
    }), "پیش‌نویس درخواست تصمیم ثبت شد؛ هنوز تصمیم رسمی نیست.");
    if (ok) { setDecisionQuestion(""); setDecisionWhy(""); setDecisionFacts(""); setDecisionOptions(""); }
  }

  async function assessSelectedRisk(event: FormEvent) {
    event.preventDefault(); const matrix = state?.riskMatrices[0];
    if (!selectedRisk || !matrix || !riskResponse.trim() || ((selectedRisk.status === "Proposed" || selectedRisk.status === "Reopened") && !riskWarning.trim())) { setMessage("ریسک، ماتریس، پاسخ و برای ارزیابی اولیه شاخص هشدار زودهنگام الزامی است."); return; }
    const initial = selectedRisk.status === "Proposed" || selectedRisk.status === "Reopened";
    const ok = await run(`assess-${selectedRisk.id}`, () => initial
      ? assessRisk(props.apiBaseUrl, identity, props.projectId, selectedRisk,
          { matrixVersionId: matrix.id, probability: "Possible", impact: severity === "Critical" ? "Severe" : severity === "High" ? "Major" : "Moderate", responseStrategy: "Mitigate", responsePlan: riskResponse, earlyWarningIndicator: riskWarning, reviewDate: futureDate })
      : reviewRisk(props.apiBaseUrl, identity, props.projectId, selectedRisk,
          { residualProbability: "Possible", residualImpact: severity === "Critical" ? "Severe" : severity === "High" ? "Major" : "Moderate", responsePlan: riskResponse, nextReviewDate: futureDate }),
    initial ? "ریسک با نسخه مشخص ماتریس ارزیابی شد؛ امتیاز آن سابقه‌پذیر است." : "بازبینی ریسک ثبت شد و امتیاز ذاتی بازنویسی نشد.");
    if (ok) { setSelectedRisk(null); setRiskResponse(""); setRiskWarning(""); }
  }

  async function closeSelectedRisk() {
    if (!selectedRisk || !riskResponse.trim() || lines(evidence).length === 0) { setMessage("برای بستن ریسک، دلیل و حداقل یک مدرک الزامی است."); return; }
    const ok = await run(`close-${selectedRisk.id}`, () => closeRisk(props.apiBaseUrl, identity,
      props.projectId, selectedRisk, riskResponse, lines(evidence)), "ریسک با دلیل و مدرک بسته شد؛ سابقه ارزیابی باقی ماند.");
    if (ok) { setSelectedRisk(null); setRiskResponse(""); }
  }

  async function submitMaterialization(event: FormEvent) {
    event.preventDefault();
    if (!materializingRisk || !issueTitle.trim() || !issueFact.trim() || lines(evidence).length === 0) { setMessage("برای تحقق ریسک، عنوان مسئله، واقعیت رخ‌داده و مدرک الزامی است."); return; }
    const ok = await run(`materialize-${materializingRisk.id}`, () => materializeRisk(props.apiBaseUrl,
      identity, props.projectId, materializingRisk, { title: issueTitle, observedFact: issueFact,
        category: "مدیریت پروژه", severity, urgency: severity === "Critical" ? "Immediate" : "Soon",
        ownerUserId, targetResolutionDate: futureDate, evidenceReferences: lines(evidence) }),
    "تحقق ریسک ثبت و یک مسئله رسمیِ پیوندخورده ساخته شد؛ خود سابقه ریسک بازنویسی نشد.");
    if (ok) { setMaterializingRisk(null); setIssueTitle(""); setIssueFact(""); }
  }

  async function recordSelectedDecision(event: FormEvent) {
    event.preventDefault();
    if (!selectedDecision || !selectedOption || !decisionRationale.trim()) { setMessage("درخواست، گزینه منتخب و توجیه تصمیم الزامی است."); return; }
    const ok = await run(`record-${selectedDecision.id}`, () => recordDecision(props.apiBaseUrl, identity, props.projectId,
      selectedDecision, selectedOption, decisionRationale), "تصمیم رسمی ثبت شد و درخواست تصمیم به همان رکورد پیوند خورد.");
    if (ok) { setSelectedDecision(null); setSelectedOption(""); setDecisionRationale(""); }
  }

  async function advanceIssue(item: IssueModel) {
    const target = nextIssueStatus(item.status); if (!target) return;
    if (target === "Resolved" && !resolutionNote.trim()) { setMessage("برای اعلام رفع، شرح نتیجه الزامی است."); return; }
    if (target === "Closed" && lines(verificationEvidence).length === 0) { setMessage("بستن مسئله پس از رفع، به مدرک راستی‌آزمایی مستقل نیاز دارد."); return; }
    await run(`issue-${item.id}`, () => transitionIssue(props.apiBaseUrl, identity, props.projectId, item,
      target, target === "Resolved" || target === "Closed" ? resolutionNote : undefined,
      target === "Closed" ? lines(verificationEvidence) : []), target === "Closed"
      ? "مسئله پس از راستی‌آزمایی بسته شد." : "وضعیت مسئله بدون حذف سابقه به مرحله بعد رفت.");
  }

  return (
    <section className="section-block governance-panel" id="governance">
      <div className="section-title"><div><p className="eyebrow">کنترل مبتنی بر شواهد</p><h2>ریسک، مسئله، تصمیم و توافق سطح خدمت</h2></div><span className="section-note">بدون امتیاز سلامت کلی</span></div>
      <p className="calculation-note" aria-live="polite">{message}</p>
      <div className="governance-summary">
        <Summary label="مسئله باز" value={state?.counts.openIssues} />
        <Summary label="ریسک باز" value={state?.counts.activeRisks} detail={state ? `${state.counts.criticalRisks.toLocaleString("fa-IR")} بحرانی` : undefined} />
        <Summary label="تصمیم معطل" value={state?.counts.pendingDecisions} />
        <Summary label="اعلام هشدار تأییدنشده" value={state?.counts.unacknowledgedEscalations} />
        <Summary label="مهلت سررسیدگذشته" value={state?.outlook.overdue} detail={state?.outlook.state === "InsufficientData" ? "داده کافی نیست" : "خروجی قطعی مهلت‌ها"} />
      </div>
      {state?.setupState === "SetupRequired" && <div className="governance-callout"><p>ماتریس ریسک یا قواعد توافق سطح خدمت هنوز تعریف نشده‌اند. نبود تنظیمات به معنی ریسک کم یا عملکرد مناسب نیست.</p><button type="button" disabled={busy !== null || !props.isOnline} onClick={() => void setup()}>{busy === "setup" ? "در حال نسخه‌گذاری…" : "ساخت تنظیمات پایه نسخه اول"}</button></div>}

      <details className="governance-editor"><summary>ثبت سابقه مدیریتی جدید</summary><div className="governance-forms">
        <form onSubmit={submitRisk}><h3>ریسک آینده</h3><label><span>علت</span><input value={riskCause} onChange={(e) => setRiskCause(e.target.value)} /></label><label><span>رویداد نامطمئن</span><input value={riskEvent} onChange={(e) => setRiskEvent(e.target.value)} /></label><label><span>پیامد احتمالی</span><textarea value={riskImpact} onChange={(e) => setRiskImpact(e.target.value)} /></label><PersonSelect label="مالک ریسک" people={state?.assignablePeople ?? []} value={ownerUserId} onChange={setOwnerUserId} /><button disabled={busy !== null || !ownerUserId}>ثبت ریسک پیشنهادی</button></form>
        <form onSubmit={submitIssue}><h3>مسئله رخ‌داده</h3><label><span>عنوان</span><input value={issueTitle} onChange={(e) => setIssueTitle(e.target.value)} /></label><label><span>واقعیت مشاهده‌شده</span><textarea value={issueFact} onChange={(e) => setIssueFact(e.target.value)} /></label><label><span>شدت</span><select value={severity} onChange={(e) => setSeverity(e.target.value as Severity)}><option value="Low">کم</option><option value="Medium">متوسط</option><option value="High">زیاد</option><option value="Critical">بحرانی</option></select></label><PersonSelect label="مالک مسئله" people={state?.assignablePeople ?? []} value={ownerUserId} onChange={setOwnerUserId} /><button disabled={busy !== null || !ownerUserId}>ثبت مسئله رسمی</button></form>
        <form onSubmit={submitDecision}><h3>درخواست تصمیم</h3><label><span>پرسش تصمیم</span><input value={decisionQuestion} onChange={(e) => setDecisionQuestion(e.target.value)} /></label><label><span>چرا اکنون؟</span><textarea value={decisionWhy} onChange={(e) => setDecisionWhy(e.target.value)} /></label><label><span>واقعیت‌ها؛ هر خط یک مورد</span><textarea value={decisionFacts} onChange={(e) => setDecisionFacts(e.target.value)} /></label><label><span>گزینه‌ها؛ دست‌کم دو خط</span><textarea value={decisionOptions} onChange={(e) => setDecisionOptions(e.target.value)} /></label><PersonSelect label="مرجع مجاز تصمیم" people={(state?.assignablePeople ?? []).filter((item) => item.canDecide)} value={authorityUserId} onChange={setAuthorityUserId} /><button disabled={busy !== null || !authorityUserId}>ثبت پیش‌نویس درخواست</button></form>
      </div><div className="governance-shared-fields"><label><span>ارجاع مدرک؛ هر خط یک مورد</span><textarea value={evidence} onChange={(e) => setEvidence(e.target.value)} /></label><label><span>سطح محرمانگی</span><select value={confidentiality} onChange={(e) => setConfidentiality(e.target.value as Confidentiality)}><option value="GeneralProject">عمومی پروژه</option><option value="RestrictedManagement">محدود مدیریتی</option><option value="ConfidentialHse">محرمانه ایمنی، بهداشت و محیط‌زیست</option><option value="CommercialSensitive">حساس تجاری</option></select></label></div></details>

      {selectedRisk && <form className="governance-inline-form" onSubmit={assessSelectedRisk}><h3>{selectedRisk.status === "Proposed" || selectedRisk.status === "Reopened" ? "ارزیابی" : "بازبینی"} {selectedRisk.number}</h3><label><span>برنامه پاسخ یا دلیل بستن</span><textarea value={riskResponse} onChange={(e) => setRiskResponse(e.target.value)} /></label><label><span>شاخص هشدار زودهنگام</span><input value={riskWarning} onChange={(e) => setRiskWarning(e.target.value)} /></label><button disabled={busy !== null}>{selectedRisk.status === "Proposed" || selectedRisk.status === "Reopened" ? "ثبت ارزیابی با ماتریس جاری" : "ثبت بازبینی با همان نسخه ماتریس"}</button>{selectedRisk.status === "Active" || selectedRisk.status === "Monitoring" ? <button type="button" disabled={busy !== null} onClick={() => void closeSelectedRisk()}>بستن با دلیل و مدرک</button> : null}<button type="button" onClick={() => setSelectedRisk(null)}>انصراف</button></form>}
      {materializingRisk && <form className="governance-inline-form" onSubmit={submitMaterialization}><h3>ثبت تحقق {materializingRisk.number}</h3><label><span>عنوان مسئله</span><input value={issueTitle} onChange={(e) => setIssueTitle(e.target.value)} /></label><label><span>واقعیت رخ‌داده</span><textarea value={issueFact} onChange={(e) => setIssueFact(e.target.value)} /></label><button disabled={busy !== null}>ساخت مسئله پیوندخورده</button><button type="button" onClick={() => setMaterializingRisk(null)}>انصراف</button></form>}
      {selectedDecision && <form className="governance-inline-form" onSubmit={recordSelectedDecision}><h3>ثبت تصمیم برای {selectedDecision.number}</h3><label><span>گزینه منتخب</span><select value={selectedOption} onChange={(e) => setSelectedOption(e.target.value)}><option value="">انتخاب کنید</option>{selectedDecision.options.map((option) => <option key={option} value={option}>{option}</option>)}</select></label><label><span>توجیه تصمیم</span><textarea value={decisionRationale} onChange={(e) => setDecisionRationale(e.target.value)} /></label><button disabled={busy !== null}>ثبت تصمیم رسمی</button><button type="button" onClick={() => setSelectedDecision(null)}>انصراف</button></form>}

      <div className="governance-record-grid">
        <article><h3>دفتر ریسک</h3>{state?.risks.length ? state.risks.map((item) => <div className="governance-row" key={item.id}><div><strong>{item.number} · {item.uncertainEvent}</strong><small>{riskStatusLabel(item.status)} · {item.ownerDisplayName}{item.inherentRating ? ` · ${ratingLabel(item.inherentRating)}` : " · ارزیابی نشده"}</small></div><div>{item.status === "Proposed" || item.status === "Reopened" ? <button type="button" disabled={busy !== null || !state.riskMatrices.length} onClick={() => setSelectedRisk(item)}>ارزیابی</button> : null}{item.status === "Assessed" ? <button type="button" disabled={busy !== null} onClick={() => void run(`activate-${item.id}`, () => activateRisk(props.apiBaseUrl, identity, props.projectId, item), "ریسک ارزیابی‌شده فعال شد.")}>فعال‌سازی</button> : null}{item.status === "Active" || item.status === "Monitoring" ? <><button type="button" disabled={busy !== null} onClick={() => setSelectedRisk(item)}>بازبینی</button><button type="button" disabled={busy !== null} onClick={() => setMaterializingRisk(item)}>ثبت تحقق</button></> : null}</div></div>) : <p className="muted">ریسکی ثبت نشده است.</p>}</article>
        <article><h3>دفتر مسئله</h3><div className="governance-inline-inputs"><input aria-label="شرح رفع مسئله" placeholder="شرح رفع" value={resolutionNote} onChange={(e) => setResolutionNote(e.target.value)} /><input aria-label="مدرک راستی‌آزمایی" placeholder="ارجاع مدرک راستی‌آزمایی" value={verificationEvidence} onChange={(e) => setVerificationEvidence(e.target.value)} /></div>{state?.issues.length ? state.issues.map((item) => <div className="governance-row" key={item.id}><div><strong>{item.number} · {item.title}</strong><small>{issueStatusLabel(item.status)} · {item.ownerDisplayName} · هدف {faDate(item.targetResolutionDate)}</small></div>{nextIssueStatus(item.status) && <button type="button" disabled={busy !== null} onClick={() => void advanceIssue(item)}>{issueActionLabel(item.status)}</button>}</div>) : <p className="muted">مسئله‌ای ثبت نشده است.</p>}</article>
        <article><h3>دفتر تصمیم</h3>{state?.decisionRequests.length ? state.decisionRequests.map((item) => <div className="governance-row" key={item.id}><div><strong>{item.number} · {item.question}</strong><small>{decisionStatusLabel(item.status)} · مرجع: {item.authorityDisplayName} · موعد {faDate(item.requiredBy)}</small></div><div>{item.status === "Draft" || item.status === "MoreInformationRequired" ? <button type="button" disabled={busy !== null} onClick={() => void run(`submit-${item.id}`, () => submitDecisionRequest(props.apiBaseUrl, identity, props.projectId, item), "درخواست برای تصمیم ارسال شد.")}>ارسال برای تصمیم</button> : null}{item.status === "ReadyForDecision" ? <button type="button" disabled={busy !== null} onClick={() => void run(`begin-${item.id}`, () => beginDecision(props.apiBaseUrl, identity, props.projectId, item), "بررسی رسمی تصمیم آغاز شد.")}>آغاز تصمیم</button> : null}{item.status === "InDecision" ? <button type="button" disabled={busy !== null} onClick={() => { setSelectedDecision(item); setSelectedOption(item.options[0] ?? ""); }}>ثبت تصمیم</button> : null}{item.status === "Decided" ? <button type="button" disabled={busy !== null} onClick={() => void run(`implement-${item.id}`, () => markDecisionImplementing(props.apiBaseUrl, identity, props.projectId, item), "اجرای تصمیم به‌صورت صریح آغاز شد.")}>آغاز اجرا</button> : null}</div></div>) : <p className="muted">درخواست تصمیمی ثبت نشده است.</p>}</article>
      </div>

      <div className="governance-alerts"><div className="section-title"><div><h3>مهلت‌ها و اعلام هشدار</h3><p className="muted">یادآوری با اعلام هشدار متفاوت است؛ تأیید هشدار به معنی رفع منبع نیست.</p></div><button type="button" disabled={busy !== null || !props.isOnline} onClick={() => void run("evaluate", () => evaluateGovernance(props.apiBaseUrl, identity, props.projectId), "مهلت‌ها به‌صورت قطعی ارزیابی و هشدارهای تکراری تجمیع شدند.")}>ارزیابی مهلت‌ها</button></div>{state?.alerts.filter((x) => x.signal !== "OnTrack").map((item) => <div className={`governance-row signal-${item.signal.toLowerCase()}`} key={`${item.entityId}-${item.deadlineKind}`}><div><strong>{item.entityNumber} · {deadlineLabel(item.deadlineKind)}</strong><small>{deadlineSignalLabel(item.signal)} · {faDateTime(item.dueAt)}</small></div></div>)}{state?.escalations.filter((x) => x.status !== "ClosedBySourceResolution").map((item) => <div className="governance-row" key={item.id}><div><strong>{item.entityNumber} · {escalationReasonLabel(item.reason)}</strong><small>{item.status === "Acknowledged" ? "دریافت تأیید شده؛ منبع هنوز باز است" : `گیرنده: ${item.recipientDisplayName}`}</small></div>{item.status === "Open" && <button type="button" disabled={busy !== null} onClick={() => void run(`ack-${item.id}`, () => acknowledgeEscalation(props.apiBaseUrl, identity, props.projectId, item, "دریافت و در حال پیگیری"), "دریافت هشدار تأیید شد؛ وضعیت منبع تغییری نکرد.")}>تأیید دریافت</button>}</div>)}</div>
      <p className="governance-boundary">این بخش فقط چشم‌انداز قطعی هفت‌روزهٔ مهلت‌های ثبت‌شده را نشان می‌دهد. پیش‌بینی احتمالاتی بدون دادهٔ تاریخی کافی تولید نمی‌شود و تحلیل هوش مصنوعی نیز اختیار ثبت تصمیم، شدت، مالک یا بستن پرونده را ندارد.</p>
    </section>
  );
}

function Summary({ label, value, detail }: { readonly label: string; readonly value?: number; readonly detail?: string }) { return <article><span>{label}</span><strong>{value === undefined ? "—" : value.toLocaleString("fa-IR")}</strong>{detail && <small>{detail}</small>}</article>; }
function PersonSelect({ label, people, value, onChange }: { readonly label: string; readonly people: readonly { readonly userId: string; readonly displayName: string }[]; readonly value: string; readonly onChange: (value: string) => void }) { return <label><span>{label}</span><select value={value} onChange={(event) => onChange(event.target.value)}><option value="">انتخاب کنید</option>{people.map((person) => <option key={person.userId} value={person.userId}>{person.displayName}</option>)}</select></label>; }
function lines(value: string) { return value.split(/\r?\n/u).map((item) => item.trim()).filter(Boolean); }
function faDate(value: string) { return formatPersianDate(value); }
function faDateTime(value: string) { return formatPersianDateTime(value); }
function riskStatusLabel(value: string) { return ({ Proposed: "پیشنهادی", Assessed: "ارزیابی‌شده", Active: "فعال", Monitoring: "در پایش", Materialized: "محقق‌شده و متصل به مسئله", Expired: "منقضی", Closed: "بسته", Reopened: "بازگشایی‌شده" } as Record<string, string>)[value] ?? "نامشخص"; }
function issueStatusLabel(value: string) { return ({ Open: "باز", UnderAssessment: "در ارزیابی", ResponseInProgress: "اقدام در جریان", PendingVerification: "در انتظار راستی‌آزمایی", Resolved: "رفع‌شده؛ در انتظار بستن", Closed: "بسته", Reopened: "بازگشایی‌شده", NotAnIssue: "مسئله نیست", Void: "باطل" } as Record<string, string>)[value] ?? "نامشخص"; }
function decisionStatusLabel(value: string) { return ({ Draft: "پیش‌نویس", ReadyForDecision: "آماده تصمیم", InDecision: "در حال تصمیم", MoreInformationRequired: "نیازمند اطلاعات بیشتر", Decided: "تصمیم‌گیری‌شده", Implementing: "در حال اجرا", EffectReviewed: "اثر بازبینی‌شده", Closed: "بسته", Withdrawn: "پس‌گرفته‌شده" } as Record<string, string>)[value] ?? "نامشخص"; }
function ratingLabel(value: string) { return ({ Low: "ریسک کم", Moderate: "ریسک متوسط", High: "ریسک زیاد", Critical: "ریسک بحرانی" } as Record<string, string>)[value] ?? "بدون رتبه"; }
function nextIssueStatus(value: string): IssueModel["status"] | null { return ({ Open: "UnderAssessment", UnderAssessment: "ResponseInProgress", ResponseInProgress: "Resolved", PendingVerification: "Resolved", Resolved: "Closed", Reopened: "UnderAssessment" } as Partial<Record<string, IssueModel["status"]>>)[value] ?? null; }
function issueActionLabel(value: string) { return ({ Open: "آغاز ارزیابی", UnderAssessment: "آغاز اقدام", ResponseInProgress: "اعلام رفع", PendingVerification: "اعلام رفع", Resolved: "بستن پس از راستی‌آزمایی", Reopened: "ارزیابی مجدد" } as Record<string, string>)[value] ?? "ادامه"; }
function deadlineLabel(value: string) { return ({ ServiceLevel: "مهلت توافق سطح خدمت", TargetResolution: "تاریخ هدف رفع", RiskReview: "تاریخ بازبینی ریسک", RequiredDecision: "موعد تصمیم" } as Record<string, string>)[value] ?? "مهلت"; }
function deadlineSignalLabel(value: string) { return ({ DueSoon: "نزدیک سررسید", Overdue: "سررسیدگذشته", EscalationDue: "موعد اعلام هشدار", OnTrack: "در محدوده مهلت" } as Record<string, string>)[value] ?? "نامشخص"; }
function escalationReasonLabel(value: string) { return ({ Overdue: "سررسید گذشته", CriticalSeverity: "ریسک بحرانی", ReviewOverdue: "بازبینی ریسک عقب‌افتاده", DecisionOverdue: "تصمیم عقب‌افتاده", Blocked: "اقدام مسدود", DueSoon: "نزدیک سررسید" } as Record<string, string>)[value] ?? "نیازمند توجه"; }
