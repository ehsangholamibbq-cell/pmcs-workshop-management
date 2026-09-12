"use client";

import { type FormEvent, type ReactNode, useCallback, useEffect, useMemo, useState } from "react";
import { PersianDateInput } from "@/components/persian-date-input";
import {
  createDocumentRevision,
  createRfi,
  createSubmittal,
  createTechnicalDocument,
  createTransmittal,
  getTechnicalOfficeState,
  recordRfiResponse,
  reviewSubmittal,
  transitionDocumentRevision,
  transitionRfi,
  transitionSubmittal,
  transitionTransmittal,
  type DocumentRevisionModel,
  type RfiResponseClassification,
  type RfiModel,
  type SubmittalModel,
  type SubmittalReviewOutcome,
  type TechnicalDocumentType,
  type TechnicalOfficeStateModel,
  type TechnicalSubmittalType,
  type TransmittalModel,
} from "@/lib/technical-office";
import { scopedStorageKey } from "@/lib/field-database";
import { toUserMessage } from "@/lib/localization";
import { formatPersianDate, todayIsoInProjectTimeZone } from "@/lib/persian-date";

interface TechnicalOfficePanelProps {
  readonly apiBaseUrl: string;
  readonly tenantId: string;
  readonly userId: string;
  readonly projectId: string;
  readonly isOnline: boolean;
  readonly refreshToken: number;
  readonly onChanged?: () => void;
}

const initialDate = todayIsoInProjectTimeZone();
const emptyState: TechnicalOfficeStateModel = {
  documents: [], documentRevisions: [], transmittals: [], rfis: [], submittals: [],
  blockingRfiCount: 0, overdueRfiCount: 0, overdueSubmittalCount: 0, supersededRevisionCount: 0,
};

export function TechnicalOfficePanel(props: TechnicalOfficePanelProps) {
  const { apiBaseUrl, tenantId, userId, projectId, isOnline, refreshToken, onChanged } = props;
  const identity = useMemo(() => ({ tenantId, userId }), [tenantId, userId]);
  const cacheKey = useMemo(() => scopedStorageKey(`pmcs-technical-office:${projectId}`), [projectId]);
  const [state, setState] = useState<TechnicalOfficeStateModel>(emptyState);
  const [message, setMessage] = useState("در حال دریافت دفتر فنی…");
  const [busy, setBusy] = useState(false);

  const [documentTitle, setDocumentTitle] = useState("");
  const [documentDiscipline, setDocumentDiscipline] = useState("");
  const [documentType, setDocumentType] = useState<TechnicalDocumentType>("Drawing");
  const [documentOriginator, setDocumentOriginator] = useState("");

  const [revisionDocumentId, setRevisionDocumentId] = useState("");
  const [revisionCode, setRevisionCode] = useState("");
  const [revisionFileName, setRevisionFileName] = useState("");
  const [revisionFileReference, setRevisionFileReference] = useState("");
  const [revisionHash, setRevisionHash] = useState("");

  const [rfiTitle, setRfiTitle] = useState("");
  const [rfiQuestion, setRfiQuestion] = useState("");
  const [rfiRequestedFrom, setRfiRequestedFrom] = useState("");
  const [rfiDiscipline, setRfiDiscipline] = useState("");
  const [rfiRequiredBy, setRfiRequiredBy] = useState("");
  const [rfiEvidence, setRfiEvidence] = useState("");
  const [rfiBlocking, setRfiBlocking] = useState(false);
  const [impactTime, setImpactTime] = useState(true);
  const [impactCost, setImpactCost] = useState(false);
  const [impactScope, setImpactScope] = useState(false);
  const [impactQuality, setImpactQuality] = useState(false);
  const [impactSafety, setImpactSafety] = useState(false);
  const [responseParty, setResponseParty] = useState("");
  const [responseText, setResponseText] = useState("");
  const [responseClassification, setResponseClassification] = useState<RfiResponseClassification>("DesignClarification");
  const [responseChangePotential, setResponseChangePotential] = useState(false);
  const [responseRevisionId, setResponseRevisionId] = useState("");
  const [reviewNote, setReviewNote] = useState("");

  const [submittalTitle, setSubmittalTitle] = useState("");
  const [submittalType, setSubmittalType] = useState<TechnicalSubmittalType>("MaterialOrProductData");
  const [submittalDiscipline, setSubmittalDiscipline] = useState("");
  const [submittalSubmitter, setSubmittalSubmitter] = useState("");
  const [submittalReviewer, setSubmittalReviewer] = useState("");
  const [submittalRevisionId, setSubmittalRevisionId] = useState("");
  const [submittalReviewDue, setSubmittalReviewDue] = useState("");
  const [submittalSupersedesId, setSubmittalSupersedesId] = useState("");
  const [submittalResubmissionNumber, setSubmittalResubmissionNumber] = useState(0);
  const [reviewOutcome, setReviewOutcome] = useState<SubmittalReviewOutcome>("Approved");

  const [transmittalSender, setTransmittalSender] = useState("");
  const [transmittalRecipient, setTransmittalRecipient] = useState("");
  const [transmittalRevisionId, setTransmittalRevisionId] = useState("");
  const [transmittalPurpose, setTransmittalPurpose] = useState("");
  const [transmittalChannel, setTransmittalChannel] = useState("");
  const [acknowledgmentReference, setAcknowledgmentReference] = useState("");

  const load = useCallback(async () => {
    if (!isOnline) {
      const cached = readCache(cacheKey);
      if (cached) setState(cached);
      setMessage(cached
        ? "نسخه ذخیره‌شده نمایش داده می‌شود؛ وضعیت رسمی ممکن است قدیمی باشد."
        : "بدون اتصال، نسخه ذخیره‌شده‌ای از دفتر فنی روی این دستگاه وجود ندارد.");
      return;
    }
    try {
      const next = await getTechnicalOfficeState(apiBaseUrl, identity, projectId);
      setState(next);
      localStorage.setItem(cacheKey, JSON.stringify(next));
      setMessage("دفتر فنی با آخرین وضعیت رسمی سرور به‌روز شد.");
    } catch (error) {
      setMessage(toUserMessage(error, "دفتر فنی دریافت نشد یا دسترسی این کاربر محدود است."));
    }
  }, [apiBaseUrl, cacheKey, identity, isOnline, projectId]);

  useEffect(() => {
    const timeoutId = window.setTimeout(() => void load(), 0);
    return () => window.clearTimeout(timeoutId);
  }, [load, refreshToken]);

  async function run(action: () => Promise<unknown>, pendingMessage: string, failureMessage: string) {
    if (!isOnline) {
      setMessage("این اقدام رسمی فقط هنگام اتصال به سرور انجام می‌شود.");
      return;
    }
    setBusy(true);
    setMessage(pendingMessage);
    try {
      await action();
      await load();
      onChanged?.();
    } catch (error) {
      setMessage(toUserMessage(error, failureMessage));
    } finally {
      setBusy(false);
    }
  }

  async function registerDocument(event: FormEvent) {
    event.preventDefault();
    if (!documentTitle.trim() || !documentDiscipline.trim()) {
      setMessage("عنوان و رشته تخصصی مدرک الزامی است.");
      return;
    }
    await run(async () => {
      const created = await createTechnicalDocument(apiBaseUrl, identity, projectId, {
        title: documentTitle, type: documentType, discipline: documentDiscipline, originator: documentOriginator,
      });
      setRevisionDocumentId(created.id);
      setDocumentTitle(""); setDocumentOriginator("");
    }, "در حال ثبت شناسنامه مدرک…", "ثبت مدرک ناموفق بود.");
  }

  async function addRevision(event: FormEvent) {
    event.preventDefault();
    if (!revisionDocumentId || !revisionCode.trim() || !revisionFileName.trim() ||
        !revisionFileReference.trim() || !/^[0-9a-f]{64}$/iu.test(revisionHash.trim())) {
      setMessage("مدرک، کد بازنگری، نام و مرجع فایل و هش ۶۴ نویسه‌ای معتبر الزامی‌اند.");
      return;
    }
    await run(async () => {
      await createDocumentRevision(apiBaseUrl, identity, projectId, revisionDocumentId, {
        revisionCode, revisionDate: initialDate, purpose: "ForApproval", fileName: revisionFileName,
        fileReference: revisionFileReference, sha256: revisionHash,
        supersedesRevisionId: state.documents.find(item => item.id === revisionDocumentId)?.currentOfficialRevisionId ?? undefined,
      });
      setRevisionCode(""); setRevisionFileName(""); setRevisionFileReference(""); setRevisionHash("");
    }, "در حال ثبت بازنگری مستقل…", "ثبت بازنگری ناموفق بود.");
  }

  async function registerRfi(event: FormEvent) {
    event.preventDefault();
    if (!rfiTitle.trim() || !rfiQuestion.trim() || !rfiRequestedFrom.trim() || !rfiDiscipline.trim() || !rfiEvidence.trim()) {
      setMessage("عنوان، سؤال، مخاطب، رشته تخصصی و حداقل یک مرجع مدرک برای پرسش فنی (RFI) الزامی‌اند.");
      return;
    }
    const potentialImpact = (impactTime ? 1 : 0) + (impactCost ? 2 : 0) + (impactQuality ? 4 : 0) +
      (impactScope ? 8 : 0) + (impactSafety ? 16 : 0);
    await run(async () => {
      await createRfi(apiBaseUrl, identity, projectId, {
        title: rfiTitle, question: rfiQuestion, requestedFrom: rfiRequestedFrom,
        discipline: rfiDiscipline, raisedDate: initialDate, requiredByDate: rfiRequiredBy,
        potentialImpact, isBlocking: rfiBlocking, evidenceReferences: splitReferences(rfiEvidence),
      });
      setRfiTitle(""); setRfiQuestion(""); setRfiEvidence(""); setRfiRequiredBy(""); setRfiBlocking(false);
    }, "در حال ثبت پیش‌نویس پرسش فنی (RFI)…", "ثبت پرسش فنی (RFI) ناموفق بود.");
  }

  async function registerSubmittal(event: FormEvent) {
    event.preventDefault();
    if (!submittalTitle.trim() || !submittalDiscipline.trim() || !submittalSubmitter.trim() ||
        !submittalReviewer.trim() || !submittalRevisionId) {
      setMessage("عنوان، رشته تخصصی، ارسال‌کننده، بررسی‌کننده و بازنگری پیوست الزامی‌اند.");
      return;
    }
    await run(async () => {
      await createSubmittal(apiBaseUrl, identity, projectId, {
        title: submittalTitle, type: submittalType, discipline: submittalDiscipline,
        submitter: submittalSubmitter, reviewer: submittalReviewer,
        revisionIds: [submittalRevisionId], reviewDueDate: submittalReviewDue,
        supersedesSubmittalId: submittalSupersedesId,
        resubmissionNumber: submittalResubmissionNumber,
      });
      setSubmittalTitle(""); setSubmittalRevisionId(""); setSubmittalReviewDue("");
      setSubmittalSupersedesId(""); setSubmittalResubmissionNumber(0);
    }, "در حال ثبت بسته بررسی فنی…", "ثبت بسته بررسی فنی ناموفق بود.");
  }

  async function registerTransmittal(event: FormEvent) {
    event.preventDefault();
    if (!transmittalSender.trim() || !transmittalRecipient.trim() || !transmittalRevisionId ||
        !transmittalPurpose.trim() || !transmittalChannel.trim()) {
      setMessage("فرستنده، گیرنده، بازنگری مصوب، هدف و کانال ابلاغ الزامی‌اند.");
      return;
    }
    await run(async () => {
      await createTransmittal(apiBaseUrl, identity, projectId, {
        sender: transmittalSender, recipients: splitReferences(transmittalRecipient),
        revisionIds: [transmittalRevisionId], purpose: transmittalPurpose,
        deliveryChannel: transmittalChannel,
      });
      setTransmittalPurpose(""); setTransmittalRevisionId("");
    }, "در حال ساخت پیش‌نویس ابلاغ رسمی…", "ثبت ابلاغ رسمی ناموفق بود.");
  }

  const approvedRevisions = state.documentRevisions.filter(item => item.status === "Approved");

  return (
    <section className="section-block technical-office" id="technical-office">
      <div className="section-title">
        <div><p className="eyebrow">دفتر فنی و کنترل اسناد</p><h2>گردش رسمی مدارک و پاسخ‌ها</h2></div>
        <span className="section-note">بارگذاری فایل ≠ ابلاغ رسمی</span>
      </div>

      <div className="technical-summary-grid">
        <article><span>پرسش فنی مسدودکننده</span><strong>{state.blockingRfiCount.toLocaleString("fa-IR")}</strong></article>
        <article><span>پرسش فنی سررسیدگذشته</span><strong>{state.overdueRfiCount.toLocaleString("fa-IR")}</strong></article>
        <article><span>بررسی فنی معوق</span><strong>{state.overdueSubmittalCount.toLocaleString("fa-IR")}</strong></article>
        <article><span>بازنگری منسوخ</span><strong>{state.supersededRevisionCount.toLocaleString("fa-IR")}</strong></article>
      </div>
      <p className="calculation-note" aria-live="polite">{message}</p>

      <details className="technical-editor" open>
        <summary>مدرک و بازنگری</summary>
        <div className="technical-two-column">
          <form onSubmit={registerDocument}>
            <h3>ثبت شناسنامه مدرک</h3>
            <label><span>عنوان</span><input value={documentTitle} onChange={event => setDocumentTitle(event.target.value)} /></label>
            <label><span>رشته تخصصی</span><input value={documentDiscipline} onChange={event => setDocumentDiscipline(event.target.value)} /></label>
            <label><span>نوع مدرک</span><select value={documentType} onChange={event => setDocumentType(event.target.value as TechnicalDocumentType)}>{documentTypeOptions()}</select></label>
            <label><span>تهیه‌کننده اختیاری</span><input value={documentOriginator} onChange={event => setDocumentOriginator(event.target.value)} /></label>
            <button disabled={busy || !isOnline}>ثبت مدرک</button>
          </form>
          <form onSubmit={addRevision}>
            <h3>افزودن بازنگری غیرقابل‌بازنویسی</h3>
            <label><span>مدرک</span><select value={revisionDocumentId} onChange={event => setRevisionDocumentId(event.target.value)}><option value="">انتخاب مدرک</option>{state.documents.map(item => <option key={item.id} value={item.id}>{item.number} · {item.title}</option>)}</select></label>
            <label><span>کد بازنگری</span><input value={revisionCode} onChange={event => setRevisionCode(event.target.value)} /></label>
            <label><span>نام فایل</span><input value={revisionFileName} onChange={event => setRevisionFileName(event.target.value)} /></label>
            <label><span>مرجع فایل بارگذاری‌شده</span><input value={revisionFileReference} onChange={event => setRevisionFileReference(event.target.value)} /></label>
            <label className="wide"><span>هش یکپارچگی ۶۴ نویسه‌ای</span><input dir="ltr" value={revisionHash} onChange={event => setRevisionHash(event.target.value)} /></label>
            <button disabled={busy || !isOnline}>ثبت بازنگری</button>
          </form>
        </div>
        <WorkflowList title="بازنگری‌های مدارک" empty="هنوز بازنگری ثبت نشده است.">
          {state.documentRevisions.map(item => (
            <article key={item.id}>
              <div><strong>{documentName(state, item)}</strong><small>{item.revisionCode} · {revisionStatusLabel(item.status)} · {item.fileName}</small></div>
              <div className="technical-actions">
                {item.status === "Draft" && <button disabled={busy} onClick={() => void revisionAction(item, "submit")}>ارسال برای بررسی</button>}
                {item.status === "Submitted" && <><button disabled={busy} onClick={() => void revisionAction(item, "approve")}>تأیید</button><button disabled={busy || !reviewNote.trim()} onClick={() => void revisionAction(item, "return")}>بازگشت</button></>}
                {item.status === "Superseded" && <span className="stale-badge">نسخه منسوخ است</span>}
              </div>
            </article>
          ))}
        </WorkflowList>
      </details>

      <details className="technical-editor" open>
        <summary>پرسش فنی (RFI)</summary>
        <form className="technical-form-grid" onSubmit={registerRfi}>
          <label><span>عنوان</span><input value={rfiTitle} onChange={event => setRfiTitle(event.target.value)} /></label>
          <label><span>مخاطب پاسخ</span><input value={rfiRequestedFrom} onChange={event => setRfiRequestedFrom(event.target.value)} /></label>
          <label><span>رشته تخصصی</span><input value={rfiDiscipline} onChange={event => setRfiDiscipline(event.target.value)} /></label>
          <label><span>تاریخ موردنیاز</span><PersianDateInput value={rfiRequiredBy} onChange={setRfiRequiredBy} ariaLabel="تاریخ شمسی موردنیاز پاسخ فنی" /></label>
          <label className="wide"><span>سؤال یا تعارض مشاهده‌شده</span><textarea value={rfiQuestion} onChange={event => setRfiQuestion(event.target.value)} /></label>
          <label className="wide"><span>مراجع مدرک، جداشده با ویرگول</span><input value={rfiEvidence} onChange={event => setRfiEvidence(event.target.value)} /></label>
          <fieldset className="technical-checks"><legend>اثر احتمالی</legend>
            <label><input type="checkbox" checked={impactTime} onChange={event => setImpactTime(event.target.checked)} />زمان</label>
            <label><input type="checkbox" checked={impactCost} onChange={event => setImpactCost(event.target.checked)} />هزینه</label>
            <label><input type="checkbox" checked={impactScope} onChange={event => setImpactScope(event.target.checked)} />دامنه</label>
            <label><input type="checkbox" checked={impactQuality} onChange={event => setImpactQuality(event.target.checked)} />کیفیت</label>
            <label><input type="checkbox" checked={impactSafety} onChange={event => setImpactSafety(event.target.checked)} />ایمنی</label>
            <label><input type="checkbox" checked={rfiBlocking} onChange={event => setRfiBlocking(event.target.checked)} />مانع اجرا</label>
          </fieldset>
          <button disabled={busy || !isOnline}>ثبت پیش‌نویس</button>
        </form>
        <div className="technical-response-controls">
          <label><span>منبع پاسخ</span><input value={responseParty} onChange={event => setResponseParty(event.target.value)} /></label>
          <label><span>طبقه‌بندی پاسخ</span><select value={responseClassification} onChange={event => setResponseClassification(event.target.value as RfiResponseClassification)}>{rfiResponseClassificationOptions()}</select></label>
          <label><span>بازنگری مرجع اختیاری</span><select value={responseRevisionId} onChange={event => setResponseRevisionId(event.target.value)}><option value="">بدون بازنگری مرجع</option>{state.documentRevisions.map(item => <option key={item.id} value={item.id}>{documentName(state, item)} · {item.revisionCode}</option>)}</select></label>
          <label><span>اثر تغییر احتمالی</span><select value={responseChangePotential ? "yes" : "no"} onChange={event => setResponseChangePotential(event.target.value === "yes")}><option value="no">خیر؛ فقط پاسخ فنی</option><option value="yes">بله؛ نیازمند بررسی تغییر مستقل</option></select></label>
          <label className="wide"><span>متن پاسخ یا یادداشت بررسی</span><input value={responseText} onChange={event => setResponseText(event.target.value)} /></label>
          <label className="wide"><span>دلیل بازگشت یا درخواست توضیح</span><input value={reviewNote} onChange={event => setReviewNote(event.target.value)} /></label>
        </div>
        <WorkflowList title="گردش پرسش‌های فنی" empty="هنوز پرسش فنی (RFI) ثبت نشده است.">
          {state.rfis.map(item => (
            <article key={item.id} className={item.isBlocking && item.status !== "Closed" ? "attention-row" : undefined}>
              <div><strong>{item.number} · {item.title}</strong><small>{rfiStatusLabel(item.status)} · مخاطب: {item.requestedFrom} · موعد: {formatDate(item.requiredByDate)}</small></div>
              <div className="technical-actions">{rfiButtons(item)}</div>
            </article>
          ))}
        </WorkflowList>
      </details>

      <details className="technical-editor">
        <summary>مدرک ارسالی برای بررسی</summary>
        <form className="technical-form-grid" onSubmit={registerSubmittal}>
          <label><span>عنوان بسته</span><input value={submittalTitle} onChange={event => setSubmittalTitle(event.target.value)} /></label>
          <label><span>نوع</span><select value={submittalType} onChange={event => setSubmittalType(event.target.value as TechnicalSubmittalType)}>{submittalTypeOptions()}</select></label>
          <label><span>رشته تخصصی</span><input value={submittalDiscipline} onChange={event => setSubmittalDiscipline(event.target.value)} /></label>
          <label><span>ارسال‌کننده</span><input value={submittalSubmitter} onChange={event => setSubmittalSubmitter(event.target.value)} /></label>
          <label><span>بررسی‌کننده</span><input value={submittalReviewer} onChange={event => setSubmittalReviewer(event.target.value)} /></label>
          <label><span>بازنگری پیوست</span><select value={submittalRevisionId} onChange={event => setSubmittalRevisionId(event.target.value)}><option value="">انتخاب بازنگری</option>{state.documentRevisions.map(item => <option key={item.id} value={item.id}>{documentName(state, item)} · {item.revisionCode}</option>)}</select></label>
          <label><span>مهلت بررسی</span><PersianDateInput value={submittalReviewDue} onChange={setSubmittalReviewDue} ariaLabel="مهلت شمسی بررسی فنی" /></label>
          <button disabled={busy || !isOnline}>ثبت بسته</button>
        </form>
        <div className="technical-response-controls">
          <label><span>نتیجه بررسی</span><select value={reviewOutcome} onChange={event => setReviewOutcome(event.target.value as SubmittalReviewOutcome)}>{reviewOutcomeOptions()}</select></label>
        </div>
        <WorkflowList title="گردش بررسی فنی" empty="هنوز بسته بررسی فنی ثبت نشده است.">
          {state.submittals.map(item => <article key={item.id}><div><strong>{item.number} · {item.title}</strong><small>{submittalStatusLabel(item.status)} · مهلت: {formatDate(item.reviewDueDate)}</small></div><div className="technical-actions">{submittalButtons(item)}</div></article>)}
        </WorkflowList>
      </details>

      <details className="technical-editor">
        <summary>ابلاغ رسمی مدارک</summary>
        <form className="technical-form-grid" onSubmit={registerTransmittal}>
          <label><span>فرستنده</span><input value={transmittalSender} onChange={event => setTransmittalSender(event.target.value)} /></label>
          <label><span>گیرندگان، جداشده با ویرگول</span><input value={transmittalRecipient} onChange={event => setTransmittalRecipient(event.target.value)} /></label>
          <label><span>بازنگری مصوب</span><select value={transmittalRevisionId} onChange={event => setTransmittalRevisionId(event.target.value)}><option value="">انتخاب بازنگری مصوب</option>{approvedRevisions.map(item => <option key={item.id} value={item.id}>{documentName(state, item)} · {item.revisionCode}</option>)}</select></label>
          <label><span>کانال تحویل</span><input value={transmittalChannel} onChange={event => setTransmittalChannel(event.target.value)} /></label>
          <label className="wide"><span>هدف ابلاغ</span><input value={transmittalPurpose} onChange={event => setTransmittalPurpose(event.target.value)} /></label>
          <button disabled={busy || !isOnline}>ساخت پیش‌نویس</button>
        </form>
        <div className="technical-response-controls"><label className="wide"><span>مرجع رسید دریافت</span><input value={acknowledgmentReference} onChange={event => setAcknowledgmentReference(event.target.value)} /></label></div>
        <WorkflowList title="دفتر ابلاغ" empty="هنوز ابلاغ رسمی ثبت نشده است.">
          {state.transmittals.map(item => <article key={item.id}><div><strong>{item.number} · {item.purpose}</strong><small>{transmittalStatusLabel(item.status)} · {item.sender} ← {item.recipients.join("، ")}</small></div><div className="technical-actions">{transmittalButtons(item)}</div></article>)}
        </WorkflowList>
      </details>
      <p className="technical-boundary">پاسخ یا بازنگری فنی به‌تنهایی مبلغ، مدت یا دامنه قرارداد را تغییر نمی‌دهد؛ تغییر تجاری فقط از گردش مستقل و مصوب قرارداد ساخته می‌شود.</p>
    </section>
  );

  async function revisionAction(item: DocumentRevisionModel, action: "submit" | "approve" | "return") {
    await run(() => transitionDocumentRevision(apiBaseUrl, identity, projectId, item, action, reviewNote),
      "در حال ثبت گردش بازنگری…", "تغییر وضعیت بازنگری ناموفق بود.");
  }

  function rfiButtons(item: RfiModel) {
    if (item.status === "Draft") return <button disabled={busy} onClick={() => void simpleRfiAction(item, "internal-review")}>ارسال به بررسی داخلی</button>;
    if (item.status === "InternalReview") return <><button disabled={busy} onClick={() => void simpleRfiAction(item, "issue")}>صدور رسمی</button><button disabled={busy || !reviewNote.trim()} onClick={() => void simpleRfiAction(item, "return")}>بازگشت</button></>;
    if (item.status === "Submitted" || item.status === "ClarificationRequired") return <button disabled={busy || !responseParty.trim() || !responseText.trim()} onClick={() => void respondRfi(item)}>ثبت پاسخ دریافتی</button>;
    if (item.status === "Answered") return <><button disabled={busy} onClick={() => void simpleRfiAction(item, "accept")}>پذیرش پاسخ</button><button disabled={busy || !reviewNote.trim()} onClick={() => void simpleRfiAction(item, "clarification")}>توضیح بیشتر</button></>;
    if (item.status === "ResponseAccepted") return <button disabled={busy} onClick={() => void simpleRfiAction(item, "close")}>بستن پس از تعیین تکلیف</button>;
    return <span>{rfiStatusLabel(item.status)}</span>;
  }

  async function simpleRfiAction(item: RfiModel, action: "internal-review" | "issue" | "accept" | "clarification" | "close" | "return") {
    await run(() => transitionRfi(apiBaseUrl, identity, projectId, item, action, reviewNote),
      "در حال ثبت گردش پرسش فنی (RFI)…", "تغییر وضعیت پرسش فنی (RFI) ناموفق بود.");
  }

  async function respondRfi(item: RfiModel) {
    await run(() => recordRfiResponse(apiBaseUrl, identity, projectId, item, {
      responseText, respondingParty: responseParty, responseAt: new Date().toISOString(),
      classification: responseClassification, changePotential: responseChangePotential,
      referencedRevisionIds: responseRevisionId ? [responseRevisionId] : [],
    }), "در حال ثبت پاسخ دریافتی…", "ثبت پاسخ پرسش فنی (RFI) ناموفق بود.");
  }

  function submittalButtons(item: SubmittalModel) {
    if (item.status === "Draft") return <button disabled={busy} onClick={() => void submittalAction(item, "submit")}>ارسال رسمی</button>;
    if (item.status === "Submitted") return <button disabled={busy} onClick={() => void submittalAction(item, "begin-review")}>شروع بررسی</button>;
    if (item.status === "UnderReview") return <button disabled={busy || ((reviewOutcome === "Rejected" || reviewOutcome === "ReviseAndResubmit") && !reviewNote.trim())} onClick={() => void performSubmittalReview(item)}>ثبت نتیجه</button>;
    if (item.status === "Approved" || item.status === "ApprovedAsNoted" || item.status === "Rejected") return <button disabled={busy} onClick={() => void submittalAction(item, "close")}>بستن گردش</button>;
    if (item.status === "ReviseAndResubmit") return <button disabled={busy} onClick={() => prepareResubmission(item)}>آماده‌سازی ارسال مجدد</button>;
    return <span>{submittalStatusLabel(item.status)}</span>;
  }

  async function submittalAction(item: SubmittalModel, action: "submit" | "begin-review" | "close") {
    await run(() => transitionSubmittal(apiBaseUrl, identity, projectId, item, action),
      "در حال ثبت گردش بررسی فنی…", "تغییر وضعیت بررسی فنی ناموفق بود.");
  }

  async function performSubmittalReview(item: SubmittalModel) {
    await run(() => reviewSubmittal(apiBaseUrl, identity, projectId, item, reviewOutcome, reviewNote),
      "در حال ثبت نتیجه بررسی…", "ثبت نتیجه بررسی فنی ناموفق بود.");
  }

  function prepareResubmission(item: SubmittalModel) {
    setSubmittalTitle(item.title);
    setSubmittalType(item.type);
    setSubmittalDiscipline(item.discipline);
    setSubmittalSubmitter(item.submitter);
    setSubmittalReviewer(item.reviewer);
    setSubmittalSupersedesId(item.id);
    setSubmittalResubmissionNumber(item.resubmissionNumber + 1);
    setSubmittalRevisionId("");
    setMessage("اطلاعات ارسال مجدد آماده شد؛ بازنگری تازه را انتخاب و بسته جدید را ثبت کنید.");
  }

  function transmittalButtons(item: TransmittalModel) {
    if (item.status === "Draft") return <button disabled={busy} onClick={() => void transmittalAction(item, "issue")}>صدور و جاری‌کردن</button>;
    if (item.status === "Issued") return <button disabled={busy || !acknowledgmentReference.trim()} onClick={() => void transmittalAction(item, "acknowledge")}>ثبت رسید</button>;
    return <span>رسید ثبت شده</span>;
  }

  async function transmittalAction(item: TransmittalModel, action: "issue" | "acknowledge") {
    await run(() => transitionTransmittal(apiBaseUrl, identity, projectId, item, action, acknowledgmentReference),
      "در حال ثبت ابلاغ رسمی…", "تغییر وضعیت ابلاغ ناموفق بود.");
  }
}

function WorkflowList({ title, empty, children }: { title: string; empty: string; children: ReactNode }) {
  const items = Array.isArray(children) ? children : [children];
  return <div className="technical-workflow-list"><h3>{title}</h3>{items.length > 0 && items[0] ? children : <p className="empty-state">{empty}</p>}</div>;
}

function splitReferences(value: string): string[] {
  return value.split(/[,،\n]/u).map(item => item.trim()).filter(Boolean);
}

function readCache(key: string): TechnicalOfficeStateModel | null {
  try { const value = localStorage.getItem(key); return value ? JSON.parse(value) as TechnicalOfficeStateModel : null; }
  catch { return null; }
}

function documentName(state: TechnicalOfficeStateModel, revision: DocumentRevisionModel): string {
  const document = state.documents.find(item => item.id === revision.documentId);
  return document ? `${document.number} · ${document.title}` : "مدرک نامشخص";
}

function formatDate(value: string | null): string {
  return value ? formatPersianDate(value) : "تعریف نشده";
}

function revisionStatusLabel(value: DocumentRevisionModel["status"]): string {
  return ({ Draft: "پیش‌نویس", Submitted: "ارسال‌شده", Returned: "بازگشتی", Approved: "مصوب", Issued: "ابلاغ‌شده", Superseded: "منسوخ" })[value];
}
function rfiStatusLabel(value: RfiModel["status"]): string {
  return ({ Draft: "پیش‌نویس", InternalReview: "بررسی داخلی", Submitted: "صادرشده", Answered: "پاسخ‌داده‌شده", ResponseAccepted: "پاسخ پذیرفته‌شده", ClarificationRequired: "نیازمند توضیح", Closed: "بسته" })[value];
}
function submittalStatusLabel(value: SubmittalModel["status"]): string {
  return ({ Draft: "پیش‌نویس", Submitted: "ارسال‌شده", UnderReview: "در حال بررسی", Approved: "تأییدشده", ApprovedAsNoted: "تأیید مشروط", ReviseAndResubmit: "اصلاح و ارسال مجدد", Rejected: "ردشده", Closed: "بسته" })[value];
}
function transmittalStatusLabel(value: TransmittalModel["status"]): string {
  return ({ Draft: "پیش‌نویس", Issued: "صادرشده", Acknowledged: "رسید ثبت‌شده" })[value];
}

function documentTypeOptions() {
  return <><option value="Drawing">نقشه</option><option value="Specification">مشخصات فنی</option><option value="MethodStatement">روش اجرا</option><option value="ShopDrawing">نقشه کارگاهی</option><option value="CalculationOrReport">محاسبه یا گزارش</option><option value="Correspondence">مکاتبه</option><option value="Other">سایر</option></>;
}
function submittalTypeOptions() {
  return <><option value="MaterialOrProductData">مصالح یا اطلاعات محصول</option><option value="ShopDrawing">نقشه کارگاهی</option><option value="MethodStatement">روش اجرا</option><option value="SampleOrMockup">نمونه</option><option value="TechnicalCalculation">محاسبه فنی</option><option value="TestOrCertificate">آزمایش یا گواهی</option><option value="AsBuiltOrHandover">چون‌ساخت یا تحویل</option></>;
}
function rfiResponseClassificationOptions() {
  return <><option value="InformationOnly">صرفاً اطلاع‌رسانی</option><option value="DesignClarification">رفع ابهام طراحی</option><option value="NewRevisionRequired">نیازمند بازنگری جدید</option><option value="InstructionPotential">محتمل برای دستور فنی</option><option value="ChangePotential">محتمل برای تغییر</option></>;
}
function reviewOutcomeOptions() {
  return <><option value="Approved">تأیید</option><option value="ApprovedAsNoted">تأیید مشروط</option><option value="ReviseAndResubmit">اصلاح و ارسال مجدد</option><option value="Rejected">رد</option><option value="ForInformation">صرفاً جهت اطلاع</option></>;
}
