"use client";

import { type FormEvent, useState } from "react";
import { useCollaborationCommandGate } from "@/components/collaboration-command-gate";
import { CollaborationAccessError } from "@/lib/collaboration-events";
import { loadProjectMessageAttachments, type ProjectMessageAttachment } from "@/lib/collaboration-attachments";
import {
  CollaborationEvidenceSourceAlreadyExists, CollaborationEvidenceValidationError,
  convertProjectMessageToEvidence, loadProjectMessageConversions,
} from "@/lib/collaboration-conversions";
import { CollaborationRevisionConflict } from "@/lib/collaboration-interactions";
import type { ProjectConversationMessage } from "@/lib/collaboration-room";
import { getDailyReport, listDailyReports, type DailyReportSummary } from "@/lib/daily-reports";
import { ApiRequestError } from "@/lib/localization";
import { formatPersianDate } from "@/lib/persian-date";

const evidenceMimeTypes = new Set(["image/jpeg", "image/png", "image/webp", "image/heic",
  "image/heif", "application/pdf"]);
const reportStatusLabels: Readonly<Record<DailyReportSummary["status"], string>> = {
  Draft: "پیش‌نویس", Submitted: "ارسال‌شده", Returned: "بازگشت‌داده‌شده",
  Approved: "تأییدشده", Rejected: "ردشده", Superseded: "جایگزین‌شده",
};

type Phase = "closed" | "checking" | "editing" | "sending" | "uncertain" |
  "conflict" | "done" | "exists";

export function ProjectEvidenceConversion({ projectId, message, tenantId, actorUserId,
  onAccessLoss, onChanged }: {
  readonly projectId: string; readonly message: ProjectConversationMessage;
  readonly tenantId: string; readonly actorUserId: string;
  readonly onAccessLoss: (status: number) => void; readonly onChanged: () => void;
}) {
  const { canCommand, isCurrent } = useCollaborationCommandGate();
  const [phase, setPhase] = useState<Phase>("closed");
  const [base, setBase] = useState(message);
  const [reports, setReports] = useState<readonly DailyReportSummary[]>([]);
  const [selectedReport, setSelectedReport] = useState<DailyReportSummary | null>(null);
  const [reportBusy, setReportBusy] = useState(false);
  const [attachments, setAttachments] = useState<readonly ProjectMessageAttachment[]>([]);
  const [documentId, setDocumentId] = useState("");
  const [dailyFactId, setDailyFactId] = useState("");
  const [identity, setIdentity] = useState<{ destinationId: string; key: string } | null>(null);
  const [confirmed, setConfirmed] = useState(false);
  const [status, setStatus] = useState("");
  const stale = (phase === "editing" || phase === "uncertain") && message.revision > base.revision;
  const attachment = attachments.find((item) => item.documentId === documentId);
  const identityHeaders = { tenantId, userId: actorUserId };

  function fail(error: unknown, fallback: string) {
    if (error instanceof CollaborationAccessError) onAccessLoss(error.status);
    else if (error instanceof ApiRequestError && [401, 403].includes(error.status))
      onAccessLoss(error.status);
    else setStatus(fallback);
  }

  async function selectReport(id: string) {
    if (!isCurrent()) return;
    setSelectedReport(null); setDailyFactId(""); setConfirmed(false); setReportBusy(true);
    try {
      const report = await getDailyReport("/api/pmcs", identityHeaders, projectId, id);
      if (!report || report.id.toLowerCase() !== id.toLowerCase() ||
          report.projectId.toLowerCase() !== projectId.toLowerCase() ||
          !Array.isArray(report.facts)) throw new Error("جزئیات گزارش معتبر نیست.");
      setSelectedReport(report); setStatus("");
    } catch (error) { fail(error, "جزئیات گزارش دریافت نشد؛ دوباره تلاش کنید."); }
    finally { setReportBusy(false); }
  }

  async function open() {
    if (!isCurrent() || phase !== "closed") return;
    setPhase("checking"); setStatus("");
    try {
      const [lineage, files, allReports] = await Promise.all([
        loadProjectMessageConversions("/api/pmcs", projectId, message.id),
        loadProjectMessageAttachments("/api/pmcs", projectId, message.id),
        listDailyReports("/api/pmcs", identityHeaders, projectId),
      ]);
      if (!Array.isArray(allReports) || allReports.some((item) =>
          item.projectId?.toLowerCase() !== projectId.toLowerCase()))
        throw new Error("گزارش‌ها بیرون از محدودهٔ پروژه‌اند.");
      const used = new Set(lineage.filter((item) => item.destinationType === "Evidence")
        .flatMap((item) => item.documents.map((document) => document.id.toLowerCase())));
      const eligible = files.filter((item) => evidenceMimeTypes.has(item.contentType) &&
        item.sizeBytes <= 25 * 1024 * 1024 && !used.has(item.documentId.toLowerCase()));
      if (eligible.length === 0 || allReports.length === 0) {
        setPhase("closed");
        setStatus("برای تبدیل، یک فایل آزادشده تبدیل‌نشده در همین پیام و یک گزارش روزانهٔ همان پروژه لازم است.");
        return;
      }
      setAttachments(eligible); setReports(allReports); setDocumentId(eligible[0].documentId);
      setBase(message);
      setIdentity({ destinationId: crypto.randomUUID(), key: crypto.randomUUID() });
      setConfirmed(false); setPhase("editing");
      await selectReport(allReports[0].id);
    } catch (error) { setPhase("closed"); fail(error, "بررسی تبار، فایل یا گزارش کامل نشد؛ دوباره تلاش کنید."); }
  }

  async function reconcile() {
    if (!isCurrent()) return;
    try {
      const lineage = await loadProjectMessageConversions("/api/pmcs", projectId, message.id);
      if (lineage.some((item) => item.destinationType === "Evidence" &&
          item.documents.some((document) => document.id.toLowerCase() === documentId.toLowerCase()))) {
        setPhase("done"); setStatus("مدرک رسمی این فایل در تبار پیام ثبت شده است."); onChanged();
      } else setStatus("هنوز مدرک این فایل ثبت‌شده دیده نمی‌شود؛ تلاش مجدد فقط با همان شناسه انجام شود.");
    } catch (error) { fail(error, "بازبینی نتیجه کامل نشد؛ هویت درخواست حفظ شده است."); }
  }

  async function rebaseMessage() {
    if (!isCurrent() || message.revision <= base.revision || message.deletedAt || message.redactedAt) return;
    try {
      const [lineage, currentFiles] = await Promise.all([
        loadProjectMessageConversions("/api/pmcs", projectId, message.id),
        loadProjectMessageAttachments("/api/pmcs", projectId, message.id),
      ]);
      if (lineage.some(item => item.destinationType === "Evidence" &&
          item.documents.some(document => document.id.toLowerCase() === documentId.toLowerCase()))) {
        setPhase("exists"); setStatus("رکورد رسمی این فایل قبلاً در تبار پیام ثبت شده است."); return;
      }
      const retained = currentFiles.find(item => attachment && item.documentId === attachment.documentId &&
        item.sha256 === attachment.sha256 && item.versionNumber === attachment.versionNumber &&
        item.originalFileName === attachment.originalFileName && item.contentType === attachment.contentType &&
        item.sizeBytes === attachment.sizeBytes);
      setAttachments(currentFiles); setDocumentId(retained?.documentId ?? "");
      setBase(message); setIdentity({ destinationId: crypto.randomUUID(), key: crypto.randomUUID() });
      setConfirmed(false); setPhase("editing");
      setStatus(retained ? "" : "فایل انتخاب‌شده تغییر کرده یا در دسترس نیست؛ منبع جاری را دوباره انتخاب کنید.");
    } catch (error) { fail(error, "بازخوانی فایل و تبار کامل نشد؛ پیش‌نویس حفظ شده است."); }
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!isCurrent() || stale) return;
    if (!identity || !confirmed || !selectedReport || !attachment || reportBusy ||
        (phase !== "editing" && phase !== "uncertain")) return;
    if (!navigator.onLine) { setStatus("تبدیل رسمی فقط هنگام اتصال به سرور انجام می‌شود."); return; }
    setPhase("sending"); setStatus("در حال ثبت مدرک رسمی با تبار فایل…");
    try {
      const result = await convertProjectMessageToEvidence("/api/pmcs", projectId, base,
        actorUserId, attachment, selectedReport.id, dailyFactId || null,
        identity.destinationId, identity.key);
      setPhase("done"); setStatus(`مدرک رسمی با ارجاع ${result.destinationReference} ثبت شد.`); onChanged();
    } catch (error) {
      if (error instanceof CollaborationAccessError) onAccessLoss(error.status);
      else if (error instanceof CollaborationRevisionConflict) {
        setPhase("conflict"); setStatus("نسخهٔ پیام تغییر کرده است؛ فایل و نسخهٔ تازه را بررسی و تبدیل را دوباره تأیید کنید.");
        onChanged();
      } else if (error instanceof CollaborationEvidenceSourceAlreadyExists) {
        setPhase("exists"); setStatus(error.message); onChanged();
      } else if (error instanceof CollaborationEvidenceValidationError) {
        setPhase("editing"); setIdentity({ destinationId: crypto.randomUUID(), key: crypto.randomUUID() });
        setConfirmed(false); setStatus(error.message);
      } else {
        setPhase("uncertain");
        setStatus("نتیجهٔ ارسال قطعی نیست؛ ابتدا تبار را بازبینی یا با همان شناسه تلاش مجدد کنید.");
      }
    }
  }

  const editable = phase === "editing";
  return <section className="collaboration-edit" aria-label="تبدیل فایل پیام به مدرک رسمی">
    {phase === "closed" && <button className="secondary-button" type="button" disabled={!canCommand}
      onClick={() => void open()}>ساخت مدرک رسمی از فایل پیام</button>}
    {phase === "checking" && <p role="status">در حال بررسی فایل آزادشده و تبار پیام…</p>}
    {status && <p role={phase === "conflict" ? "alert" : "status"}>{status}</p>}
    {(phase === "conflict" || stale) && <div>
      {stale && <p role="alert">نسخهٔ پیام تغییر کرده است؛ پیش‌نویس را بر پایهٔ نسخهٔ تازه بررسی و دوباره تأیید کنید.</p>}
      <p>نسخهٔ فعلی پیام: {message.body}</p>
      {message.revision > base.revision && !message.deletedAt && !message.redactedAt &&
        <button className="secondary-button" type="button" disabled={!canCommand}
          onClick={() => void rebaseMessage()}>تبدیل مدرک بر پایهٔ نسخهٔ تازه</button>}
    </div>}
    {(phase === "editing" || phase === "sending" || phase === "uncertain") &&
      <form onSubmit={(event) => void submit(event)}>
        <p>تنها یک فایل آزادشده متصل به همین پیام به مدرک رسمی تبدیل می‌شود؛ هش و نسخهٔ فایل در تبار می‌ماند.</p>
        <label>فایل آزادشده پیام<select required value={documentId} disabled={!editable}
          onChange={(event) => { setDocumentId(event.target.value); setConfirmed(false); }}>
          <option value="">انتخاب فایل جاری</option>
          {attachments.map((item) => <option key={item.documentId} value={item.documentId}>
            {item.originalFileName} · نسخهٔ {item.versionNumber} · {item.sizeBytes.toLocaleString("fa-IR")} بایت
          </option>)}
        </select></label>
        {attachment && <p>هش منبع: <code dir="ltr">{attachment.sha256}</code></p>}
        <label>گزارش روزانه<select required value={selectedReport?.id ?? ""} disabled={!editable || reportBusy}
          onChange={(event) => void selectReport(event.target.value)}>
          {reports.map((item) => <option key={item.id} value={item.id}>
            {formatPersianDate(item.reportDate)} · {item.locationName || "بدون عنوان محل"} · {reportStatusLabels[item.status]}
          </option>)}
        </select></label>
        {reportBusy && <p role="status">در حال دریافت واقعیت‌های گزارش…</p>}
        {selectedReport && <label>واقعیت روزانه، اختیاری<select value={dailyFactId}
          disabled={!editable} onChange={(event) => { setDailyFactId(event.target.value); setConfirmed(false); }}>
          <option value="">مدرک مربوط به کل گزارش</option>
          {selectedReport.facts?.map((fact) => <option key={fact.id} value={fact.id}>
            {fact.description.slice(0, 90)}
          </option>)}
        </select></label>}
        <p>نسخهٔ پیام: {base.revision}؛ فایل بدون آپلود دوباره از منبع آزادشده رسمی خوانده می‌شود.</p>
        <label><input type="checkbox" checked={confirmed} disabled={!canCommand || !editable || reportBusy}
          onChange={(event) => setConfirmed(event.target.checked)} />
          تبدیل همین فایل به مدرک رسمی برای گزارش/واقعیت انتخاب‌شده را تأیید می‌کنم.</label>
        <div className="collaboration-message-actions">
          <button type="submit" disabled={!canCommand || stale || phase === "sending" || !confirmed || !selectedReport || !attachment || reportBusy}>
            {phase === "uncertain" ? "تلاش مجدد با همان شناسه" : phase === "sending"
              ? "در حال ثبت…" : "تأیید و ساخت مدرک رسمی"}
          </button>
          {phase === "uncertain" && <button className="secondary-button" type="button" disabled={!canCommand}
            onClick={() => void reconcile()}>بازبینی نتیجهٔ مدرک</button>}
          {editable && <button className="secondary-button" type="button" onClick={() => {
            setPhase("closed"); setStatus(""); setIdentity(null);
          }}>انصراف از مدرک</button>}
        </div>
      </form>}
  </section>;
}
